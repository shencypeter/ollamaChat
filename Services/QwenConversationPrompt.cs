using AiTeachingAssistant.Models;
namespace AiTeachingAssistant.Services;

// Model protocol only. Raw passes user/assistant messages as-is and gains no
// implicit application system prompt or memory. Stage supplies its own system.
public static class QwenConversationPrompt
{
    public static string Render(IReadOnlyList<OllamaMessage> messages)
    {
        if(messages.Any(m=>m.Role is not ("system" or "user" or "assistant")))throw new ArgumentException("Unsupported conversation role.");
        return string.Concat(messages.Select(m=>"<|im_start|>"+m.Role+"\n"+m.Content+"<|im_end|>\n"))+"<|im_start|>assistant\n";
    }
}
