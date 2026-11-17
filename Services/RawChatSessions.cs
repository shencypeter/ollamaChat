using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using LangChain.Memory;
using LangChain.Providers;
using Microsoft.Extensions.Options;

namespace AiTeachingAssistant.Services;

// Server-process memory only. Each random ID belongs to exactly one course/user.
public sealed class RawChatSessions(TimeProvider clock, IOptions<CaseChatOptions> options, IOptions<OllamaOptions> model) : BackgroundService
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, Entry> entries = [];
    private readonly CaseChatOptions settings = options.Value;
    private readonly OllamaOptions modelSettings = model.Value;

    private sealed class Entry(Guid course, Guid actor, DateTimeOffset now)
    {
        public Guid Course { get; } = course;
        public Guid Actor { get; } = actor;
        public ConversationBufferMemory Memory { get; } = new(new ChatMessageHistory());
        public List<RequestMetrics> Metrics {get;}=[];
        public CancellationTokenSource Lifetime { get; } = new();
        public DateTimeOffset LastActivity { get; set; } = now;
        public bool Busy { get; set; }
        public bool Removed { get; set; }
    }

    public Guid Create(Guid course, Guid actor)
    {
        lock (gate)
        {
            RemoveExpired();
            if (entries.Count >= settings.MaxRawSessions || entries.Values.Count(e => e.Actor == actor) >= settings.MaxRawSessionsPerUser)
                throw new CaseChatException(429, "暫存對話數已達上限，請清除舊對話後再試。");
            var id = Guid.NewGuid();
            entries.Add(id, new Entry(course, actor, clock.GetUtcNow()));
            return id;
        }
    }

    public async Task<RawChatReply> Send(Guid course, Guid actor, Guid id, string content, IOllamaClient provider, CancellationToken ct,Func<CancellationToken,Task>? authorizeCompletion=null)
    {
        Entry entry;
        List<OllamaMessage> messages;
        lock (gate)
        {
            RemoveExpired();
            entry = Find(course, actor, id);
            if (entry.Busy) throw new CaseChatException(409, "上一則訊息仍在處理中，請稍後再試。");
            if (string.IsNullOrWhiteSpace(content)) throw new CaseChatException(400, "請輸入訊息。");
            if (content.Length > settings.MaxMessageCharacters) throw new CaseChatException(413, "訊息超過長度上限。");
            messages = entry.Memory.ChatHistory.Messages.Select(m => new OllamaMessage(m.Role == MessageRole.Ai ? "assistant" : "user", m.Content)).ToList();
            messages.Add(new("user", content));
            CaseChatBudget.EnsureFits(messages, settings, modelSettings);
            ct.ThrowIfCancellationRequested();
            entry.Busy = true;
            entry.LastActivity = clock.GetUtcNow();
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, entry.Lifetime.Token);
        try
        {
            var result = await provider.ConversationAsync(messages, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if(authorizeCompletion is not null)
            {
                try{await authorizeCompletion(linked.Token);}
                catch
                {
                    lock(gate)if(!entry.Removed)Remove(id,entry);
                    throw;
                }
            }
            if (string.IsNullOrWhiteSpace(result.Content) || result.Content.Length > settings.MaxResponseCharacters)
                throw new CaseChatException(502, "模型回覆為空或超過長度上限，請重試。",new("reply_size",$"模型回覆長度 {result.Content?.Length??0} 字元；需為非空白，且不超過 {settings.MaxResponseCharacters} 字元。"));
            lock (gate)
            {
                linked.Token.ThrowIfCancellationRequested();
                if (entry.Removed) throw new OperationCanceledException(linked.Token);
                // Only a completed pair enters the real LangChain full buffer.
                // A failed human draft stays with the caller for a clean retry.
                entry.Memory.ChatHistory.AddMessages([Message.Human(content), Message.Ai(result.Content)]).GetAwaiter().GetResult();
                entry.Metrics.Add(ChatPerformanceMetrics.Clean(result.Metrics));
                entry.LastActivity = clock.GetUtcNow();
                return new(id, entry.Memory.ChatHistory.Messages.Select((m,index) => new RawChatMessage(m.Role == MessageRole.Ai ? "assistant" : "user", m.Content,m.Role==MessageRole.Ai?entry.Metrics[index/2]:null)).ToArray(), modelSettings.Model);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (CaseChatException) { throw; }
        catch (Exception)
        {
            // Provider exception messages/inner exceptions can contain private text.
            throw new CaseChatException(502, "模型暫時無法回覆，請稍後重試。");
        }
        finally
        {
            lock (gate)
            {
                entry.Busy = false;
                if (entry.Removed) entry.Lifetime.Dispose();
            }
        }
    }

    public Task Clear(Guid course, Guid actor, Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate) Remove(id, Find(course, actor, id));
        return Task.CompletedTask;
    }
    public Task ClearAll(Guid actor,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock(gate)foreach(var (id,entry) in entries.Where(pair=>pair.Value.Actor==actor).ToArray())Remove(id,entry);
        return Task.CompletedTask;
    }

    private Entry Find(Guid course, Guid actor, Guid id) => entries.TryGetValue(id, out var entry) && entry.Course == course && entry.Actor == actor
        ? entry : throw new CaseChatException(404, "暫存對話不存在或已過期，請開始新對話。");

    private void RemoveExpired()
    {
        var cutoff = clock.GetUtcNow().AddMinutes(-settings.RawIdleMinutes);
        foreach (var (id, entry) in entries.Where(pair => pair.Value.LastActivity <= cutoff).ToArray()) Remove(id, entry);
    }

    private void Remove(Guid id, Entry entry)
    {
        entries.Remove(id);
        entry.Removed = true;
        entry.Lifetime.Cancel();
        entry.Memory.Clear().GetAwaiter().GetResult();
        entry.Metrics.Clear();
        if (!entry.Busy) entry.Lifetime.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                lock (gate) RemoveExpired();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { lock (gate) foreach (var (id, entry) in entries.ToArray()) Remove(id, entry); }
    }

    public override void Dispose()
    {
        lock (gate) foreach (var (id, entry) in entries.ToArray()) Remove(id, entry);
        base.Dispose();
    }
}
