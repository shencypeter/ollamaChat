using System.Text;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;

namespace AiTeachingAssistant.Services;

public static class CaseChatBudget
{
    public static void EnsureFits(IReadOnlyList<OllamaMessage> messages, CaseChatOptions options, OllamaOptions model,int? predictionTokens=null)
    {
        // Byte safety and model context are different limits. The configured CJK
        // cost is a conservative estimate, not an exact server tokenizer count.
        // Never truncate or summarize the original buffer.
        var context = (long)model.AssessmentSampling.NumCtx - Math.Max(0, predictionTokens??model.AssessmentSampling.NumPredict);
        long bytes = 16,tokens=model.ContextTokenOverhead;
        foreach (var message in messages)
        {
            bytes += Encoding.UTF8.GetByteCount(message.Content) + Encoding.UTF8.GetByteCount(message.Role) + 16L;
            tokens+=Encoding.UTF8.GetByteCount(message.Role)+16L;
            foreach(var rune in message.Content.EnumerateRunes())
                tokens+=rune.Value is >=0x4E00 and <=0x9FFF?model.CjkContextTokensPerCharacter:rune.Utf8SequenceLength;
        }
        if (context <= 0 || bytes > options.MaxContextBytes || tokens > context)
            throw new CaseChatException(413, "完整輸入超過目前設定的容量預算，尚未送至模型。原有內容不會截斷或摘要。",new("context_limit",$"應用程式預檢：UTF-8 {bytes}/{options.MaxContextBytes} bytes；估計輸入 {tokens}/{context} tokens（含傳輸預留 {model.ContextTokenOverhead}），模型 context={model.AssessmentSampling.NumCtx}，輸出預留={Math.Max(0,predictionTokens??model.AssessmentSampling.NumPredict)}。尚未呼叫模型，並非模型拒答。"));
    }
}
