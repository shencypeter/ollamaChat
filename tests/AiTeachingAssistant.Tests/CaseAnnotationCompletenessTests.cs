using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
namespace AiTeachingAssistant.Tests;

public sealed class CaseAnnotationCompletenessTests
{
    private static readonly string[] Clauses=Enumerable.Range(0,128).Select(i=>$"合成片段{i:D3}。").ToArray();
    private static readonly string Original=string.Concat(Clauses);
    private static object Annotation(string text,int occurrence=0,string code="C1",string status="unverified")=>new{text,occurrence,codes=new[]{code},explanation="合成分類",status};
    private static object Complete(int count)=>new{feedback="依本次原文分類，案例事實仍待核實。",annotations=Clauses.Take(count).Select((s,i)=>Annotation(s,code:i==count-1?"D3":"C1")).ToArray()};
    private static object Loop()=>new{feedback="機械重複回覆",annotations=Enumerable.Repeat(Annotation(Clauses[0]),24).ToArray()};
    [Theory][InlineData(49)][InlineData(66)][InlineData(128)]
    public void AcceptsUniqueEvidenceBeyondOldLimitAndRetainsLastClause(int count)
    {
        Assert.Equal(128,CaseAnnotationContract.Schema.GetProperty("properties").GetProperty("annotations").GetProperty("maxItems").GetInt32());
        var result=CaseAnnotationContract.Parse(JsonSerializer.Serialize(Complete(count)),Original);
        Assert.Equal(count,result.Annotations.Count);Assert.Equal(Clauses[count-1],result.Annotations[^1].Text);
    }
    [Fact] public void MechanicalRepeatedOccurrenceIsRejectedWithoutExposingAnswer()
    {
        var failure=Assert.Throws<CaseChatException>(()=>CaseAnnotationContract.Parse(JsonSerializer.Serialize(Loop()),Original));
        Assert.Equal("annotation_repetition",failure.Diagnostic!.Code);Assert.DoesNotContain(Clauses[0],failure.ToString());
    }
    [Fact] public void RepeatedTextAtDistinctOccurrencesAndDifferentStatusesRemainValid()
    {
        var body=JsonSerializer.Serialize(new{feedback="待核實",annotations=Enumerable.Range(0,10).Select(i=>Annotation("合成",i)).Append(Annotation("合成",0,status:"unsupported")).ToArray()});
        var result=CaseAnnotationContract.Parse(body,string.Concat(Enumerable.Repeat("合成。",10)));
        Assert.Equal(11,result.Annotations.Count);Assert.Equal("unsupported",result.Annotations[^1].Status);
    }
    [Fact] public async Task OneBoundedRetryReclassifiesWholeCurrentAnswerWithoutBrokenResponseOrHistory()
    {
        var original=string.Concat(Clauses.Take(24));
        var wire=new QueueWire(Envelope(Loop()),Envelope(Complete(24)));
        var result=await Client(wire).CaseFeedbackAsync([new("system","本次規則"),new("user","OLD_DRAFT"),new("assistant","OLD_REPLY"),new("user",original)],AnalysisStage.Premorbid,default);
        Assert.Equal(2,wire.Requests.Count);Assert.Equal(2,result.Metrics.ProviderCalls);Assert.Equal(20,result.Metrics.PromptTokens);
        Assert.All(wire.Requests,r=>{
            Assert.DoesNotContain("OLD_DRAFT",r);Assert.DoesNotContain("OLD_REPLY",r);Assert.DoesNotContain("機械重複回覆",r);
            using var doc=JsonDocument.Parse(r);var messages=doc.RootElement.GetProperty("messages");Assert.Equal(2,messages.GetArrayLength());
            Assert.Equal("system",messages[0].GetProperty("role").GetString());
            using var user=JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);Assert.Equal(original,user.RootElement.GetProperty("answerText").GetString());
        });
        var parsed=CaseAnnotationContract.Parse(result.Content,original);Assert.Equal(24,parsed.Annotations.Count);Assert.Contains("D3",parsed.Annotations[^1].Codes);
    }
    [Fact] public async Task ASecondMechanicalLoopFailsWithoutThirdCall()
    {
        var wire=new QueueWire(Envelope(Loop()),Envelope(Loop()));
        var error=await Assert.ThrowsAsync<CaseChatException>(()=>Client(wire).CaseFeedbackAsync([new("system","規則"),new("user",string.Concat(Clauses.Take(24)))],AnalysisStage.Premorbid,default));
        Assert.Equal("annotation_repetition",error.Diagnostic!.Code);Assert.Equal(2,wire.Requests.Count);
    }
    [Theory][InlineData("llama3")][InlineData("breeze")][InlineData("qwen")][InlineData("qwen25-omni")]
    public async Task LongAnswerUsesOneFiniteSlotPerClauseIncludingRepeatedQuotesAndTail(string profile)
    {
        var clauses=Clauses.Take(66).Concat(new[]{Clauses[0]}).ToArray();var original=string.Concat(clauses);
        var slots=clauses.Select((text,i)=>(Key:$"s{i:D3}",Value:new{codes=i==65?new[]{"D3"}:i is 0 or 66?new[]{"C1"}:Array.Empty<string>(),explanation="合成參考",status="unverified"})).ToDictionary(x=>x.Key,x=>x.Value);
        var wire=new QueueWire(Envelope(new{feedback="分類仍需人工判斷",annotations=slots}));
        var settings=new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory,"Configuration","Ollama",profile+".json")).Build().GetSection("Ollama").Get<OllamaOptions>()!;
        var client=new OllamaClient(new HttpClient(wire){BaseAddress=new Uri("http://model.test/")},Microsoft.Extensions.Options.Options.Create(settings),NullLogger<OllamaClient>.Instance);
        var result=await client.CaseFeedbackAsync([new("system",CaseChatPrompts.Get(AnalysisStage.Premorbid)),new("user",original)],AnalysisStage.Premorbid,default);
        using var request=JsonDocument.Parse(wire.Requests.Single());var schema=request.RootElement.GetProperty("format").GetProperty("properties").GetProperty("annotations");
        Assert.Equal("object",schema.GetProperty("type").GetString());Assert.Equal(67,schema.GetProperty("required").GetArrayLength());
        var userText=request.RootElement.TryGetProperty("messages",out var messages)?messages[1].GetProperty("content").GetString()!
            :System.Text.RegularExpressions.Regex.Match(request.RootElement.GetProperty("prompt").GetString()!,@"<\|im_start\|>user\n(.*?)<\|im_end\|>",System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
        using var user=JsonDocument.Parse(userText);
        Assert.Equal(original,user.RootElement.GetProperty("answerText").GetString());Assert.Equal(67,user.RootElement.GetProperty("annotationSlots").EnumerateObject().Count());
        var parsed=CaseAnnotationContract.Parse(result.Content,original);Assert.Equal(3,parsed.Annotations.Count);Assert.Equal(original.Length-1,parsed.Annotations[^1].End);
        Assert.Contains("D3",parsed.Annotations[1].Codes);Assert.NotEqual(parsed.Annotations[0].Start,parsed.Annotations[^1].Start);
    }
    [Theory][InlineData("missing")][InlineData("unknown")][InlineData("duplicate")]
    public async Task MissingUnknownOrDuplicateSlotsCannotBeAcceptedAsACompleteResponse(string fault)
    {
        var values=Enumerable.Range(0,66).ToDictionary(i=>$"s{i:D3}",_=>new{codes=Array.Empty<string>(),explanation="",status="unverified"});
        if(fault=="missing")values.Remove("s065");if(fault=="unknown")values.Add("unknown",values["s000"]);
        var content=JsonSerializer.Serialize(new{feedback="合成回饋",annotations=values});
        if(fault=="duplicate")content=content.Replace("\"s000\":","\"s000\":{\"codes\":[],\"explanation\":\"\",\"status\":\"unverified\"},\"s000\":",StringComparison.Ordinal);
        var wire=new QueueWire(JsonSerializer.Serialize(new{message=new{role="assistant",content},done_reason="stop"}));
        var error=await Assert.ThrowsAsync<CaseChatException>(()=>Client(wire).CaseFeedbackAsync([new("system","規則"),new("user",string.Concat(Clauses.Take(66)))],AnalysisStage.Premorbid,default));
        Assert.Equal("annotation_slots",error.Diagnostic!.Code);Assert.Single(wire.Requests);Assert.DoesNotContain(Clauses[0],error.ToString());
    }
    private static OllamaClient Client(QueueWire wire)=>new(new HttpClient(wire){BaseAddress=new Uri("http://model.test/")},Microsoft.Extensions.Options.Options.Create(new OllamaOptions{CompactCaseFeedback=true,CaseFeedbackMaxTokens=5120}),NullLogger<OllamaClient>.Instance);
    private static string Envelope(object content)=>JsonSerializer.Serialize(new{message=new{role="assistant",content=JsonSerializer.Serialize(content)},done_reason="stop",prompt_eval_count=10,eval_count=7,eval_duration=1000000000});
    private sealed class QueueWire(params string[] responses):HttpMessageHandler
    {
        public List<string> Requests=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Requests.Add(await request.Content!.ReadAsStringAsync(ct));var response=responses[Requests.Count-1];if(request.RequestUri!.AbsolutePath.EndsWith("/generate")){var json=System.Text.Json.Nodes.JsonNode.Parse(response)!;json["response"]=json["message"]!["content"]!.GetValue<string>();json.AsObject().Remove("message");response=json.ToJsonString();}return new(System.Net.HttpStatusCode.OK){Content=new StringContent(response)};}
    }
}
