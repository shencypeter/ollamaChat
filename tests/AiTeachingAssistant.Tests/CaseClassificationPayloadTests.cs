using System.Net;
using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiTeachingAssistant.Tests;

public sealed class CaseClassificationPayloadTests
{
    [Theory]
    [InlineData("llama3")][InlineData("breeze")][InlineData("qwen")][InlineData("qwen25-omni")]
    public async Task CourseCatalogFlowsIntoActualSchemaAndNeverLeaksAsMessageMetadata(string profile)
    {
        var definitions=CaseClassificationRules.Defaults.Select(d=>d.Code=="A1"?d with{Enabled=false}:d).Concat([new CaseClassificationRuleDefinition("C7","合成分類","C","合成描述",["提示"],true)]).ToArray();
        using var handler=new Capture();
        var options=new OllamaOptions{Profile=profile,CompactCaseFeedback=profile=="qwen25-omni",ConversationTransport=profile=="qwen25-omni"?"qwen-chatml-generate":"chat"};
        var client=new OllamaClient(new HttpClient(handler){BaseAddress=new("http://localhost:11434/")},Microsoft.Extensions.Options.Options.Create(options),NullLogger<OllamaClient>.Instance);
        var prompt=CaseClassificationRules.Prompt(AnalysisStage.FunctionalAssessment,1,definitions);
        await client.CaseFeedbackAsync([new("system",prompt),new("user","合成原文"){CaseRules=definitions}],AnalysisStage.FunctionalAssessment,default);
        using var doc=JsonDocument.Parse(handler.Body!);var root=doc.RootElement;
        var codes=root.GetProperty("format").GetProperty("properties").GetProperty("annotations").GetProperty("items").GetProperty("properties").GetProperty("codes").GetProperty("items").GetProperty("enum").EnumerateArray().Select(x=>x.GetString()).ToArray();
        Assert.Contains("C7",codes);Assert.DoesNotContain("A1",codes);Assert.DoesNotContain("A1a",codes);Assert.DoesNotContain("caseRules",handler.Body!);
        if(profile=="qwen25-omni")Assert.Contains("合成描述",root.GetProperty("prompt").GetString());
        else Assert.Contains("合成描述",root.GetProperty("messages")[0].GetProperty("content").GetString());
        await client.ConversationAsync([new("user","合成Raw")],default);
        using var raw=JsonDocument.Parse(handler.Body!);Assert.False(raw.RootElement.TryGetProperty("format",out _));
        if(profile=="qwen25-omni")Assert.DoesNotContain("<|im_start|>system",raw.RootElement.GetProperty("prompt").GetString());
        else Assert.DoesNotContain(raw.RootElement.GetProperty("messages").EnumerateArray(),m=>m.GetProperty("role").GetString()=="system");
    }
    private sealed class Capture:HttpMessageHandler
    {
        public string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Body=await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK){Content=new StringContent("""{"message":{"role":"assistant","content":"{\"feedback\":\"合成評語\",\"annotations\":[]}"},"response":"{\"feedback\":\"合成評語\",\"annotations\":[]}","done_reason":"stop"}""")};
        }
    }
}
