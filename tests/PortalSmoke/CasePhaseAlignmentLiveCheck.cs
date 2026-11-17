using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class CasePhaseAlignmentLiveCheck
{
    public static async Task Run(string root)
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development")throw new InvalidOperationException("Synthetic checks require Development.");
        var baseline=new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json").AddEnvironmentVariables().Build();
        var profile=baseline["Ollama:Profile"]??"llama3";
        if(!AiTeachingAssistant.Options.OllamaOptions.ProfileNames.Contains(profile))throw new InvalidOperationException("Unknown model profile.");
        var config=new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json").AddJsonFile($"Configuration/Ollama/{profile}.json").AddJsonFile("appsettings.Local.json",true).AddEnvironmentVariables().Build();
        var options=config.GetSection("Ollama").Get<OllamaOptions>()!;
        using var http=new HttpClient{BaseAddress=new Uri(options.BaseUrl.TrimEnd('/')+"/"),Timeout=TimeSpan.FromSeconds(options.TimeoutSeconds)};
        var provider=new OllamaClient(http,Options.Create(options),NullLogger<OllamaClient>.Instance);
        var inputs=new[]{
            (Id:"P01",Scenario:"A main answer submitted in B",Text:"發病前可自行搭乘公車、購物與管理帳務，固定每週快走，沒有吸菸習慣。"),
            (Id:"P02",Scenario:"B main answer submitted in B",Text:"1月3日因跌倒受傷，當日安排頭部影像檢查，1月4日接受手術，1月8日轉入復健病房。"),
            (Id:"P03",Scenario:"B answer with necessary A context",Text:"病前可自行購物及搭車；1月3日跌倒受傷，1月4日接受手術，這裡的病前描述僅作功能變化背景。")
        };
        var results=new List<object>();
        foreach(var input in inputs)
        {
            try
            {
                var response=await provider.CaseFeedbackAsync([new("system",CaseChatPrompts.Get(AnalysisStage.IllnessCourse)),new("user",input.Text)],AnalysisStage.IllnessCourse,default);
                var parsed=CaseAnnotationContract.Parse(response.Content,input.Text);
                using var json=JsonDocument.Parse(response.Content);var proposed=json.RootElement.GetProperty("annotations").GetArrayLength();
                results.Add(new{id=input.Id,input.Scenario,model=options.Model,promptVersion=CaseChatPrompts.Version,currentPhase="B",feedback=parsed.Feedback,annotations=parsed.Annotations,proposedAnnotations=proposed,rejectedAnnotations=proposed-parsed.Annotations.Count,metrics=ChatPerformanceMetrics.Clean(response.Metrics),transport="passed"});
                Console.WriteLine($"{input.Id} synthetic phase B reply parsed; accepted={parsed.Annotations.Count}, rejected={proposed-parsed.Annotations.Count}; content omitted.");
            }
            catch(Exception)
            {
                results.Add(new{id=input.Id,input.Scenario,transport="failed"});Console.WriteLine(input.Id+" synthetic phase check failed; response body omitted.");
            }
            await File.WriteAllTextAsync(Path.Combine(root,".codex-build","phase-match-live-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
        }
        Console.WriteLine("Synthetic phase checks finished; inspect ignored results for behavioral quality. No DB writes.");
    }
}
