using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using LangChain.Memory;
using LangChain.Providers;
using Microsoft.Extensions.Options;

namespace AiTeachingAssistant.Services;

public interface IMemoryOrchestrator
{
    Task<MemoryContext> BuildContextAsync(
        IReadOnlyList<ChatMessage> history,
        string currentMessage,
        AnalysisStage stage,
        CancellationToken cancellationToken = default);
    Task<bool> CommitTurnAsync(
        AnalysisStage stage,
        ChatMessage userMessage,
        ChatMessage assistantMessage,
        CancellationToken cancellationToken = default);
    string Strategy { get; }
}

public sealed class LangChainMemoryOrchestrator(
    IOptions<MemoryOptions> options,
    IChatSessionStore sessionStore,
    IOllamaClient ollamaClient,
    ILogger<LangChainMemoryOrchestrator> logger) : IMemoryOrchestrator
{
    private readonly MemoryOptions _options = options.Value;
    public string Strategy => sessionStore.GetMemoryStrategy();

    public async Task<MemoryContext> BuildContextAsync(
        IReadOnlyList<ChatMessage> history,
        string currentMessage,
        AnalysisStage stage,
        CancellationToken cancellationToken = default)
    {
        var strategy = Strategy;
        if (stage != AnalysisStage.GeneralChat)
        {
            return CreateContext(
                [StageInstruction(stage), new OllamaMessage("user", currentMessage)],
                "None", 0, false, 0, false);
        }

        if (strategy.Equals("Summary", StringComparison.OrdinalIgnoreCase))
            return await BuildSummaryContextAsync(history, currentMessage, stage, cancellationToken);

        var current = new[] { Message.Human(currentMessage) };
        if (strategy.Equals("None", StringComparison.OrdinalIgnoreCase) || history.Count == 0)
        {
            return CreateContext(
                [.. current.Select(ToOllama)],
                strategy, 0, false, 0, false);
        }

        var langChainMessages = history.Select(ToLangChain).ToList();
        var langChainHistory = new ChatMessageHistory();
        await langChainHistory.AddMessages(langChainMessages).WaitAsync(cancellationToken);

        BaseChatMemory memory = strategy.Equals("Buffer", StringComparison.OrdinalIgnoreCase)
            ? new ConversationBufferMemory(langChainHistory)
            : new ConversationWindowBufferMemory(langChainHistory) { WindowSize = _options.WindowSize };

        var selected = AttachRolePreservingHistory(memory, current);
        return CreateContext(
            [.. selected.Select(ToOllama)],
            strategy,
            selected.Count - current.Length,
            false,
            0,
            false);
    }

    public async Task<bool> CommitTurnAsync(
        AnalysisStage stage,
        ChatMessage userMessage,
        ChatMessage assistantMessage,
        CancellationToken cancellationToken = default)
    {
        if (stage != AnalysisStage.GeneralChat ||
            !Strategy.Equals("Summary", StringComparison.OrdinalIgnoreCase))
            return false;

        var state = sessionStore.GetSummaryMemory(stage) ?? new SummaryMemoryState(string.Empty, []);
        var recent = state.RecentMessages.Append(userMessage).Append(assistantMessage).ToList();
        var pendingState = state with { RecentMessages = recent };

        // Persist first so a failed summary call never loses a completed conversation turn.
        sessionStore.SaveSummaryMemory(stage, pendingState);

        var maxRecentMessages = _options.SummaryRecentTurns * 2;
        var overflow = recent.Count - maxRecentMessages;
        if (overflow <= 0)
            return false;

        var agedOut = recent.Take(overflow).ToList();
        try
        {
            var summary = await ollamaClient.SummarizeAsync(
                state.Summary,
                agedOut,
                _options.SummaryMaxCharacters,
                cancellationToken);
            sessionStore.SaveSummaryMemory(stage, new SummaryMemoryState(summary, recent.Skip(overflow).ToList()));
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Could not update running conversation summary; unsummarized turns were retained.");
            return false;
        }
    }

    private async Task<MemoryContext> BuildSummaryContextAsync(
        IReadOnlyList<ChatMessage> history,
        string currentMessage,
        AnalysisStage stage,
        CancellationToken cancellationToken)
    {
        var state = sessionStore.GetSummaryMemory(stage);
        var summaryUpdated = false;
        if (state is null)
        {
            var recentMessageLimit = _options.SummaryRecentTurns * 2;
            var olderCount = Math.Max(0, history.Count - recentMessageLimit);
            var olderMessages = history.Take(olderCount).ToList();
            var recentMessages = history.Skip(olderCount).ToList();
            var summary = olderMessages.Count == 0
                ? string.Empty
                : await ollamaClient.SummarizeAsync(
                    string.Empty,
                    olderMessages,
                    _options.SummaryMaxCharacters,
                    cancellationToken);
            summaryUpdated = olderMessages.Count > 0;
            state = new SummaryMemoryState(summary, recentMessages);
            sessionStore.SaveSummaryMemory(stage, state);
        }

        var messages = new List<OllamaMessage>();
        if (!string.IsNullOrWhiteSpace(state.Summary))
        {
            messages.Add(new OllamaMessage(
                "system",
                $"Summary of earlier conversation:\n{state.Summary}"));
        }

        messages.AddRange(state.RecentMessages.Select(message => ToOllama(ToLangChain(message))));
        messages.Add(new OllamaMessage("user", currentMessage));

        return CreateContext(
            messages,
            "Summary",
            state.RecentMessages.Count,
            !string.IsNullOrWhiteSpace(state.Summary),
            state.Summary.Length,
            summaryUpdated);
    }

    private static MemoryContext CreateContext(
        IReadOnlyList<OllamaMessage> messages,
        string strategy,
        int recentMessages,
        bool summaryPresent,
        int summaryCharacters,
        bool summaryUpdated) => new(
            messages,
            new MemoryDiagnostics(
                strategy,
                recentMessages,
                recentMessages / 2,
                summaryPresent,
                summaryCharacters,
                summaryUpdated));

    private static IReadOnlyList<Message> AttachRolePreservingHistory(
        BaseChatMemory memory,
        IReadOnlyCollection<Message> currentMessages)
    {
        // Do not use LangChain.NET 0.17.0's incompatible WithHistory() helper.
        IEnumerable<Message> selectedHistory = memory switch
        {
            ConversationWindowBufferMemory window => window.ChatHistory.Messages
                .TakeLast(window.WindowSize * 2),
            _ => memory.ChatHistory.Messages
        };

        return selectedHistory.Concat(currentMessages).ToList();
    }

    private static Message ToLangChain(ChatMessage message) => message.Role == "user"
        ? Message.Human(message.Content)
        : Message.Ai(message.Content);

    private static OllamaMessage ToOllama(Message message) => new(
        message.Role == MessageRole.Ai ? "assistant" : message.Role == MessageRole.System ? "system" : "user",
        message.Content);

    private static OllamaMessage StageInstruction(AnalysisStage stage)
        => new("system", AnalysisStageInstructions.Get(stage));
}
