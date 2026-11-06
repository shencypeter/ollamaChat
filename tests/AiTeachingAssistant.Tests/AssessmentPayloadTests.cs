using System.Net;
using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiTeachingAssistant.Tests;

public class AssessmentPayloadTests
{
    [Theory]
    [InlineData("breeze", true, 2)]
    [InlineData("llama3", false, 4)]
    [InlineData("qwen", false, 4)]
    public async Task SavedProfilesPreserveWireLayout(string profile, bool merged, int count)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        // Profile files are copied by the application project into build output.
        var path = Path.Combine(root.FullName, "Configuration", "Ollama", profile + ".json");
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var settings = config.RootElement.GetProperty("Ollama").Deserialize<OllamaOptions>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(merged, settings.MergeAssessmentSystemMessages);
        var handler = new Capture();
        var client = new OllamaClient(new HttpClient(handler) { BaseAddress = new Uri(settings.BaseUrl) },
            Microsoft.Extensions.Options.Options.Create(settings), NullLogger<OllamaClient>.Instance);
        await client.AssessAsync([new("system", AnalysisStageInstructions.Get(AnalysisStage.Premorbid)), new("user", AssessmentContract.BuildInput("合成答案", null, null))], false, default);
        using var wire = JsonDocument.Parse(handler.Body!);
        Assert.Equal(settings.Model, wire.RootElement.GetProperty("model").GetString());
        Assert.Equal(count, wire.RootElement.GetProperty("messages").GetArrayLength());
        Assert.Equal(settings.RepeatAssessmentRequirements, wire.RootElement.GetProperty("messages")[count - 1].GetProperty("content").GetString()!.StartsWith("本次輸出要求"));
        Assert.True(wire.RootElement.TryGetProperty("format", out _));
    }
    [Fact]
    public async Task MissingCaseUsesReadableChineseAndConstrainedSchema()
    {
        var handler = new Capture();
        var input = AssessmentContract.BuildInput("資料不足，無法推論。", null, null);
        Assert.Contains("資料不足", input);
        await Client(handler).AssessAsync([new("system", AnalysisStageInstructions.Get(AnalysisStage.Premorbid)), new("user", input)], false, default);
        using var doc = JsonDocument.Parse(handler.Body!);
        var schema = doc.RootElement.GetProperty("format");
        var fields = schema.GetProperty("properties").GetProperty("evaluations").GetProperty("items").GetProperty("properties");
        Assert.False(fields.GetProperty("caseEvidenceAvailable").GetProperty("enum")[0].GetBoolean());
        Assert.Equal(0, fields.GetProperty("supportedClaims").GetProperty("maxItems").GetInt32());
        Assert.Equal("", schema.GetProperty("properties").GetProperty("comparisonFeedback").GetProperty("enum")[0].GetString());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WirePayloadIncludesSchemaSamplingAndIsolatedCaseContext(bool compare)
    {
        var handler = new Capture();
        var client = Client(handler);
        var input = AssessmentContract.BuildInput("current unique answer", compare ? "old unique answer" : null, "reference unique case");
        await client.AssessAsync([new("system", AnalysisStageInstructions.Get(AnalysisStage.Premorbid)), new("user", input)], compare, default);
        using var doc = JsonDocument.Parse(handler.Body!);
        var payload = doc.RootElement;
        Assert.Equal("llama3.1:8b", payload.GetProperty("model").GetString());
        Assert.False(payload.GetProperty("stream").GetBoolean());
        Assert.Equal(0, payload.GetProperty("options").GetProperty("temperature").GetDouble());
        Assert.Equal(42, payload.GetProperty("options").GetProperty("seed").GetInt32());
        Assert.Equal(AssessmentContract.Schema(compare, true).GetRawText(), payload.GetProperty("format").GetRawText());
        var messages = payload.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(new[] { "system", "user" }, messages.Select(m => m.GetProperty("role").GetString()));
        Assert.StartsWith("zh-TW", messages[0].GetProperty("content").GetString());
        Assert.Contains("輸出 JSON schema", messages[0].GetProperty("content").GetString());
        Assert.EndsWith(input, messages[1].GetProperty("content").GetString());
        Assert.DoesNotContain("校準例子", handler.Body!);
    }

    [Fact]
    public async Task GeneralChatDoesNotAcquireAssessmentSchemaOrSampling()
    {
        var handler = new Capture();
        await Client(handler).ChatAsync([new("user", "hello")], default);
        using var doc = JsonDocument.Parse(handler.Body!);
        Assert.False(doc.RootElement.TryGetProperty("format", out _));
        Assert.False(doc.RootElement.TryGetProperty("options", out _));
    }

    [Theory]
    [InlineData("Production", true, false)]
    [InlineData("Development", false, false)]
    [InlineData("Development", true, true)]
    public async Task DiagnosticsAreOptInDevelopmentOnlyAndRedactClinicalContent(string environment, bool enabled, bool captured)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Diagnostics:CaptureOllamaPayload"] = enabled.ToString(), ["Ollama:SystemPrompt"] = "zh-TW"
        }).Build();
        var diagnostics = new OllamaPayloadDiagnostics(new FakeEnvironment(environment), config);
        var handler = new Capture();
        await Client(handler, diagnostics).AssessAsync([
            new("system", AnalysisStageInstructions.Get(AnalysisStage.Psychosocial)),
            new("user", AssessmentContract.BuildInput("SENSITIVE ANSWER", null, "SENSITIVE CASE"))], false, default);
        Assert.Equal(captured, diagnostics.LatestRedactedPayload != null);
        if (captured)
        {
            Assert.DoesNotContain("SENSITIVE", diagnostics.LatestRedactedPayload!);
            Assert.Contains("rubric-evidence-v3", diagnostics.LatestRedactedPayload!);
            using var doc = JsonDocument.Parse(diagnostics.LatestRedactedPayload!);
            Assert.Equal(2, doc.RootElement.GetProperty("payload").GetProperty("messages").GetArrayLength());
            Assert.Contains("SENSITIVE", handler.Body!); // Redaction changes only the diagnostic copy.
        }
    }

    [Fact]
    public void ProductionPromptHasNoOriginalAnswerOrAnswerKey()
    {
        foreach (var stage in Enum.GetValues<AnalysisStage>().Where(s => s != AnalysisStage.GeneralChat))
        {
            var prompt = AnalysisStageInstructions.Get(stage);
            foreach (var fragment in new[] { "完成學業", "有工作經驗", "只是內向", "困難全部由疾病造成", "校準對照", "依規準屬 2 分" })
                Assert.DoesNotContain(fragment, prompt);
        }
    }

    private static OllamaClient Client(Capture handler, OllamaPayloadDiagnostics? diagnostics = null) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") },
        Microsoft.Extensions.Options.Options.Create(new OllamaOptions { Model = "llama3.1:8b", SystemPrompt = "zh-TW" }),
        NullLogger<OllamaClient>.Instance, diagnostics);
    private sealed class Capture : HttpMessageHandler
    {
        public string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"message":{"role":"assistant","content":"{}"},"done_reason":"stop"}""") };
        }
    }
    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

