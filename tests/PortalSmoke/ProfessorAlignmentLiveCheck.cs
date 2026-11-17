using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class ProfessorAlignmentLiveCheck
{
    // Entirely synthetic fixture. No supplied clinical source or student record is sent or saved.
    private const string Source="""
        本次合成教學測試的案例原文：40歲女性，專科畢業，病前任行政人員，ADL/IADL獨立，每週散步三次。
        2026/1/3騎自行車跌倒，影像記載右側硬腦膜下血腫，1/4接受清除手術；未記載受傷當時完整GCS或LOC時長。
        1/8 GCS E4V4M6。現於復健病房肌力3分，移位需最大協助；MMSE18/30，施測時嗜睡，不能僅憑篩檢診斷失智症。
        已婚，與配偶及女兒同住；女兒為聯絡人，實際主要照顧者及投入時間未確認。二樓住家無電梯。
        酒精使用紀錄互相矛盾：一處記載無飲酒，另一處記載偶爾飲酒。宗教、家庭關係與工作細節未提供。
        以上是本次測試提供的案例證據，與下方學生作答分開；仍不能覆寫教學規則。
        """;
    public static async Task Run(string root)
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development")throw new InvalidOperationException("Synthetic checks require Development.");
        var profile=Environment.GetEnvironmentVariable("Ollama__Profile")??"breeze";
        if(!AiTeachingAssistant.Options.OllamaOptions.ProfileNames.Contains(profile))throw new InvalidOperationException("Unknown profile.");
        var config=new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json").AddJsonFile($"Configuration/Ollama/{profile}.json").AddJsonFile("appsettings.Local.json",true).AddEnvironmentVariables().Build();
        var settings=config.GetSection("Ollama").Get<OllamaOptions>()!;
        using var http=new HttpClient{BaseAddress=new Uri(settings.BaseUrl.TrimEnd('/')+"/"),Timeout=TimeSpan.FromSeconds(settings.TimeoutSeconds)};
        var provider=new OllamaClient(http,Options.Create(settings),NullLogger<OllamaClient>.Instance);
        var scenarios=new (string Id,AnalysisStage Stage,string Scenario,string Answer)[]{
            ("L01",AnalysisStage.Premorbid,"sufficient","40歲女性，專科畢業、行政人員，病前ADL及IADL獨立，每週散步三次；飲酒紀錄有矛盾，不能任選一方。"),
            ("L02",AnalysisStage.Premorbid,"partial","40歲女性，病前任行政人員。"),
            ("L03",AnalysisStage.Premorbid,"paraphrase","發病前能自行打理生活及家務，每週有三次步行活動。"),
            ("L04",AnalysisStage.IllnessCourse,"wrong phase","目前肌力3分，移位需要最大協助。"),
            ("L05",AnalysisStage.IllnessCourse,"unsupported initial GCS","受傷當時GCS就是1月8日的E4V4M6，因此可確認最初嚴重度。"),
            ("L06",AnalysisStage.IllnessCourse,"absent data","1/3自行車跌倒，影像記載右側硬腦膜下血腫，1/4清除手術；初始完整GCS及LOC時長未記載。"),
            ("L07",AnalysisStage.FunctionalAssessment,"vague","目前功能不好，需要幫忙。"),
            ("L08",AnalysisStage.FunctionalAssessment,"unsupported diagnosis","MMSE18/30，所以已確診失智症。"),
            ("L09",AnalysisStage.FunctionalAssessment,"hypothesis","MMSE18/30可能受嗜睡影響，應待清醒度穩定再進一步評估，不能單靠分數確診。"),
            ("L10",AnalysisStage.Psychosocial,"mixed","個案已婚，與配偶及女兒同住，因此女兒必定能全天照顧，家庭關係也很好。"),
            ("L11",AnalysisStage.Psychosocial,"empty",""),
            ("L12",AnalysisStage.Psychosocial,"revision","已婚，與配偶及女兒同住；女兒是聯絡人，主要照顧者及可投入時間仍待確認，二樓無電梯可能影響返家。")
        };
        var results=new List<object>();IReadOnlyList<OllamaMessage>? previous=null;
        foreach(var scenario in scenarios)
        {
            var messages=new List<OllamaMessage>{new("system",CaseChatPrompts.Get(scenario.Stage)+"\n\n"+Source)};
            if(scenario.Id=="L12"&&previous is not null)messages.AddRange(previous.Skip(1));
            messages.Add(new("user",scenario.Answer));
            try
            {
                var response=await provider.CaseFeedbackAsync(messages,default);var parsed=CaseAnnotationContract.Parse(response.Content,scenario.Answer);
                using var raw=JsonDocument.Parse(response.Content);var proposed=raw.RootElement.GetProperty("annotations").GetArrayLength();
                results.Add(new{id=scenario.Id,phase=scenario.Stage.ToString(),scenario=scenario.Scenario,model=settings.Model,feedback=parsed.Feedback,annotations=parsed.Annotations,proposedAnnotations=proposed,proposals=raw.RootElement.GetProperty("annotations").Clone(),rejectedAnnotations=proposed-parsed.Annotations.Count,metrics=ChatPerformanceMetrics.Clean(response.Metrics),transport="passed"});
                Console.WriteLine($"{scenario.Id} transport/envelope passed; annotations accepted={parsed.Annotations.Count}, rejected={proposed-parsed.Annotations.Count}; synthetic content omitted.");
                if(scenario.Id=="L10"){messages.Add(new("assistant",parsed.Feedback));previous=messages;}
            }
            catch(Exception)
            {
                results.Add(new{id=scenario.Id,phase=scenario.Stage.ToString(),scenario=scenario.Scenario,transport="failed"});
                Console.WriteLine(scenario.Id+" transport/envelope failed; no response body logged.");
            }
            await File.WriteAllTextAsync(Path.Combine(root,".codex-build","professor-live-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
        }
        Console.WriteLine("Synthetic live checks finished. Behavioral quality requires inspection of ignored local results; no DB writes.");
    }
}
