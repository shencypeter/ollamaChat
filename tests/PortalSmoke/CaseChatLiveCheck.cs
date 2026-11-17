using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class CaseChatLiveCheck
{
    public static async Task Run(string root)
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development")throw new InvalidOperationException("Synthetic live check requires Development.");
        var profile=Environment.GetEnvironmentVariable("Ollama__Profile")??"breeze";
        if(profile is not ("breeze" or "llama3" or "qwen"))throw new InvalidOperationException("Unknown profile.");
        var config=new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json").AddJsonFile($"Configuration/Ollama/{profile}.json").AddJsonFile("appsettings.Local.json",true).AddEnvironmentVariables().Build();
        var settings=config.GetSection("Ollama").Get<OllamaOptions>()!;var limits=config.GetSection("CaseChat").Get<CaseChatOptions>()??new();
        using var http=new HttpClient{BaseAddress=new Uri(settings.BaseUrl.TrimEnd('/')+"/"),Timeout=TimeSpan.FromSeconds(settings.TimeoutSeconds)};
        var provider=new OllamaClient(http,Options.Create(settings),NullLogger<OllamaClient>.Instance);
        using var memory=new RawChatSessions(TimeProvider.System,Options.Create(limits),Options.Create(settings));var course=Guid.NewGuid();var actor=Guid.NewGuid();var id=memory.Create(course,actor);
        try
        {
            var raw=await memory.Send(course,actor,id,"Synthetic transport check. Reply with one short sentence: ready.",provider,default);
            if(raw.Messages.Count!=2||raw.Messages[1].Metrics?.AvailableFields?.Contains("roundTripMilliseconds") is not true)throw new Exception("Raw reply/performance missing.");Console.WriteLine("Live Raw first turn and per-reply performance received; content omitted.");
            raw=await memory.Send(course,actor,id,"This is the second synthetic turn. Reply with one short sentence.",provider,default);
            if(raw.Messages.Count!=4)throw new Exception("Raw context missing.");Console.WriteLine("Live Raw second turn received; four messages retained only in memory.");
            var stage=await provider.CaseFeedbackAsync([new("system",CaseChatPrompts.Get(AnalysisStage.Premorbid)),new("user","這是合成傳輸檢查，尚未提供案例。請用一句話說明需要去識別化案例資料，不評分。")],AnalysisStage.Premorbid,default);
            if(string.IsNullOrWhiteSpace(CaseAnnotationContract.Parse(stage.Content,"這是合成傳輸檢查，尚未提供案例。請用一句話說明需要去識別化案例資料，不評分。").Feedback)||stage.Thinking is not null)throw new Exception("Stage feedback contract failed.");
            Console.WriteLine("Live stage prompt received structured feedback on the same provider/model/sampling; content omitted, no DB history or grades created.");
        }
        finally{await memory.ClearAll(actor,default);}
    }
}
