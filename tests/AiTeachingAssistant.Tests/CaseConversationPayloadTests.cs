using System.Net;
using System.Security.Claims;
using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiTeachingAssistant.Tests;

public sealed class CaseConversationPayloadTests
{
    [Theory][InlineData("llama3")][InlineData("qwen")][InlineData("breeze")][InlineData("qwen25-omni")]
    public async Task RawSessionAddsOnlyLanguagePolicyAcrossTransportsWithoutStoringItOrChangingStages(string profile)
    {
        var cfg=new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory,"Configuration","Ollama",profile+".json")).Build();
        var settings=cfg.GetSection("Ollama").Get<OllamaOptions>()!;var limits=cfg.GetSection("CaseChat").Get<CaseChatOptions>()??new();
        var capture=new Capture{Response=JsonSerializer.Serialize(new{message=new{content="Synthetic reply"},done_reason="stop"})};var logger=new CaptureLogger();
        var client=new OllamaClient(new HttpClient(capture){BaseAddress=new Uri("http://model.test/")},Microsoft.Extensions.Options.Options.Create(settings),logger);
        using var sessions=new RawChatSessions(TimeProvider.System,Microsoft.Extensions.Options.Options.Create(limits),Microsoft.Extensions.Options.Options.Create(settings));
        var course=Guid.NewGuid();var actor=Guid.NewGuid();var id=sessions.Create(course,actor);
        await sessions.Send(course,actor,id,"Hello, explain briefly.",client,default);
        using(var first=JsonDocument.Parse(capture.Body!)){
            var messages=WireMessages(first.RootElement);Assert.Equal(new[]{"system","user"},messages.Select(m=>m.Role));
            Assert.Contains("language of the user's current message",messages[0].Content);Assert.Contains("Traditional Chinese",messages[0].Content);Assert.Contains("Taiwan",messages[0].Content);Assert.Contains("zh-TW",messages[0].Content);Assert.Contains("zh-CN",messages[0].Content);
            Assert.DoesNotContain(settings.SystemPrompt,messages[0].Content);Assert.Equal("Hello, explain briefly.",messages[1].Content);Assert.False(first.RootElement.TryGetProperty("format",out _));
        }
        var reply=await sessions.Send(course,actor,id,"請用中文簡短回答。",client,default);
        using(var second=JsonDocument.Parse(capture.Body!)){
            var messages=WireMessages(second.RootElement);Assert.Equal(new[]{"system","user","assistant","user"},messages.Select(m=>m.Role));Assert.Single(messages.Where(m=>m.Role=="system"));
            Assert.Equal(new[]{"Hello, explain briefly.","Synthetic reply","請用中文簡短回答。"},messages.Skip(1).Select(m=>m.Content));
        }
        Assert.Equal(4,reply.Messages.Count);Assert.DoesNotContain(reply.Messages,m=>m.Role=="system");Assert.Empty(logger.Messages);
        capture.Response=JsonSerializer.Serialize(new{message=new{content=JsonSerializer.Serialize(new{feedback="合成回饋",annotations=Array.Empty<object>()})},done_reason="stop"});
        await client.CaseFeedbackAsync([new("system","Stage-only synthetic instruction"),new("user","合成作答")],AnalysisStage.Premorbid,default);
        using var formal=JsonDocument.Parse(capture.Body!);Assert.Equal("Stage-only synthetic instruction",WireMessages(formal.RootElement)[0].Content);
    }
    [Fact] public async Task QwenThinkingAndCompactFeedbackAreExplicitWithoutChangingOtherProfiles()
    {
        foreach(var profile in new[]{"qwen","llama3","breeze","qwen25-omni"}){
            var settings=new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory,"Configuration","Ollama",profile+".json")).Build().GetSection("Ollama").Get<OllamaOptions>()!;
            var capture=new Capture();var client=new OllamaClient(new HttpClient(capture){BaseAddress=new Uri("http://model.test/")},Microsoft.Extensions.Options.Options.Create(settings),new CaptureLogger());
            await client.ConversationAsync([new("user","合成第一輪"),new("assistant","合成回覆"),new("user","合成第二輪")],default);
            using var raw=JsonDocument.Parse(capture.Body!);
            if(profile=="qwen"){Assert.True(raw.RootElement.TryGetProperty("think",out var think));Assert.False(think.GetBoolean());Assert.Equal(600,settings.TimeoutSeconds);Assert.True(settings.CompactCaseFeedback);}
            else{Assert.False(raw.RootElement.TryGetProperty("think",out _));Assert.Equal(180,settings.TimeoutSeconds);}
            Assert.Equal(new[]{"user","assistant","user"},WireMessages(raw.RootElement).Select(m=>m.Role));
            await client.CaseFeedbackAsync([new("system",CaseChatPrompts.Get(AnalysisStage.Premorbid)),new("user","合成病前作答")],AnalysisStage.Premorbid,default);
            using var formal=JsonDocument.Parse(capture.Body!);Assert.Equal(profile is "qwen" or "qwen25-omni"?600:12000,formal.RootElement.GetProperty("format").GetProperty("properties").GetProperty("feedback").GetProperty("maxLength").GetInt32());
            if(profile=="qwen")Assert.False(formal.RootElement.GetProperty("think").GetBoolean());
        }
    }
    [Fact] public async Task TimeoutDiagnosticsShowActualHttpBudgetWithoutLeakingTransportErrors()
    {
        var capture=new Capture{SimulateTimeout=true};var client=new OllamaClient(new HttpClient(capture){BaseAddress=new Uri("http://model.test/"),Timeout=TimeSpan.FromSeconds(5)},Microsoft.Extensions.Options.Options.Create(new OllamaOptions{TimeoutSeconds=600}),new CaptureLogger());
        var failure=await Assert.ThrowsAsync<CaseChatException>(()=>client.ConversationAsync([new("user","合成測試")],default));
        Assert.Equal("provider_timeout",failure.Diagnostic!.Code);Assert.Contains("5 秒",failure.Diagnostic.Reason);Assert.DoesNotContain("600",failure.Diagnostic.Reason);Assert.DoesNotContain("SENSITIVE",failure.ToString());
    }
    [Theory][InlineData("llama3")][InlineData("qwen")][InlineData("breeze")][InlineData("qwen25-omni")]
    public async Task AllProfilesAcceptLongAnswersInEveryPhaseWithTheFullWrappedPrompt(string profile)
    {
        var cfg=new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory,"Configuration","Ollama",profile+".json")).Build();
        var settings=cfg.GetSection("Ollama").Get<OllamaOptions>()!;var limits=cfg.GetSection("CaseChat").Get<CaseChatOptions>()??new();
        var capture=new Capture{RespondToSlots=true};var client=new OllamaClient(new HttpClient(capture){BaseAddress=new Uri("http://model.test/")},Microsoft.Extensions.Options.Options.Create(settings),new CaptureLogger());
        var original=string.Join("，",Enumerable.Repeat(new string('測',24),85))+"測";
        Assert.Equal(2125,original.Length);
        foreach(var phase in new[]{AnalysisStage.Premorbid,AnalysisStage.IllnessCourse,AnalysisStage.FunctionalAssessment,AnalysisStage.Psychosocial}){
            var prompt=CaseClassificationRules.Prompt(phase,0,CaseClassificationRules.Defaults);
            var input=new[]{new OllamaMessage("system",prompt),new OllamaMessage("user",original){CaseRules=CaseClassificationRules.Defaults}};
            CaseChatBudget.EnsureFits(input,limits,settings,settings.CaseFeedbackMaxTokens);
            await client.CaseFeedbackAsync(input,phase,default);
            using var wire=JsonDocument.Parse(capture.Body!);Assert.Equal(32768,wire.RootElement.GetProperty("options").GetProperty("num_ctx").GetInt32());
            var messages=WireMessages(wire.RootElement);Assert.Equal(2,messages.Count);Assert.Equal(prompt,messages[0].Content);
            using var body=JsonDocument.Parse(messages[1].Content);Assert.Equal(original,body.RootElement.GetProperty("answerText").GetString());
            Assert.Equal(85,body.RootElement.GetProperty("annotationSlots").EnumerateObject().Count());
            await client.ConversationAsync([new("user","合成Raw第一輪"),new("assistant","合成Raw回覆"),new("user","合成Raw第二輪")],default);
            using var raw=JsonDocument.Parse(capture.Body!);Assert.Equal(32768,raw.RootElement.GetProperty("options").GetProperty("num_ctx").GetInt32());Assert.DoesNotContain(WireMessages(raw.RootElement),m=>m.Role=="system");Assert.Equal(3,WireMessages(raw.RootElement).Count);
        }
    }
    [Theory][InlineData(AnalysisStage.Premorbid)][InlineData(AnalysisStage.IllnessCourse)][InlineData(AnalysisStage.FunctionalAssessment)][InlineData(AnalysisStage.Psychosocial)]
    public async Task OmniGuidanceKeepsTwoThousandCharacterAnswersWithinActualWrappedRequestBudget(AnalysisStage phase)
    {
        var settings=new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory,"Configuration","Ollama","qwen25-omni.json")).Build().GetSection("Ollama").Get<OllamaOptions>()!;
        var capture=new Capture();var client=new OllamaClient(new HttpClient(capture){BaseAddress=new Uri("http://model.test/")},Microsoft.Extensions.Options.Options.Create(settings),new CaptureLogger());
        try{await client.CaseFeedbackAsync([new("system",CaseChatPrompts.Get(phase)),new("user",new string('測',2125))],phase,default);}
        catch(CaseChatException error){Assert.Fail(error.Diagnostic?.Reason??error.Message);}
        Assert.NotNull(capture.Body);
    }
    [Fact] public async Task OmniConversationUsesExplicitQwenRoleProtocolWithoutAddingARawSystemPrompt()
    {
        var capture=new Capture();var settings=new OllamaOptions{ConversationTransport="qwen-chatml-generate"};
        var client=new OllamaClient(new HttpClient(capture){BaseAddress=new Uri("http://localhost:11434/")},Microsoft.Extensions.Options.Options.Create(settings),new CaptureLogger());
        await client.ConversationAsync([new("user","第一輪 😀\n"),new("assistant","合成回覆"),new("user","第二輪")],default);
        using var body=JsonDocument.Parse(capture.Body!);Assert.Equal("/api/generate",capture.Url!.AbsolutePath);Assert.True(body.RootElement.GetProperty("raw").GetBoolean());Assert.False(body.RootElement.TryGetProperty("messages",out _));Assert.False(body.RootElement.TryGetProperty("format",out _));
        Assert.Equal("<|im_start|>user\n第一輪 😀\n<|im_end|>\n<|im_start|>assistant\n合成回覆<|im_end|>\n<|im_start|>user\n第二輪<|im_end|>\n<|im_start|>assistant\n",body.RootElement.GetProperty("prompt").GetString());
        Assert.DoesNotContain("<|im_start|>system",body.RootElement.GetProperty("prompt").GetString());
    }
    [Theory]
    [InlineData("model SENSITIVE_MODEL not found","model_missing")]
    [InlineData("SENSITIVE out of memory","provider_memory")]
    [InlineData("SENSITIVE context length exceeded","provider_context")]
    public void KnownUpstreamReasonsAreClassifiedWithoutEchoingTheError(string upstream,string code)
    {
        var result=CaseChatFailure.FromHttp(500,JsonSerializer.Serialize(new{error=upstream}));
        Assert.Equal(code,result.Code);Assert.DoesNotContain("SENSITIVE",JsonSerializer.Serialize(result));
    }
    [Theory]
    [InlineData(404,"provider_http")][InlineData(503,"provider_http")]
    public async Task ConversationErrorsKeepSafeStatusDiagnosticsWithoutUpstreamBody(int status,string code)
    {
        var capture=new Capture{Status=(HttpStatusCode)status,Response="{\"error\":\"SENSITIVE_UPSTREAM_BODY\"}"};
        var client=new OllamaClient(new HttpClient(capture){BaseAddress=new Uri("http://localhost:11434/")},Microsoft.Extensions.Options.Options.Create(new OllamaOptions()),new CaptureLogger());
        var failure=await Assert.ThrowsAsync<CaseChatException>(()=>client.ConversationAsync([new("user","SENSITIVE_INPUT")],default));
        Assert.Equal(code,failure.Diagnostic!.Code);Assert.Equal(status,failure.Diagnostic.UpstreamStatus);
        Assert.DoesNotContain("SENSITIVE",JsonSerializer.Serialize(failure.Diagnostic));Assert.DoesNotContain("SENSITIVE",failure.ToString());
    }
    [Fact] public async Task IllnessFeedbackDistinguishesPhaseFromSubcodesWithoutForcingACorrectionOrChangingOtherStages()
    {
        var capture=new Capture();var client=new OllamaClient(new HttpClient(capture){BaseAddress=new Uri("http://localhost:11434/")},Microsoft.Extensions.Options.Options.Create(new OllamaOptions()),new CaptureLogger());
        const string original="  合成原文😀\n";
        await client.CaseFeedbackAsync([new("system",CaseChatPrompts.Get(AnalysisStage.IllnessCourse)),new("user",original)],AnalysisStage.IllnessCourse,default);
        using var request=JsonDocument.Parse(capture.Body!);var messages=request.RootElement.GetProperty("messages");
        Assert.Equal("annotations",request.RootElement.GetProperty("format").GetProperty("properties").EnumerateObject().First().Name);
        Assert.Contains("B階段不等於B4",messages[0].GetProperty("content").GetString());
        using var answer=JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);
        Assert.Equal(original,answer.RootElement.GetProperty("answerText").GetString());
        Assert.Contains("個案經歷了什麼？",answer.RootElement.GetProperty("outputRequirements").GetString());
        Assert.Contains("尚待與原始教材核對",answer.RootElement.GetProperty("outputRequirements").GetString());
        Assert.Contains("有實際問題才建議修改",answer.RootElement.GetProperty("outputRequirements").GetString());
        Assert.DoesNotContain("不能只稱讚",answer.RootElement.GetProperty("outputRequirements").GetString());
        foreach(var stage in new[]{AnalysisStage.Premorbid,AnalysisStage.FunctionalAssessment,AnalysisStage.Psychosocial}){
            Assert.DoesNotContain("B階段不等於B4",CaseChatPrompts.Get(stage));
            Assert.Equal(CaseChatPrompts.Version,CaseChatPrompts.VersionFor(stage));
        }
        Assert.NotEqual(CaseChatPrompts.Version,CaseChatPrompts.VersionFor(AnalysisStage.IllnessCourse));
        Assert.DoesNotContain("層級對照",CaseChatPrompts.TeachingInstructions(AnalysisStage.IllnessCourse));
    }
    [Fact] public async Task CompactCaseBudgetKeepsRawShortAndReturnsBoundedFormalSchemaWithoutMutatingSampling()
    {
        var settings=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Model"]="test-omni",["AssessmentSampling:NumPredict"]="512",["CaseFeedbackMaxTokens"]="4096",["CompactCaseFeedback"]="true"}).Build().Get<OllamaOptions>()!;
        var capture=new Capture();var client=new OllamaClient(new HttpClient(capture){BaseAddress=new Uri("http://localhost:11434/")},Microsoft.Extensions.Options.Options.Create(settings),new CaptureLogger());
        await client.CaseFeedbackAsync([new("system",CaseChatPrompts.Get(AnalysisStage.IllnessCourse)),new("user","合成格式測試")],AnalysisStage.IllnessCourse,default);
        using(var stage=JsonDocument.Parse(capture.Body!)){
            Assert.Equal(4096,stage.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());
            var properties=stage.RootElement.GetProperty("format").GetProperty("properties");Assert.Equal(600,properties.GetProperty("feedback").GetProperty("maxLength").GetInt32());
            Assert.Equal(128,properties.GetProperty("annotations").GetProperty("maxItems").GetInt32());
            var annotation=properties.GetProperty("annotations").GetProperty("items").GetProperty("properties");Assert.Equal(160,annotation.GetProperty("text").GetProperty("maxLength").GetInt32());Assert.Equal(80,annotation.GetProperty("explanation").GetProperty("maxLength").GetInt32());
        }
        await client.ConversationAsync([new("user","合成Raw")],default);using var raw=JsonDocument.Parse(capture.Body!);
        Assert.Equal(512,raw.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());Assert.False(raw.RootElement.TryGetProperty("format",out _));Assert.Equal(512,settings.AssessmentSampling.NumPredict);
    }
    [Fact] public async Task ProfilesKeepTheirOwnFormalAndRawOutputBudgetsWithExpandedContext()
    {
        foreach(var profile in new[]{"qwen25-omni","llama3","breeze","qwen"}){
            var settings=new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory,"Configuration","Ollama",profile+".json")).Build().GetSection("Ollama").Get<OllamaOptions>()!;
            var capture=new Capture();var client=new OllamaClient(new HttpClient(capture){BaseAddress=new Uri("http://localhost:11434/")},Microsoft.Extensions.Options.Options.Create(settings),new CaptureLogger());
            var formalBudget=profile=="qwen25-omni"?8192:4096;
            Assert.Equal(32768,settings.AssessmentSampling.NumCtx);
            if(profile=="qwen25-omni")CaseChatBudget.EnsureFits([new("system",CaseChatPrompts.Get(AnalysisStage.FunctionalAssessment)),new("user",new string('測',2125))],new CaseChatOptions{MaxContextBytes=24000},settings,settings.CaseFeedbackMaxTokens);
            await client.CaseFeedbackAsync([new("system",CaseChatPrompts.Get(AnalysisStage.FunctionalAssessment)),new("user",profile=="qwen25-omni"?new string('測',2125):"合成容量檢查")],AnalysisStage.FunctionalAssessment,default);
            using(var stage=JsonDocument.Parse(capture.Body!))Assert.Equal(formalBudget,stage.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());
            await client.ConversationAsync([new("user","合成Raw")],default);
            using var raw=JsonDocument.Parse(capture.Body!);
            Assert.Equal(profile=="qwen25-omni"?512:4096,raw.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());
        }
    }
    [Theory][InlineData(true)][InlineData(false)] public async Task TruncatedFormalReplyHasAnExplicitSafeErrorWhileRawKeepsItsExistingBehavior(bool suppliesTokens)
    {
        var response=new Dictionary<string,object>{{"message",new{role="assistant",content="synthetic partial reply"}},{"done_reason","length"}};if(suppliesTokens)response["eval_count"]=8192;
        var capture=new Capture{Response=JsonSerializer.Serialize(response)};
        var client=new OllamaClient(new HttpClient(capture){BaseAddress=new Uri("http://localhost:11434/")},Microsoft.Extensions.Options.Options.Create(new OllamaOptions{CaseFeedbackMaxTokens=8192}),new CaptureLogger());
        var failure=await Assert.ThrowsAsync<CaseChatException>(()=>client.CaseFeedbackAsync([new("system","synthetic system"),new("user","synthetic answer")],AnalysisStage.IllnessCourse,default));
        Assert.Equal(502,failure.StatusCode);Assert.Contains("輸出上限",failure.Message);Assert.DoesNotContain("partial reply",failure.Message);
        Assert.Contains("輸出上限 8192 tokens",failure.Diagnostic!.Reason);Assert.Contains(suppliesTokens?"實際生成 8192 tokens":"模型未提供實際生成量",failure.Diagnostic.Reason);
        Assert.Equal("synthetic partial reply",(await client.ConversationAsync([new("user","synthetic raw")],default)).Content);
    }
    [Theory][InlineData("breeze")][InlineData("llama3")][InlineData("qwen")][InlineData("qwen25-omni")]
    public async Task StagePayloadContainsOnlyCurrentSystemAndAnswerEvenWhenCallerSuppliesDraftHistory(string profile)
    {
        using var file=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"Configuration","Ollama",profile+".json")));
        var settings=file.RootElement.GetProperty("Ollama").Deserialize<OllamaOptions>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        var transport=new Capture();var client=new OllamaClient(new HttpClient(transport){BaseAddress=new Uri("http://localhost:11434/")},Microsoft.Extensions.Options.Options.Create(settings),new CaptureLogger());
        var prompt=CaseChatPrompts.Get(AnalysisStage.IllnessCourse);
        await client.CaseFeedbackAsync([new("system",prompt),new("user","prior draft must stay in DB"),new("assistant","prior feedback must stay in DB"),new("user","current answer")],AnalysisStage.IllnessCourse,default);
        using var payload=JsonDocument.Parse(transport.Body!);var messages=WireMessages(payload.RootElement);
        Assert.Equal(2,messages.Count);Assert.Equal(prompt,messages[0].Content);
        Assert.Equal("user",messages[1].Role);
        using var current=JsonDocument.Parse(messages[1].Content);
        Assert.Equal("current answer",current.RootElement.GetProperty("answerText").GetString());
        Assert.DoesNotContain("must stay in DB",transport.Body!);
    }
    [Fact] public async Task CurrentStageIsExplicitEvenWhenAnswerContainsOnlyAnotherPhase()
    {
        var transport=new Capture();var client=new OllamaClient(new HttpClient(transport){BaseAddress=new Uri("http://localhost:11434/")},Microsoft.Extensions.Options.Options.Create(new OllamaOptions()),new CaptureLogger());
        const string original="病前能獨立自行搭車及處理帳務。";
        await client.CaseFeedbackAsync([new("system",CaseChatPrompts.Get(AnalysisStage.IllnessCourse)),new("user",original)],AnalysisStage.IllnessCourse,default);
        using var payload=JsonDocument.Parse(transport.Body!);
        using var current=JsonDocument.Parse(payload.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal("B",current.RootElement.GetProperty("currentPhase").GetString());
        Assert.Equal("illnessCourse",current.RootElement.GetProperty("currentStage").GetString());
        Assert.Equal("B 疾病病程",current.RootElement.GetProperty("currentStageLabel").GetString());
        Assert.Equal(original,current.RootElement.GetProperty("answerText").GetString());
        Assert.Equal("system",payload.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
    }
    [Theory][InlineData("breeze")][InlineData("llama3")][InlineData("qwen")][InlineData("qwen25-omni")]
    public async Task FormalAnnotationsRequestDistinctClauseEvidenceAcrossPunctuationWithoutChangingTheOriginal(string profile)
    {
        using var file=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"Configuration","Ollama",profile+".json")));
        var settings=file.RootElement.GetProperty("Ollama").Deserialize<OllamaOptions>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        var transport=new Capture();var client=new OllamaClient(new HttpClient(transport){BaseAddress=new Uri("http://model.test/")},Microsoft.Extensions.Options.Options.Create(settings),new CaptureLogger());
        const string original="He was 15, had surgery; ADL: needs help. 藥量2.5mg，日期2026/1/3、縮寫（ADL, IADL）保持原文。😀";
        foreach(var stage in new[]{AnalysisStage.Premorbid,AnalysisStage.IllnessCourse,AnalysisStage.FunctionalAssessment,AnalysisStage.Psychosocial}){
            await client.CaseFeedbackAsync([new("system",CaseChatPrompts.Get(stage)),new("user",original)],stage,default);
            using var payload=JsonDocument.Parse(transport.Body!);var messages=WireMessages(payload.RootElement);Assert.Equal(2,messages.Count);
            Assert.Contains("最小完整語意片段",messages[0].Content);Assert.Contains("同一句內不同事實分開annotations",messages[0].Content);
            Assert.Contains("，,、；;：:。.!?！？",messages[0].Content);Assert.Contains("小數、日期、縮寫、括號內容勿機械拆開",messages[0].Content);
            using var user=JsonDocument.Parse(messages[1].Content);Assert.Equal(original,user.RootElement.GetProperty("answerText").GetString());
            var textSchema=payload.RootElement.GetProperty("format").GetProperty("properties").GetProperty("annotations").GetProperty("items").GetProperty("properties").GetProperty("text");
            Assert.True(textSchema.TryGetProperty("enum",out var choices));var excerpts=choices.EnumerateArray().Select(c=>c.GetString()).ToArray();Assert.Contains("He was 15",excerpts);Assert.Contains("had surgery",excerpts);Assert.Contains("藥量2.5mg",excerpts);Assert.Contains("縮寫（ADL, IADL）保持原文",excerpts);Assert.DoesNotContain(original,excerpts);
            Assert.Contains("20261120",CaseChatPrompts.VersionFor(stage));
        }
        await client.ConversationAsync([new("user",original)],default);using var raw=JsonDocument.Parse(transport.Body!);Assert.Single(WireMessages(raw.RootElement));Assert.Equal(original,WireMessages(raw.RootElement)[0].Content);
    }
    [Theory]
    [InlineData("breeze")][InlineData("llama3")][InlineData("qwen")][InlineData("qwen25-omni")]
    public async Task AnnotatedFeedbackHasIsolatedSchemaAndNeverRewritesTheLatestOriginal(string profile)
    {
        using var file=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"Configuration","Ollama",profile+".json")));
        var settings=file.RootElement.GetProperty("Ollama").Deserialize<OllamaOptions>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        var transport=new Capture();var logger=new CaptureLogger();
        var client=new OllamaClient(new HttpClient(transport){BaseAddress=new Uri("http://localhost:11434/")},Microsoft.Extensions.Options.Options.Create(settings),logger);
        const string original=" 😀55歲\nADL  ";
        await client.CaseFeedbackAsync([new("system",CaseChatPrompts.Get(AnalysisStage.Premorbid)),new("user",original)],default);
        using var payload=JsonDocument.Parse(transport.Body!);var root=payload.RootElement;
        using var answerPayload=JsonDocument.Parse(WireMessages(root)[1].Content);
        Assert.Equal(original,answerPayload.RootElement.GetProperty("answerText").GetString());
        Assert.Equal(2,WireMessages(root).Count);
        Assert.Contains("annotations",root.GetProperty("format").GetRawText());
        Assert.Equal("array",root.GetProperty("format").GetProperty("properties").GetProperty("annotations").GetProperty("type").GetString());
        Assert.Empty(logger.Messages);
        await client.ConversationAsync([new("user",original)],default);
        using var raw=JsonDocument.Parse(transport.Body!);Assert.False(raw.RootElement.TryGetProperty("format",out _));Assert.Single(WireMessages(raw.RootElement));
    }
    [Fact]
    public async Task OmittedModelMeasurementsAreMarkedUnavailableInsteadOfMeasuredZero()
    {
        var transport=new Capture{Response="""{"message":{"role":"assistant","content":"reply"},"eval_count":0,"eval_duration":0,"prompt_eval_count":12}"""};
        var client=new OllamaClient(new HttpClient(transport){BaseAddress=new Uri("http://localhost:11434/")},Microsoft.Extensions.Options.Options.Create(new OllamaOptions()),new CaptureLogger());
        var reply=await client.ConversationAsync([new("user","synthetic")],default);
        Assert.Contains("responseTokens",reply.Metrics.AvailableFields!);Assert.Contains("promptTokens",reply.Metrics.AvailableFields!);Assert.Contains("roundTripMilliseconds",reply.Metrics.AvailableFields!);
        Assert.DoesNotContain("cachedPromptTokens",reply.Metrics.AvailableFields!);Assert.DoesNotContain("totalDurationNanoseconds",reply.Metrics.AvailableFields!);Assert.DoesNotContain("tokensPerSecond",reply.Metrics.AvailableFields!);
        Assert.Equal(0,reply.Metrics.ResponseTokens);
    }
    [Theory]
    [InlineData("breeze")][InlineData("llama3")][InlineData("qwen")][InlineData("qwen25-omni")]
    public async Task RawHasOnlyOriginalRolesAndStageKeepsItsConfiguredSamplingWithoutCapture(string profile)
    {
        var options = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory,"Configuration","Ollama",profile+".json")).Build().GetSection("Ollama").Get<OllamaOptions>()!;
        var diagnostics = new OllamaPayloadDiagnostics(new Environment(), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Diagnostics:CaptureOllamaPayload"] = "true" }).Build());
        var inspection = new OllamaRequestInspection(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role,"Professor") }, "test")) } });
        var transport = new Capture(); var logger = new CaptureLogger();
        var client = new OllamaClient(new HttpClient(transport) { BaseAddress = new Uri("http://localhost:11434/") }, Microsoft.Extensions.Options.Options.Create(options), logger, diagnostics, inspection);
        var reply = await client.ConversationAsync([new("user", "原始 human"), new("assistant", "原始 ai"), new("user", "下一輪")], default);
        using var raw = JsonDocument.Parse(transport.Body!);
        Assert.Equal(new[] { "user", "assistant", "user" }, WireMessages(raw.RootElement).Select(m=>m.Role));
        Assert.Equal(new[] { "原始 human", "原始 ai", "下一輪" }, WireMessages(raw.RootElement).Select(m=>m.Content));
        Assert.False(raw.RootElement.TryGetProperty("format", out _)); Assert.Null(reply.Thinking);
        await client.ConversationAsync([new("system", "server stage instructions"), new("user", "stage input")], default);
        using var stage = JsonDocument.Parse(transport.Body!);
        Assert.Equal("server stage instructions", WireMessages(stage.RootElement)[0].Content);
        Assert.Equal(2, WireMessages(stage.RootElement).Count);
        var expectedStageSampling=JsonSerializer.SerializeToElement(options.AssessmentSampling);
        Assert.Equal(expectedStageSampling.GetRawText(), stage.RootElement.GetProperty("options").GetRawText());
        var sampling = raw.RootElement.GetProperty("options");
        Assert.Equal(0, sampling.GetProperty("temperature").GetDouble()); Assert.Equal(42, sampling.GetProperty("seed").GetInt32());
        Assert.Equal(0.9, sampling.GetProperty("top_p").GetDouble()); Assert.Equal(40, sampling.GetProperty("top_k").GetInt32());
        Assert.Equal(options.AssessmentSampling.NumCtx, sampling.GetProperty("num_ctx").GetInt32()); Assert.Equal(options.AssessmentSampling.NumPredict, sampling.GetProperty("num_predict").GetInt32());
        Assert.Null(diagnostics.LatestRedactedPayload); Assert.Null(inspection.Current); Assert.Empty(logger.Messages);
    }

    [Theory]
    [InlineData(200, "{\"message\":{\"content\":SENSITIVE_INVALID_JSON}}")]
    [InlineData(500, "SENSITIVE_ERROR_BODY")]
    public async Task ProviderFailureNeverKeepsResponseContentInExceptionOrLog(int status, string body)
    {
        var logger = new CaptureLogger(); var capture = new Capture { Response = body, Status = (HttpStatusCode)status };
        var client = new OllamaClient(new HttpClient(capture) { BaseAddress = new Uri("http://localhost:11434/") }, Microsoft.Extensions.Options.Options.Create(new OllamaOptions()), logger);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => client.ConversationAsync([new("user", "SENSITIVE_INPUT")], default));
        Assert.DoesNotContain("SENSITIVE", error.ToString()); Assert.All(logger.Messages, m => Assert.DoesNotContain("SENSITIVE", m));
    }

    private static IReadOnlyList<OllamaMessage> WireMessages(JsonElement root)
    {
        if(root.TryGetProperty("messages",out var messages))return messages.Deserialize<OllamaMessage[]>()!;
        return System.Text.RegularExpressions.Regex.Matches(root.GetProperty("prompt").GetString()!,@"<\|im_start\|>(system|user|assistant)\n(.*?)<\|im_end\|>\n",System.Text.RegularExpressions.RegexOptions.Singleline).Select(m=>new OllamaMessage(m.Groups[1].Value,m.Groups[2].Value)).ToArray();
    }
    private sealed class Capture : HttpMessageHandler
    {
        public bool RespondToSlots;
        public bool SimulateTimeout;
        public string? Body;public Uri? Url;
        public string Response = """{"message":{"role":"assistant","content":"{\"feedback\":\"合成回饋，案例事實待核實。\",\"annotations\":[]}","thinking":"private thinking"},"done_reason":"stop"}""";
        public HttpStatusCode Status = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Body = await request.Content!.ReadAsStringAsync(ct);Url=request.RequestUri;if(SimulateTimeout)throw new TaskCanceledException("SENSITIVE transport diagnostic");var response=Response;
            if(RespondToSlots){using var input=JsonDocument.Parse(Body);if(input.RootElement.TryGetProperty("format",out var format)&&format.GetProperty("properties").GetProperty("annotations").GetProperty("type").GetString()=="object"){
                var annotations=format.GetProperty("properties").GetProperty("annotations").GetProperty("properties").EnumerateObject().ToDictionary(p=>p.Name,p=>(object)new{codes=Array.Empty<string>(),explanation="合成容量測試",status="unverified"});
                response=JsonSerializer.Serialize(new{message=new{role="assistant",content=JsonSerializer.Serialize(new{feedback="合成回饋，案例事實待核實。",annotations})},done_reason="stop"});
            }}
            if(request.RequestUri!.AbsolutePath.EndsWith("/generate")&&Status==HttpStatusCode.OK){var json=System.Text.Json.Nodes.JsonNode.Parse(response)!;json["response"]=json["message"]?["content"]?.GetValue<string>();json.AsObject().Remove("message");response=json.ToJsonString();}
            return new(Status) { Content = new StringContent(response) }; }
    }
    private sealed class CaptureLogger : ILogger<OllamaClient>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState,Exception?,string> formatter) => Messages.Add(formatter(state, exception) + exception);
    }
    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
