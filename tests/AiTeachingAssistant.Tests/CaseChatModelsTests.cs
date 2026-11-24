using System.Collections.Concurrent;
using System.Text.Json;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
namespace AiTeachingAssistant.Tests;

public sealed class CaseChatModelsTests
{
    [Fact] public async Task NamedProfilesUseIndependentClientsAndOwnSettingsWithExpandedInputLimits()
    {
        var transport=new Clients();using var models=new CaseChatModels(new Host(),transport,NullLogger<OllamaClient>.Instance,new OllamaRequestInspection(new HttpContextAccessor()));
        var llama=models.Select("llama3");var omni=models.Select("qwen25-omni");
        Assert.NotSame(llama.Choice.Options,omni.Choice.Options);Assert.Equal(48000,llama.Choice.Limits.MaxContextBytes);Assert.Equal(48000,omni.Choice.Limits.MaxContextBytes);Assert.Equal("chat",llama.Choice.Options.ConversationTransport);Assert.Equal("qwen-chatml-generate",omni.Choice.Options.ConversationTransport);
        await Task.WhenAll(llama.Provider.ConversationAsync([new("user","合成Llama請求")],default),omni.Provider.ConversationAsync([new("user","合成Omni請求")],default));
        Assert.Equal(new[]{llama.Choice.Options.Model,omni.Choice.Options.Model}.Order(),transport.Requests.Select(x=>x.Model).Order());Assert.Equal(2,transport.Requests.Count);Assert.All(transport.Requests,r=>Assert.Equal(1,r.Messages));
        Assert.Equal(4,models.Profiles.Count);var original=llama.Choice.Options.Model;llama.Choice.Options.Model="local test mutation";Assert.Equal(original,models.Select("llama3").Choice.Options.Model);
    }
    [Fact] public void UnknownProfileIsRejectedBeforeCreatingAnyClient()
    {
        var transport=new Clients();using var models=new CaseChatModels(new Host(),transport,NullLogger<OllamaClient>.Instance,new OllamaRequestInspection(new HttpContextAccessor()));
        Assert.Equal(400,Assert.Throws<CaseChatException>(()=>models.Select("../../appsettings.Local")).StatusCode);Assert.Equal(0,transport.Created);
    }
    private sealed class Clients:IHttpClientFactory
    {
        public ConcurrentBag<(string Model,int Messages)> Requests=[];public int Created;
        public HttpClient CreateClient(string name){Interlocked.Increment(ref Created);return new(new Capture(Requests));}
    }
    private sealed class Capture(ConcurrentBag<(string Model,int Messages)> requests):HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));var generate=json.RootElement.TryGetProperty("prompt",out var prompt);requests.Add((json.RootElement.GetProperty("model").GetString()!,generate?System.Text.RegularExpressions.Regex.Matches(prompt.GetString()!,"<\\|im_end\\|>").Count:json.RootElement.GetProperty("messages").GetArrayLength()));return new(System.Net.HttpStatusCode.OK){Content=new StringContent(generate?"{\"response\":\"合成回覆\",\"done_reason\":\"stop\"}":"{\"message\":{\"content\":\"合成回覆\"},\"done_reason\":\"stop\"}")};}
    }
    private sealed class Host:IHostEnvironment
    {
        public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="test";public string ContentRootPath{get;set;}=AppContext.BaseDirectory;public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
}
