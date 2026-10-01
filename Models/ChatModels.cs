using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiTeachingAssistant.Models;

public sealed record ChatMessage(string Role, string Content, DateTimeOffset Timestamp, string? Thinking = null);

public sealed class SendMessageRequest
{
    [Required, StringLength(8000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;

    [StringLength(8000)]
    public string? BeforeMessage { get; set; }

    public bool Compare { get; set; }

    [Required]
    public string Stage { get; set; } = string.Empty;
}

public sealed class SetAnalysisStageRequest
{
    [Required]
    public string Stage { get; set; } = string.Empty;
}

public sealed class SetMemoryStrategyRequest
{
    [Required, RegularExpression("^(Window|Buffer|None|Summary)$")]
    public string Strategy { get; set; } = string.Empty;
}

public sealed record ChatPageViewModel(
    IReadOnlyList<ChatMessage> Messages,
    string Model,
    string Server,
    string MemoryStrategy,
    int MemoryWindow,
    AnalysisStage ActiveStage);

public sealed record ChatReply(
    ChatMessage Message,
    string RenderedContent,
    RequestMetrics Metrics,
    string MemoryStrategy,
    int ContextMessages,
    MemoryDiagnostics Memory);

public sealed record MemoryContext(
    IReadOnlyList<OllamaMessage> Messages,
    MemoryDiagnostics Diagnostics);

public sealed record MemoryDiagnostics(
    string Strategy,
    int RecentMessagesSent,
    int RecentTurnsSent,
    bool SummaryPresent,
    int SummaryCharacters,
    bool SummaryUpdated);

public sealed record SummaryMemoryState(
    string Summary,
    IReadOnlyList<ChatMessage> RecentMessages);

public sealed record RequestMetrics(
    long TotalDurationNanoseconds,
    long LoadDurationNanoseconds,
    long PromptEvalDurationNanoseconds,
    long EvalDurationNanoseconds,
    int PromptTokens,
    int CachedPromptTokens,
    int ResponseTokens,
    double TokensPerSecond,
    long RoundTripMilliseconds,
    string DoneReason);

public sealed record OllamaHealthResult(bool Online, string Message, IReadOnlyList<string> Models, long LatencyMilliseconds);

internal sealed class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("messages")]
    public IReadOnlyList<OllamaMessage> Messages { get; init; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }
}

public sealed record OllamaMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("thinking"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Thinking = null);

internal sealed class OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; init; }

    [JsonPropertyName("done_reason")]
    public string DoneReason { get; init; } = string.Empty;

    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; init; }

    [JsonPropertyName("load_duration")]
    public long LoadDuration { get; init; }

    [JsonPropertyName("prompt_eval_count")]
    public int PromptEvalCount { get; init; }

    [JsonPropertyName("prompt_eval_cached_count")]
    public int PromptEvalCachedCount { get; init; }

    [JsonPropertyName("prompt_eval_duration")]
    public long PromptEvalDuration { get; init; }

    [JsonPropertyName("eval_count")]
    public int EvalCount { get; init; }

    [JsonPropertyName("eval_duration")]
    public long EvalDuration { get; init; }
}

internal sealed class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public IReadOnlyList<OllamaTagModel> Models { get; init; } = [];
}

internal sealed class OllamaTagModel
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
