using AiTeachingAssistant.Models;
namespace AiTeachingAssistant.Services;

// Model protocol only: serialize supplied system/user/assistant roles as-is.
// Raw supplies a language-only instruction; Stage supplies its teaching system.
// This transport adds no implicit prompt or memory.
public static class QwenConversationPrompt
{
    public static string Render(IReadOnlyList<OllamaMessage> messages)
    {
        if(messages.Any(m=>m.Role is not ("system" or "user" or "assistant")))throw new ArgumentException("Unsupported conversation role.");
        return string.Concat(messages.Select(m=>"<|im_start|>"+m.Role+"\n"+m.Content+"<|im_end|>\n"))+"<|im_start|>assistant\n";
    }
}
