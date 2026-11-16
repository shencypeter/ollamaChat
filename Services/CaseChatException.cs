namespace AiTeachingAssistant.Services;

public sealed class CaseChatException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
