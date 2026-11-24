namespace AiTeachingAssistant.Models;

public sealed record RawChatMessage(string Role, string Content,RequestMetrics? Metrics=null,string? ModelName=null);
public sealed record RawChatReply(Guid SessionId, IReadOnlyList<RawChatMessage> Messages, string ModelName);
