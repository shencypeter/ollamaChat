using System.Text.Json;
using AiTeachingAssistant.Models;
namespace AiTeachingAssistant.Services;

// A bounded transport for clause-dense answers. The model still chooses every
// code (or no codes); source locations are immutable, server-owned references.
internal sealed class CaseAnnotationSlots
{
    internal sealed record Reference(string Text,int Start,int End);
    private readonly Dictionary<string,Reference> slots=new();
    public object Input=>slots.ToDictionary(x=>x.Key,x=>new{text=x.Value.Text});
    public const string Instructions="若提供annotationSlots，annotations須是以s000等key為欄位的物件，逐一依完整answerText脈絡判讀所有欄位至末段。每欄只回codes、explanation、status，不回text或occurrence；沒有充分分類依據時codes=[]。不可跳欄、重複欄位或發明代碼；explanation簡短。分類仍由你判斷，不強迫每句或每碼出現。";
    private CaseAnnotationSlots(string source,IReadOnlyList<string> clauses)
    {
        var cursor=0;
        for(var i=0;i<clauses.Count;i++){
            var start=source.IndexOf(clauses[i],cursor,StringComparison.Ordinal);
            if(start<0)throw new InvalidOperationException("Invalid source clause reference.");
            cursor=start+clauses[i].Length;slots.Add($"s{i:D3}",new(clauses[i],start,cursor));
        }
    }
    public static CaseAnnotationSlots? For(string source,bool compact)
    {
        var clauses=CaseClauseEvidence.Clauses(source,compact?160:16000);
        return clauses.Count>=32?new(source,clauses):null;
    }
    public JsonElement Schema(bool compact,bool annotationsFirst,IReadOnlyList<CaseClassificationRuleDefinition>? definitions)
    {
        var item=new{type="object",additionalProperties=false,required=new[]{"codes","explanation","status"},properties=new{
            codes=new{type="array",minItems=0,maxItems=8,uniqueItems=true,items=new{type="string",@enum=definitions is null?CaseCodingCatalog.All.Select(c=>c.Code).ToArray():CaseClassificationRules.Enabled(definitions).Select(c=>c.Code).ToArray()}},
            explanation=new{type="string",maxLength=compact?80:200},status=new{type="string",@enum=new[]{"unverified","unsupported","misplaced"}}
        }};
        var annotations=new{type="object",additionalProperties=false,required=slots.Keys.ToArray(),properties=slots.Keys.ToDictionary(key=>key,_=>item)};
        var feedback=new{type="string",minLength=1,maxLength=compact?600:12000};
        var properties=new Dictionary<string,object>();
        if(annotationsFirst){properties.Add("annotations",annotations);properties.Add("feedback",feedback);}else{properties.Add("feedback",feedback);properties.Add("annotations",annotations);}
        return JsonSerializer.SerializeToElement(new{type="object",additionalProperties=false,required=new[]{"feedback","annotations"},properties});
    }
    public string Normalize(string content)
    {
        try{
            if(content.Length>128000)throw Invalid();
            using var doc=JsonDocument.Parse(content,new JsonDocumentOptions{MaxDepth=16});var root=doc.RootElement;
            if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("feedback",out var feedback)||feedback.ValueKind!=JsonValueKind.String
                ||!root.TryGetProperty("annotations",out var annotations)||annotations.ValueKind!=JsonValueKind.Object)throw Invalid();
            var entries=annotations.EnumerateObject().ToArray();
            if(entries.Length!=slots.Count||entries.Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()!=slots.Count||entries.Any(x=>!slots.ContainsKey(x.Name)))throw Invalid();
            var normalized=new List<object>();
            foreach(var slot in slots){
                var value=annotations.GetProperty(slot.Key);
                if(value.ValueKind!=JsonValueKind.Object||!value.TryGetProperty("codes",out var codes)||codes.ValueKind!=JsonValueKind.Array
                    ||codes.GetArrayLength()>8||codes.EnumerateArray().Any(c=>c.ValueKind!=JsonValueKind.String)
                    ||!value.TryGetProperty("explanation",out var explanation)||explanation.ValueKind!=JsonValueKind.String
                    ||!value.TryGetProperty("status",out var status)||status.ValueKind!=JsonValueKind.String)throw Invalid();
                if(codes.GetArrayLength()==0)continue;
                normalized.Add(new{start=slot.Value.Start,end=slot.Value.End,text=slot.Value.Text,codes,explanation,status});
            }
            return JsonSerializer.Serialize(new{feedback,annotations=normalized});
        }catch(JsonException){throw Invalid();}
    }
    private static CaseChatException Invalid()=>new(502,"模型未完整回填本次分句分類，原作答已保留，請重試。",new("annotation_slots","固定分句欄位缺漏、重複或格式不符，系統未接受部分回饋；不是確認模型拒答。"));
}
