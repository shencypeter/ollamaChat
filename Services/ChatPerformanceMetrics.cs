using AiTeachingAssistant.Models;

namespace AiTeachingAssistant.Services;

// Only numeric performance data and a known completion code cross the boundary.
// No prompt, response, thinking or upstream error body belongs in this metadata.
public static class ChatPerformanceMetrics
{
    private static readonly HashSet<string> Fields=["totalDurationNanoseconds","loadDurationNanoseconds","promptEvalDurationNanoseconds","evalDurationNanoseconds","promptTokens","cachedPromptTokens","responseTokens","tokensPerSecond","roundTripMilliseconds"];
    public static RequestMetrics Clean(RequestMetrics metrics)=>new(
        Math.Max(0,metrics.TotalDurationNanoseconds),Math.Max(0,metrics.LoadDurationNanoseconds),
        Math.Max(0,metrics.PromptEvalDurationNanoseconds),Math.Max(0,metrics.EvalDurationNanoseconds),
        Math.Max(0,metrics.PromptTokens),Math.Max(0,metrics.CachedPromptTokens),Math.Max(0,metrics.ResponseTokens),
        double.IsFinite(metrics.TokensPerSecond)?Math.Max(0,metrics.TokensPerSecond):0,
        Math.Max(0,metrics.RoundTripMilliseconds),
        metrics.DoneReason is "stop" or "length" or "load" or "unload"?metrics.DoneReason:"unknown")
        {AvailableFields=metrics.AvailableFields?.Where(Fields.Contains).Distinct().ToArray()};
}
