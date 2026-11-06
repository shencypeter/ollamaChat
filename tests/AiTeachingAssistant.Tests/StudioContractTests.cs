using System.Net;
using System.Text.Json;
using AiTeachingAssistant.Controllers;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;
namespace AiTeachingAssistant.Tests;
public class StudioContractTests
{
    private const string Valid="""{"summary":"文字回饋","supportedPoints":[],"improvements":[],"nextSteps":["核對案例"]}""";
    [Fact]public void TextFeedbackHasNoScoreAndRejectsUnexpectedPrivateFields(){Assert.Equal("文字回饋",StudioFeedbackContract.Parse(Valid).Summary);Assert.Throws<InvalidOperationException>(()=>StudioFeedbackContract.Parse(Valid.Replace("\"summary\"","\"score\":4,\"summary\"")));Assert.Throws<InvalidOperationException>(()=>StudioFeedbackContract.Parse(Valid.Replace("\"summary\"","\"thinking\":\"private\",\"summary\"")));}
    [Theory][InlineData("{}")] [InlineData("not json")] [InlineData("{\"summary\":\"x\"}")]
    public void InvalidFeedbackIsNotInventedOrDisplayed(string raw)=>Assert.Throws<InvalidOperationException>(()=>StudioFeedbackContract.Parse(raw));
    [Theory][InlineData("=SUM(A1)")] [InlineData(" +cmd")] [InlineData("-1+2")] [InlineData("@function")]
    public void CsvNeutralizesFormulaCells(string cell)=>Assert.StartsWith("\"'",StudioTeachingController.CsvCell(cell));
    [Fact]public void CsvEscapesQuotesAndPreservesPlainText()=>Assert.Equal("\"合成\"\"案例\"",StudioTeachingController.CsvCell("合成\"案例"));
    [Theory][InlineData("breeze",2)][InlineData("llama3",4)][InlineData("qwen",4)]
    public async Task TextFeedbackPreservesEachModelProfileWithoutAssessmentScores(string profile,int count)
    {
        using var config=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"Configuration","Ollama",profile+".json")));
        var settings=config.RootElement.GetProperty("Ollama").Deserialize<OllamaOptions>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        var capture=new Capture();var client=new OllamaClient(new HttpClient(capture){BaseAddress=new Uri(settings.BaseUrl)},Microsoft.Extensions.Options.Options.Create(settings),NullLogger<OllamaClient>.Instance);
        var c=new StudioCase(Guid.NewGuid(),Guid.NewGuid(),"合成課程",Guid.NewGuid(),1,"合成案例","合成原文",StudioFeedbackContract.Stages.ToDictionary(s=>s,_=>"結構提示"),StudioFeedbackContract.Stages.ToDictionary(s=>s,_=>"內部重點"),true,null);
        var answer=new StudioAnswer(Guid.NewGuid(),"premorbid",1,"合成作答",DateTimeOffset.UtcNow);
        await client.FeedbackAsync(StudioFeedbackContract.Build(c,answer,"回饋模板"),default);
        using var wire=JsonDocument.Parse(capture.Body!);var payload=wire.RootElement;Assert.Equal(count,payload.GetProperty("messages").GetArrayLength());Assert.True(payload.GetProperty("format").GetProperty("properties").TryGetProperty("summary",out _));Assert.False(payload.GetProperty("format").GetProperty("properties").TryGetProperty("score",out _));Assert.Equal(settings.Model,payload.GetProperty("model").GetString());Assert.Equal(42,payload.GetProperty("options").GetProperty("seed").GetInt32());
    }
    private sealed class Capture:HttpMessageHandler
    {
        public string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Body=await request.Content!.ReadAsStringAsync(ct);return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{message=new{role="assistant",content=Valid},eval_count=1}))};}
    }
}
