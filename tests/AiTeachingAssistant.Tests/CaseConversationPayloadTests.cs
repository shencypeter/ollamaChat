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
            Assert.Equal(8,properties.GetProperty("annotations").GetProperty("maxItems").GetInt32());
            var annotation=properties.GetProperty("annotations").GetProperty("items").GetProperty("properties");Assert.Equal(160,annotation.GetProperty("text").GetProperty("maxLength").GetInt32());Assert.Equal(80,annotation.GetProperty("explanation").GetProperty("maxLength").GetInt32());
        }
        await client.ConversationAsync([new("user","合成Raw")],default);using var raw=JsonDocument.Parse(capture.Body!);
        Assert.Equal(512,raw.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());Assert.False(raw.RootElement.TryGetProperty("format",out _));Assert.Equal(512,settings.AssessmentSampling.NumPredict);
    }
    [Fact] public async Task TruncatedFormalReplyHasAnExplicitSafeErrorWhileRawKeepsItsExistingBehavior()
    {
        var capture=new Capture{Response="""{"message":{"role":"assistant","content":"synthetic partial reply"},"done_reason":"length"}"""};
        var client=new OllamaClient(new HttpClient(capture){BaseAddress=new Uri("http://localhost:11434/")},Microsoft.Extensions.Options.Options.Create(new OllamaOptions()),new CaptureLogger());
        var failure=await Assert.ThrowsAsync<CaseChatException>(()=>client.CaseFeedbackAsync([new("system","synthetic system"),new("user","synthetic answer")],AnalysisStage.IllnessCourse,default));
        Assert.Equal(502,failure.StatusCode);Assert.Contains("輸出上限",failure.Message);Assert.DoesNotContain("partial reply",failure.Message);
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
        using var payload=JsonDocument.Parse(transport.Body!);var messages=payload.RootElement.GetProperty("messages");
        Assert.Equal(2,messages.GetArrayLength());Assert.Equal(prompt,messages[0].GetProperty("content").GetString());
        Assert.Equal("user",messages[1].GetProperty("role").GetString());
        using var current=JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);
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
        using var answerPayload=JsonDocument.Parse(root.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(original,answerPayload.RootElement.GetProperty("answerText").GetString());
        Assert.Equal(2,root.GetProperty("messages").GetArrayLength());
        Assert.Contains("annotations",root.GetProperty("format").GetRawText());
        Assert.Equal("array",root.GetProperty("format").GetProperty("properties").GetProperty("annotations").GetProperty("type").GetString());
        Assert.Empty(logger.Messages);
        await client.ConversationAsync([new("user",original)],default);
        using var raw=JsonDocument.Parse(transport.Body!);Assert.False(raw.RootElement.TryGetProperty("format",out _));Assert.Equal(1,raw.RootElement.GetProperty("messages").GetArrayLength());
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
        using var configuration = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Configuration", "Ollama", profile + ".json")));
        var options = configuration.RootElement.GetProperty("Ollama").Deserialize<OllamaOptions>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var diagnostics = new OllamaPayloadDiagnostics(new Environment(), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Diagnostics:CaptureOllamaPayload"] = "true" }).Build());
        var inspection = new OllamaRequestInspection(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role,"Professor") }, "test")) } });
        var transport = new Capture(); var logger = new CaptureLogger();
        var client = new OllamaClient(new HttpClient(transport) { BaseAddress = new Uri("http://localhost:11434/") }, Microsoft.Extensions.Options.Options.Create(options), logger, diagnostics, inspection);
        var reply = await client.ConversationAsync([new("user", "原始 human"), new("assistant", "原始 ai"), new("user", "下一輪")], default);
        using var raw = JsonDocument.Parse(transport.Body!);
        Assert.Equal(new[] { "user", "assistant", "user" }, raw.RootElement.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("role").GetString()));
        Assert.Equal(new[] { "原始 human", "原始 ai", "下一輪" }, raw.RootElement.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("content").GetString()));
        Assert.False(raw.RootElement.TryGetProperty("format", out _)); Assert.Null(reply.Thinking);
        await client.ConversationAsync([new("system", "server stage instructions"), new("user", "stage input")], default);
        using var stage = JsonDocument.Parse(transport.Body!);
        Assert.Equal("server stage instructions", stage.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal(2, stage.RootElement.GetProperty("messages").GetArrayLength());
        var expectedStageSampling=JsonSerializer.SerializeToElement(options.AssessmentSampling with{NumPredict=options.CaseFeedbackMaxTokens??options.AssessmentSampling.NumPredict});
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

    private sealed class Capture : HttpMessageHandler
    {
        public string? Body;
        public string Response = """{"message":{"role":"assistant","content":"plain reply","thinking":"private thinking"},"done_reason":"stop"}""";
        public HttpStatusCode Status = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Body = await request.Content!.ReadAsStringAsync(ct); return new(Status) { Content = new StringContent(Response) }; }
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
