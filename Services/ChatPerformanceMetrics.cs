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
        {AvailableFields=metrics.AvailableFields?.Where(Fields.Contains).Distinct().ToArray(),ProviderCalls=metrics.ProviderCalls is >0?Math.Min(16,metrics.ProviderCalls.Value):null};

    public static RequestMetrics Combine(RequestMetrics first,RequestMetrics second)
    {
        first=Clean(first);second=Clean(second);
        var available=(first.AvailableFields??[]).Intersect(second.AvailableFields??[]).ToHashSet();available.Remove("tokensPerSecond");
        long Sum(string key,long a,long b)=>!available.Contains(key)?0:a>long.MaxValue-b?long.MaxValue:a+b;
        int Count(string key,int a,int b)=>(int)Math.Min(int.MaxValue,Sum(key,a,b));
        var eval=Sum("evalDurationNanoseconds",first.EvalDurationNanoseconds,second.EvalDurationNanoseconds);var responses=Count("responseTokens",first.ResponseTokens,second.ResponseTokens);
        if(available.Contains("responseTokens")&&available.Contains("evalDurationNanoseconds")&&eval>0)available.Add("tokensPerSecond");
        return new(Sum("totalDurationNanoseconds",first.TotalDurationNanoseconds,second.TotalDurationNanoseconds),Sum("loadDurationNanoseconds",first.LoadDurationNanoseconds,second.LoadDurationNanoseconds),Sum("promptEvalDurationNanoseconds",first.PromptEvalDurationNanoseconds,second.PromptEvalDurationNanoseconds),eval,Count("promptTokens",first.PromptTokens,second.PromptTokens),Count("cachedPromptTokens",first.CachedPromptTokens,second.CachedPromptTokens),responses,eval>0?Math.Round(responses/(eval/1_000_000_000d),2):0,Sum("roundTripMilliseconds",first.RoundTripMilliseconds,second.RoundTripMilliseconds),second.DoneReason)
            {AvailableFields=available.ToArray(),ProviderCalls=Math.Min(16,(first.ProviderCalls??1)+(second.ProviderCalls??1))};
    }
}
