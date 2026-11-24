using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;

namespace AiTeachingAssistant.Tests;

public sealed class CaseRawChatTests
{
    [Fact] public async Task RawLanguagePolicyIsIncludedInPreflightWithoutPollutingRejectedHistory()
    {
        using var sessions=Sessions(new(){MaxContextBytes=100});var course=Guid.NewGuid();var actor=Guid.NewGuid();var id=sessions.Create(course,actor);var provider=new Provider();
        Assert.Equal(413,(await Assert.ThrowsAsync<CaseChatException>(()=>sessions.Send(course,actor,id,"x",provider,default))).StatusCode);Assert.Empty(provider.Inputs);
        var allowed=new CaseChatModelChoice("qwen",new(){Model="synthetic",AssessmentSampling=new(){NumCtx=200000}},new(){MaxContextBytes=10000});
        var reply=await sessions.Send(course,actor,id,"accepted",provider,default,requestChoice:allowed);Assert.Equal(new[]{"accepted","answer 1"},reply.Messages.Select(m=>m.Content));
    }
    [Fact] public async Task EachRawSendCanChooseAModelWithoutLosingHistoryOrRelabelingEarlierPairs()
    {
        using var sessions=Sessions();var course=Guid.NewGuid();var actor=Guid.NewGuid();var id=sessions.Create(course,actor);var first=new Provider();var second=new Provider();
        var llama=new CaseChatModelChoice("llama3",new(){Model="raw-llama",AssessmentSampling=new(){NumCtx=200000}},new());
        var qwen=new CaseChatModelChoice("qwen",new(){Model="raw-qwen",AssessmentSampling=new(){NumCtx=200000}},new());
        await sessions.Send(course,actor,id,"first model",first,default,requestChoice:llama);
        var reply=await sessions.Send(course,actor,id,"second model",second,default,requestChoice:qwen);
        Assert.Equal(new[]{"first model","answer 1","second model"},second.Inputs.Single().Skip(1).Select(m=>m.Content));
        Assert.Equal(new[]{"system","user","assistant","user"},second.Inputs.Single().Select(m=>m.Role));
        Assert.Equal(new[]{"raw-llama","raw-llama","raw-qwen","raw-qwen"},reply.Messages.Select(m=>m.ModelName));
        reply=await sessions.Send(course,actor,id,"environment default",first,default);
        Assert.Equal("synthetic",reply.ModelName);Assert.Equal(new[]{"raw-llama","raw-llama","raw-qwen","raw-qwen","synthetic","synthetic"},reply.Messages.Select(m=>m.ModelName));
        Assert.Null(sessions.GetChoice(course,actor,id));
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>sessions.Send(course,Guid.NewGuid(),id,"foreign owner",first,default,requestChoice:llama))).StatusCode);
    }
    [Fact] public async Task SelectedRawLimitsRejectWithoutTrimmingMemoryAndDoNotPolluteTheNextModel()
    {
        using var sessions=Sessions();var course=Guid.NewGuid();var actor=Guid.NewGuid();var id=sessions.Create(course,actor);var provider=new Provider();
        await sessions.Send(course,actor,id,"retained",provider,default);
        var restricted=new CaseChatModelChoice("breeze",new(){Model="restricted",AssessmentSampling=new(){NumCtx=200000}},new(){MaxContextBytes=1});
        Assert.Equal(413,(await Assert.ThrowsAsync<CaseChatException>(()=>sessions.Send(course,actor,id,"rejected",provider,default,requestChoice:restricted))).StatusCode);
        Assert.Single(provider.Inputs);var next=await sessions.Send(course,actor,id,"next model",provider,default);
        Assert.Equal(new[]{"retained","answer 1","next model"},provider.Inputs.Last().Skip(1).Select(m=>m.Content));Assert.Equal(4,next.Messages.Count);
        Assert.All(next.Messages,m=>Assert.Equal("synthetic",m.ModelName));
    }
    [Fact] public async Task SelectedModelSnapshotBelongsOnlyToItsRawSessionAndNeverChangesTheDefault()
    {
        using var sessions=Sessions();var course=Guid.NewGuid();var actor=Guid.NewGuid();
        var selected=sessions.Create(course,actor,new("llama3",new(){Model="chosen-raw",AssessmentSampling=new(){NumCtx=200000}},new()));
        var normal=sessions.Create(course,actor);var provider=new Provider();
        Assert.Equal("chosen-raw",(await sessions.Send(course,actor,selected,"合成選模訊息",provider,default)).ModelName);
        Assert.Equal("synthetic",(await sessions.Send(course,actor,normal,"合成預設訊息",provider,default)).ModelName);
        Assert.Equal("llama3",sessions.GetChoice(course,actor,selected)!.Profile);Assert.Null(sessions.GetChoice(course,actor,normal));
        Assert.Equal(404,Assert.Throws<CaseChatException>(()=>sessions.GetChoice(course,Guid.NewGuid(),selected)).StatusCode);
    }
    [Fact]
    public async Task RawPerformanceIsPerReplyOnlyAndNeverEntersConversationContext()
    {
        using var sessions=Sessions();var course=Guid.NewGuid();var actor=Guid.NewGuid();var id=sessions.Create(course,actor);var provider=new Provider{Metrics=new(1000,0,0,1000,111,0,2,200,5,"stop")};
        await sessions.Send(course,actor,id,"first",provider,default);
        provider.Metrics=new(2000,0,0,2000,222,0,4,200,6,"length");
        var reply=await sessions.Send(course,actor,id,"second",provider,default);
        Assert.Null(reply.Messages[0].Metrics);Assert.Equal(111,reply.Messages[1].Metrics!.PromptTokens);Assert.Null(reply.Messages[2].Metrics);Assert.Equal(222,reply.Messages[3].Metrics!.PromptTokens);
        Assert.Equal(new[]{"first","answer 1","second"},provider.Inputs.Last().Skip(1).Select(m=>m.Content));
        await sessions.Clear(course,actor,id,default);var fresh=sessions.Create(course,actor);var next=await sessions.Send(course,actor,fresh,"new",provider,default);Assert.Equal(2,next.Messages.Count);
    }
    [Fact]
    public async Task ExplicitAccountResetClearsOnlyItsOwnerAndReleasesOrphanedSessionCapacity()
    {
        using var sessions=Sessions();var course=Guid.NewGuid();var actor=Guid.NewGuid();var other=Guid.NewGuid();var provider=new Provider();
        var first=sessions.Create(course,actor);sessions.Create(Guid.NewGuid(),actor);var peer=sessions.Create(course,other);
        await sessions.Send(course,other,peer,"peer context",provider,default);
        await sessions.ClearAll(actor,default);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>sessions.Send(course,actor,first,"no restoration",provider,default))).StatusCode);
        var reply=await sessions.Send(course,other,peer,"peer retained",provider,default);Assert.Equal(4,reply.Messages.Count);
        Assert.NotEqual(first,sessions.Create(course,actor));
    }
    [Fact]
    public async Task FullBufferPreservesRoleOrderAndEveryTurnBeyondFortyMessages()
    {
        using var sessions = Sessions(new() { MaxContextBytes = 100000 });
        var course = Guid.NewGuid(); var actor = Guid.NewGuid(); var id = sessions.Create(course, actor);
        var provider = new Provider();
        RawChatReply? reply = null;
        for (var i = 0; i < 24; i++) reply = await sessions.Send(course, actor, id, $"human {i}", provider, default);
        Assert.Equal(48, reply!.Messages.Count);
        Assert.Equal("system",provider.Inputs[^1][0].Role);Assert.Single(provider.Inputs[^1].Where(m=>m.Role=="system"));
        Assert.Equal("human 0", provider.Inputs[^1][1].Content);
        Assert.Equal("answer 1", provider.Inputs[^1][2].Content);
        Assert.Equal("human 23", provider.Inputs[^1][47].Content);
        Assert.Equal(Enumerable.Range(0, 48).Select(i => i % 2 == 0 ? "user" : "assistant"), reply.Messages.Select(m => m.Role));
        Assert.DoesNotContain(reply.Messages, m => m.Content.Contains("thinking"));
        Assert.Equal("synthetic", reply.ModelName);
    }

    [Fact]
    public async Task UserCourseAndSessionIsolationRejectsGuessedIds()
    {
        using var sessions = Sessions(); var course = Guid.NewGuid(); var actor = Guid.NewGuid();
        var id = sessions.Create(course, actor); var other = sessions.Create(course, actor); var provider = new Provider();
        await sessions.Send(course, actor, id, "first private turn", provider, default);
        var reply = await sessions.Send(course, actor, other, "different session", provider, default);
        Assert.Equal(2, reply.Messages.Count); Assert.Equal(new[]{"system","user"},provider.Inputs[^1].Select(m=>m.Role));
        Assert.Equal(404, (await Assert.ThrowsAsync<CaseChatException>(() => sessions.Send(Guid.NewGuid(), actor, id, "guess", provider, default))).StatusCode);
        Assert.Equal(404, (await Assert.ThrowsAsync<CaseChatException>(() => sessions.Send(course, Guid.NewGuid(), id, "guess", provider, default))).StatusCode);
        Assert.Equal(404, (await Assert.ThrowsAsync<CaseChatException>(() => sessions.Clear(course, Guid.NewGuid(), id, default))).StatusCode);
    }

    [Fact]
    public async Task ClearCancelsInflightAndLateProviderCannotRepopulate()
    {
        using var sessions = Sessions(); var course = Guid.NewGuid(); var actor = Guid.NewGuid(); var id = sessions.Create(course, actor);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        var provider = new Provider { Respond = async ct => { captured = ct; started.SetResult(); await finish.Task; return "late private reply"; } };
        var pending = sessions.Send(course, actor, id, "private input", provider, default);
        await started.Task;
        Assert.Equal(409, (await Assert.ThrowsAsync<CaseChatException>(() => sessions.Send(course, actor, id, "parallel", provider, default))).StatusCode);
        await sessions.Clear(course, actor, id, default);
        Assert.True(captured.IsCancellationRequested);
        finish.SetResult(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(404, (await Assert.ThrowsAsync<CaseChatException>(() => sessions.Send(course, actor, id, "restore", provider, default))).StatusCode);
        var next = sessions.Create(course, actor); var reply = await sessions.Send(course, actor, next, "fresh", new Provider(), default);
        Assert.Equal(2, reply.Messages.Count);
    }

    [Fact]
    public async Task IdleExpiryAndCapacityReclaimExpiredMemory()
    {
        var time = new ManualTime(); using var sessions = Sessions(new() { MaxRawSessions = 2, MaxRawSessionsPerUser = 1 }, time);
        var course = Guid.NewGuid(); var actor = Guid.NewGuid(); var id = sessions.Create(course, actor);
        Assert.Equal(429, Assert.Throws<CaseChatException>(() => sessions.Create(Guid.NewGuid(), actor)).StatusCode);
        sessions.Create(course, Guid.NewGuid());
        Assert.Equal(429, Assert.Throws<CaseChatException>(() => sessions.Create(course, Guid.NewGuid())).StatusCode);
        time.Advance(TimeSpan.FromMinutes(21));
        Assert.Equal(404, (await Assert.ThrowsAsync<CaseChatException>(() => sessions.Send(course, actor, id, "expired", new Provider(), default))).StatusCode);
        Assert.NotEqual(id, sessions.Create(course, actor));
    }

    [Fact]
    public async Task BackgroundTimerExpiresAnInflightSessionWithoutAnotherRequest()
    {
        var time = new ManualTime(); using var sessions = Sessions(new() { RawIdleMinutes = 1 }, time);
        await sessions.StartAsync(default);
        var course = Guid.NewGuid(); var actor = Guid.NewGuid(); var id = sessions.Create(course, actor);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new Provider { Respond = async ct => { using var registration = ct.Register(() => canceled.TrySetResult()); started.SetResult(); await finish.Task; return "late"; } };
        var pending = sessions.Send(course, actor, id, "expires during inference", provider, default);
        await started.Task;
        try
        {
            time.Advance(TimeSpan.FromMinutes(2));
            await canceled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally { finish.TrySetResult(); await sessions.StopAsync(default); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(404, (await Assert.ThrowsAsync<CaseChatException>(() => sessions.Send(course, actor, id, "expired", new Provider(), default))).StatusCode);
    }

    [Fact]
    public async Task FailedInferenceAndOversizedTurnsLeaveSuccessfulHistoryIntact()
    {
        var settings = new CaseChatOptions { MaxContextBytes = 700, MaxMessageCharacters = 300, MaxResponseCharacters = 100 };
        using var sessions = Sessions(settings); var course = Guid.NewGuid(); var actor = Guid.NewGuid(); var id = sessions.Create(course, actor);
        var provider = new Provider(); await sessions.Send(course, actor, id, "kept", provider, default);
        provider.Respond = _ => throw new HttpRequestException("private body");
        var failure = await Assert.ThrowsAsync<CaseChatException>(() => sessions.Send(course, actor, id, "failed draft", provider, default));
        Assert.Equal(502, failure.StatusCode); Assert.DoesNotContain("private body", failure.ToString());
        provider.Respond = _ => Task.FromResult(new string('x', 101));
        Assert.Equal(502, (await Assert.ThrowsAsync<CaseChatException>(() => sessions.Send(course, actor, id, "oversized response", provider, default))).StatusCode);
        Assert.Equal(413, (await Assert.ThrowsAsync<CaseChatException>(() => sessions.Send(course, actor, id, new string('字', 250), provider, default))).StatusCode);
        Assert.Equal(413, (await Assert.ThrowsAsync<CaseChatException>(() => sessions.Send(course, actor, id, new string('a', 301), provider, default))).StatusCode);
        provider.Respond = _ => Task.FromResult("next answer");
        var reply = await sessions.Send(course, actor, id, "next draft", provider, default);
        Assert.Equal(new[] { "kept", "answer 1", "next draft", "next answer" }, reply.Messages.Select(m => m.Content));
    }

    [Fact]
    public void BudgetReservesGenerationAndCountsUtf8AndRoleOverhead()
    {
        var options = new CaseChatOptions { MaxContextBytes = 5000 };
        var model = new OllamaOptions { AssessmentSampling = new() { NumCtx = 128, NumPredict = 64 } };
        CaseChatBudget.EnsureFits([new("user", "short")], options, model);
        Assert.Equal(413, Assert.Throws<CaseChatException>(() => CaseChatBudget.EnsureFits([new("user", new string('字', 30))], options, model)).StatusCode);
        Assert.Equal(413, Assert.Throws<CaseChatException>(() => CaseChatBudget.EnsureFits(Enumerable.Range(0, 20).Select(_ => new OllamaMessage("user", "")).ToArray(), options, model)).StatusCode);
    }

    private static RawChatSessions Sessions(CaseChatOptions? options = null, TimeProvider? time = null) => new(time ?? TimeProvider.System,
        Microsoft.Extensions.Options.Options.Create(options ?? new()), Microsoft.Extensions.Options.Options.Create(new OllamaOptions
        { Model = "synthetic", AssessmentSampling = new() { NumCtx = 200000, NumPredict = 4096 } }));

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        private readonly List<ManualTimer> timers = [];
        public override DateTimeOffset GetUtcNow() => now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, dueTime, period);
            timers.Add(timer); return timer;
        }
        public void Advance(TimeSpan amount)
        {
            now += amount;
            foreach (var timer in timers.ToArray()) timer.Tick();
        }
        private sealed class ManualTimer(ManualTime clock, TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) : ITimer
        {
            private DateTimeOffset next = clock.now + dueTime;
            private TimeSpan interval = period;
            private bool disposed;
            public void Tick()
            {
                if (!disposed && next <= clock.now)
                {
                    next = interval == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : clock.now + interval;
                    callback(state);
                }
            }
            public bool Change(TimeSpan due, TimeSpan repeat)
            { next = due == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : clock.now + due; interval = repeat; return !disposed; }
            public void Dispose() => disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    private sealed class Provider : IOllamaClient
    {
        public RequestMetrics Metrics=new(0,0,0,0,0,0,0,0,0,"stop");
        public List<IReadOnlyList<OllamaMessage>> Inputs { get; } = [];
        public Func<CancellationToken, Task<string>>? Respond;
        public async Task<(string Content, string? Thinking, RequestMetrics Metrics)> ConversationAsync(IReadOnlyList<OllamaMessage> messages, CancellationToken ct)
        { Inputs.Add(messages.ToArray()); return (Respond is null ? $"answer {Inputs.Count}" : await Respond(ct), "private thinking", Metrics); }
        public Task<(string Content, string? Thinking, RequestMetrics Metrics)> ChatAsync(IReadOnlyList<OllamaMessage> m, CancellationToken ct) => throw new NotSupportedException();
        public Task<(string Content, string? Thinking, RequestMetrics Metrics)> AssessAsync(IReadOnlyList<OllamaMessage> m, bool compare, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> SummarizeAsync(string existing, IReadOnlyList<ChatMessage> m, int max, CancellationToken ct) => throw new NotSupportedException();
        public Task<OllamaHealthResult> CheckHealthAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
