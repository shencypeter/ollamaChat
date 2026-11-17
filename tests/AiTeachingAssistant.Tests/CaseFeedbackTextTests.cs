using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;
namespace AiTeachingAssistant.Tests;

public sealed class CaseFeedbackTextTests
{
    private static readonly string Loop=string.Concat(Enumerable.Repeat("這是一段循環評語內容，",30));
    private static readonly string Original="本次合成住院接受清創處置。"+new string('測',1100);
    private static readonly object[] Annotations=[new{text="本次合成住院接受清創處置。",occurrence=0,codes=new[]{"B5"},explanation="合成引用",status="unverified"}];
    [Fact] public void DetectsMechanicalLoopsWithoutTreatingRepeatedClinicalTermsAsLoops()
    {
        Assert.True(CaseFeedbackText.RequiresRepair(Loop));
        Assert.False(CaseFeedbackText.RequiresRepair("已區分病前及住院時點。請核對原始資料的時間標記；目前沒有獨立病歷可供核實。"));
        Assert.False(CaseFeedbackText.RequiresRepair("同一術語可在不同事件出現。第一段的時間清楚；第二段仍應標出來源。"));
    }
    [Fact] public async Task LongBRepairKeepsAnnotationsAndNeverResendsOldDraftsOrTheBrokenFeedback()
    {
        var wire=new QueueWire(Envelope(new{annotations=Annotations,feedback=Loop}),Envelope(new{feedback="已按時間整理本次處置。請核對原始病歷的日期與來源；目前無法核實案例事實。"}));
        var reply=await Client(wire).CaseFeedbackAsync([new("system",CaseChatPrompts.Get(AnalysisStage.IllnessCourse)),new("user","OLD_DRAFT_MARKER"),new("assistant","OLD_ASSISTANT_MARKER"),new("user",Original)],AnalysisStage.IllnessCourse,default);
        Assert.Equal(2,wire.Requests.Count);Assert.All(wire.Requests,r=>{Assert.DoesNotContain("OLD_DRAFT_MARKER",r);Assert.DoesNotContain("OLD_ASSISTANT_MARKER",r);});
        using var repair=JsonDocument.Parse(wire.Requests[1]);var messages=repair.RootElement.GetProperty("messages");Assert.Equal(2,messages.GetArrayLength());
        using var data=JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);Assert.Equal(Original,data.RootElement.GetProperty("answerText").GetString());Assert.DoesNotContain("循環評語內容",messages[1].GetProperty("content").GetString());
        using var body=JsonDocument.Parse(reply.Content);Assert.Equal(JsonSerializer.Serialize(Annotations),body.RootElement.GetProperty("annotations").GetRawText());Assert.DoesNotContain("循環評語內容",body.RootElement.GetProperty("feedback").GetString());
        Assert.Equal(2,reply.Metrics.ProviderCalls);Assert.Equal(20,reply.Metrics.PromptTokens);Assert.Equal(14,reply.Metrics.ResponseTokens);Assert.DoesNotContain("loadDurationNanoseconds",reply.Metrics.AvailableFields!);
    }
    [Fact] public async Task ASecondLoopFailsSafelyAndDoesNotMakeAThirdCall()
    {
        var wire=new QueueWire(Envelope(new{annotations=Annotations,feedback=Loop}),Envelope(new{feedback=Loop}));
        var failure=await Assert.ThrowsAsync<CaseChatException>(()=>Client(wire).CaseFeedbackAsync([new("system","B test"),new("user",Original)],AnalysisStage.IllnessCourse,default));
        Assert.Equal("feedback_repetition",failure.Diagnostic!.Code);Assert.Equal(2,wire.Requests.Count);Assert.DoesNotContain(Original,failure.ToString());
    }
    [Theory][InlineData(AnalysisStage.IllnessCourse,30)][InlineData(AnalysisStage.Premorbid,1100)]
    public async Task ShortBAndOtherStagesKeepTheirSingleCallBehavior(AnalysisStage stage,int length)
    {
        var wire=new QueueWire(Envelope(new{annotations=Array.Empty<object>(),feedback=Loop}));
        await Client(wire).CaseFeedbackAsync([new("system","test"),new("user",new string('測',length))],stage,default);Assert.Single(wire.Requests);
    }
    private static OllamaClient Client(QueueWire wire)=>new(new HttpClient(wire){BaseAddress=new Uri("http://model.test/")},Microsoft.Extensions.Options.Options.Create(new OllamaOptions{CompactCaseFeedback=true,CaseFeedbackMaxTokens=4096}),NullLogger<OllamaClient>.Instance);
    private static string Envelope(object content)=>JsonSerializer.Serialize(new{message=new{role="assistant",content=JsonSerializer.Serialize(content)},done_reason="stop",prompt_eval_count=10,eval_count=7,eval_duration=1000000000});
    private sealed class QueueWire(params string[] responses):HttpMessageHandler
    {
        public List<string> Requests=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Requests.Add(await request.Content!.ReadAsStringAsync(ct));return new(System.Net.HttpStatusCode.OK){Content=new StringContent(responses[Requests.Count-1])};}
    }
}
