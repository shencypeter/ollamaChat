using System.Text;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;

namespace AiTeachingAssistant.Services;

public static class CaseChatBudget
{
    public static void EnsureFits(IReadOnlyList<OllamaMessage> messages, CaseChatOptions options, OllamaOptions model,int? predictionTokens=null)
    {
        // UTF-8 bytes plus message/template overhead is a conservative estimate,
        // not a tokenizer guarantee. Never truncate or summarize the original buffer.
        var context = (long)model.AssessmentSampling.NumCtx - Math.Max(0, predictionTokens??model.AssessmentSampling.NumPredict);
        var budget = Math.Min(options.MaxContextBytes, context);
        long bytes = 16;
        foreach (var message in messages)
            bytes += Encoding.UTF8.GetByteCount(message.Content) + Encoding.UTF8.GetByteCount(message.Role) + 16L;
        if (budget <= 0 || bytes > budget)
            throw new CaseChatException(413, "完整對話已超過模型脈絡上限；請開始新對話。原有內容不會截斷或摘要。");
    }
}
