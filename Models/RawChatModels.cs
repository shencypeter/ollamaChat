namespace AiTeachingAssistant.Models;

public sealed record RawChatMessage(string Role, string Content,RequestMetrics? Metrics=null);
public sealed record RawChatReply(Guid SessionId, IReadOnlyList<RawChatMessage> Messages, string ModelName);
