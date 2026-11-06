using System.Security.Claims;
using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using Microsoft.Extensions.Options;

namespace AiTeachingAssistant.Services;

public interface IChatSessionStore
{
    IReadOnlyList<ChatMessage> GetMessages(AnalysisStage stage);
    void SaveMessages(AnalysisStage stage, IEnumerable<ChatMessage> messages);
    AnalysisStage GetActiveStage();
    void SetActiveStage(AnalysisStage stage);
    string GetMemoryStrategy();
    void SetMemoryStrategy(string strategy);
    SummaryMemoryState? GetSummaryMemory(AnalysisStage stage);
    void SaveSummaryMemory(AnalysisStage stage, SummaryMemoryState state);
    void ClearSummaryMemory(AnalysisStage stage);
    void Clear(AnalysisStage stage);
}

public sealed class ChatSessionStore(IHttpContextAccessor httpContextAccessor, IOptions<MemoryOptions> options,LearnerExperience? experience=null) : IChatSessionStore
{
    private const string HistoryPrefix = "qwen-chat-history-v2:";
    private const string LegacyHistoryKey = "qwen-chat-history-v1";
    private const string ActiveStageKey = "qwen-active-analysis-stage-v1";
    private const string StrategyKey = "qwen-memory-strategy-v1";
    private const string SummaryPrefix = "qwen-summary-memory-v2:";
    private const string LegacySummaryKey = "qwen-summary-memory-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MemoryOptions _options = options.Value;

    private string UserScope => "portal:" + (httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous") + (experience?.IsPractice==true?":practice:":":");

    private ISession Session => httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("No active HTTP session is available.");

    public IReadOnlyList<ChatMessage> GetMessages(AnalysisStage stage)
    {
        var key = StageKey(HistoryPrefix, stage);
        var json = Session.GetString(key);
        if (string.IsNullOrWhiteSpace(json) && stage == AnalysisStage.Premorbid)
        {
            json = Session.GetString(UserScope + LegacyHistoryKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                Session.SetString(key, json);
                Session.Remove(UserScope + LegacyHistoryKey);
            }
        }
        return string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<ChatMessage>>(json, JsonOptions) ?? [];
    }

    public void SaveMessages(AnalysisStage stage, IEnumerable<ChatMessage> messages)
    {
        var bounded = messages.TakeLast(_options.MaxStoredMessages).ToList();
        Session.SetString(StageKey(HistoryPrefix, stage), JsonSerializer.Serialize(bounded, JsonOptions));
    }

    public AnalysisStage GetActiveStage()
    {
        var key = Session.GetString(UserScope + ActiveStageKey);
        return AnalysisStageCatalog.TryParse(key, out var stage)
            ? stage
            : AnalysisStage.Premorbid;
    }

    public void SetActiveStage(AnalysisStage stage) =>
        Session.SetString(UserScope + ActiveStageKey, AnalysisStageCatalog.Get(stage).Key);

    public string GetMemoryStrategy() => Session.GetString(UserScope + StrategyKey) ?? _options.Strategy;

    public void SetMemoryStrategy(string strategy)
    {
        Session.SetString(UserScope + StrategyKey, strategy);
        foreach (var option in AnalysisStageCatalog.All)
            ClearSummaryMemory(option.Stage);
    }

    public SummaryMemoryState? GetSummaryMemory(AnalysisStage stage)
    {
        var key = StageKey(SummaryPrefix, stage);
        var json = Session.GetString(key);
        if (string.IsNullOrWhiteSpace(json) && stage == AnalysisStage.Premorbid)
        {
            json = Session.GetString(UserScope + LegacySummaryKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                Session.SetString(key, json);
                Session.Remove(UserScope + LegacySummaryKey);
            }
        }
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<SummaryMemoryState>(json, JsonOptions);
    }

    public void SaveSummaryMemory(AnalysisStage stage, SummaryMemoryState state) =>
        Session.SetString(StageKey(SummaryPrefix, stage), JsonSerializer.Serialize(state, JsonOptions));

    public void ClearSummaryMemory(AnalysisStage stage) =>
        Session.Remove(StageKey(SummaryPrefix, stage));

    public void Clear(AnalysisStage stage)
    {
        Session.Remove(StageKey(HistoryPrefix, stage));
        ClearSummaryMemory(stage);
    }

    private string StageKey(string prefix, AnalysisStage stage) =>
        UserScope + prefix + AnalysisStageCatalog.Get(stage).Key;
}
