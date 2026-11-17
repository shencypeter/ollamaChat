using System.Text.Json;
using AiTeachingAssistant.Models;

namespace AiTeachingAssistant.Services;

/// <summary>Classification proposals refer to an immutable original, never to rewritten text.</summary>
public static class CaseAnnotationContract
{
    public const int MaximumAnnotations=48;
    public static JsonElement AnnotationSchema=>AnnotationSchemaFor(false);
    private static JsonElement AnnotationSchemaFor(bool compact)=>JsonSerializer.SerializeToElement(new {
        type="object",additionalProperties=false,required=new[]{"occurrence","text","codes","explanation","status"},properties=new {
            occurrence=new{type="integer",minimum=0,maximum=15999},text=new{type="string",minLength=1,maxLength=compact?160:16000},
            codes=new{type="array",minItems=1,maxItems=8,uniqueItems=true,items=new{type="string",@enum=CaseCodingCatalog.All.Select(c=>c.Code).ToArray()}},
            explanation=new{type="string",maxLength=compact?80:200},status=new{type="string",@enum=new[]{"unverified","unsupported","misplaced"}}
        }
    });
    public static JsonElement Schema=>SchemaFor(false);
    public static JsonElement SchemaFor(bool compact,bool annotationsFirst=false)
    {
        if(annotationsFirst)
        {
            var standard=SchemaFor(compact).GetProperty("properties");
            // Let the model select evidence/code spans before composing feedback.
            // Field names and validation stay compatible with existing responses.
            return JsonSerializer.SerializeToElement(new{type="object",additionalProperties=false,required=new[]{"annotations","feedback"},properties=new{annotations=standard.GetProperty("annotations"),feedback=standard.GetProperty("feedback")}});
        }
        return JsonSerializer.SerializeToElement(new {
        type="object",additionalProperties=false,required=new[]{"feedback","annotations"},properties=new {
            feedback=new{type="string",minLength=1,maxLength=compact?600:12000},
            annotations=new{type="array",maxItems=compact?8:MaximumAnnotations,items=AnnotationSchemaFor(compact)}
        }
        });
    }
    public static void EnsureComplete(string? doneReason)
    {
        if(string.Equals(doneReason,"length",StringComparison.OrdinalIgnoreCase))
            throw new CaseChatException(502,"模型回覆達到輸出上限而未完成，原作答已保留，請重試回覆。",new("output_limit","Ollama 回報 done_reason=length；生成碰到輸出上限，系統未接受不完整的正式回饋。"));
    }
    public sealed record FeedbackResult(string Feedback,IReadOnlyList<CaseTextAnnotation> Annotations);
    public static FeedbackResult Parse(string body,string original)
    {
        if(body.Length>128000)throw Invalid();
        try
        {
            using var doc=JsonDocument.Parse(body,new JsonDocumentOptions{MaxDepth=16});var root=doc.RootElement;
            if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("feedback",out var feedback)||feedback.ValueKind!=JsonValueKind.String
                ||string.IsNullOrWhiteSpace(feedback.GetString())||feedback.GetString()!.Length>12000
                ||!root.TryGetProperty("annotations",out var annotations)||annotations.ValueKind!=JsonValueKind.Array)throw Invalid();
            return new(feedback.GetString()!,Validate(original,annotations));
        }
        catch(JsonException){throw new CaseChatException(502,"AI 回饋格式不完整，原作答已保留，請重試。",new("feedback_json","模型已回傳文字，但文字不是合法的回饋 JSON；屬系統格式驗證失敗，未確認模型拒答。"));}
    }
    private static CaseChatException Invalid()=>new(502,"AI 回饋格式不完整，原作答已保留，請重試。",new("feedback_format","回饋超過安全長度、缺少 feedback／annotations 必要欄位、欄位型別不符，或 feedback 為空／過長；系統未接受這份回饋。"));

    public static IReadOnlyList<CaseTextAnnotation> Validate(string original,JsonElement entries)
    {
        var result=new List<CaseTextAnnotation>();
        if(entries.ValueKind!=JsonValueKind.Array)return result;
        // Inspect a bounded collection even if a provider ignores the schema.
        foreach(var entry in entries.EnumerateArray().Take(MaximumAnnotations))
        {
            if(entry.ValueKind!=JsonValueKind.Object||!TryRange(original,entry,out var start,out var end)
                ||!entry.TryGetProperty("codes",out var rawCodes)||rawCodes.ValueKind!=JsonValueKind.Array)continue;
            var codes=rawCodes.EnumerateArray().Take(8).Where(c=>c.ValueKind==JsonValueKind.String).Select(c=>c.GetString()!)
                .Where(CaseCodingCatalog.IsValid).Distinct(StringComparer.Ordinal).ToArray();
            if(codes.Length==0)continue;
            var status=String(entry,"status");if(status is not ("unsupported" or "misplaced"))status="unverified";
            var explanation=String(entry,"explanation");
            if(explanation.Length>200){var length=200;if(SplitsPair(explanation,length))length--;explanation=explanation[..length];}
            result.Add(new("ann-"+(result.Count+1),start,end,original[start..end],codes,explanation,status));
        }
        return result;
    }
    private static bool TryRange(string original,JsonElement entry,out int start,out int end)
    {
        start=end=-1;
        if(!entry.TryGetProperty("text",out var value)||value.ValueKind!=JsonValueKind.String||value.GetString() is not {Length:>0} text)return false;
        if(entry.TryGetProperty("occurrence",out var occurrence))
        {
            // Explicit alternative representation, never repair a bad numeric offset.
            // Require the model to select the exact occurrence, including repeated quotes.
            if(entry.TryGetProperty("start",out _)||entry.TryGetProperty("end",out _)||occurrence.ValueKind!=JsonValueKind.Number||!occurrence.TryGetInt32(out var index)||index<0||index>=original.Length)return false;
            var search=0;
            for(var n=0;n<=index;n++)
            {
                start=original.IndexOf(text,search,StringComparison.Ordinal);
                if(start<0)return false;
                search=start+1;
            }
            end=start+text.Length;
        }
        else if(!entry.TryGetProperty("start",out var first)||first.ValueKind!=JsonValueKind.Number||!first.TryGetInt32(out start)
            ||!entry.TryGetProperty("end",out var last)||last.ValueKind!=JsonValueKind.Number||!last.TryGetInt32(out end))return false;
        return start>=0&&end>start&&end<=original.Length&&!SplitsPair(original,start)&&!SplitsPair(original,end)&&original[start..end]==text;
    }
    private static string String(JsonElement entry,string key)=>entry.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()!:"";
    private static bool SplitsPair(string value,int index)=>index>0&&index<value.Length&&char.IsHighSurrogate(value[index-1])&&char.IsLowSurrogate(value[index]);
}
