using AiTeachingAssistant.Options;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace AiTeachingAssistant.Services;

public sealed record CaseChatModelProfile(string Key,string Label,string ModelName);
public sealed record CaseChatModelChoice(string Profile,OllamaOptions Options,CaseChatOptions Limits);
public sealed record CaseChatModelRuntime(CaseChatModelChoice Choice,IOllamaClient Provider,bool IsOverride=true);
public interface ICaseChatModels
{
    IReadOnlyList<CaseChatModelProfile> Profiles {get;}
    CaseChatModelRuntime Select(string profile);
    CaseChatModelRuntime FromChoice(CaseChatModelChoice choice);
}

// Scoped clients share transport infrastructure, never mutable global options or
// another request's inspection/authorization state. Raw stores only Choice.
public sealed class CaseChatModels(IHostEnvironment environment,IHttpClientFactory clients,ILogger<OllamaClient> logger,OllamaRequestInspection inspection):ICaseChatModels,IDisposable
{
    private readonly List<HttpClient> owned=[];
    public IReadOnlyList<CaseChatModelProfile> Profiles=>OllamaOptions.ProfileNames.Select(key=>{
        var choice=Load(key);return new CaseChatModelProfile(key,key switch{
            "llama3"=>"IAN02 · Llama3.1 8B","qwen"=>"IAN02 · Qwen3 8B","breeze"=>"Taipei · Breeze 7B","qwen25-omni"=>"Taipei · Qwen2.5 Omni 7B",_=>key},choice.Options.Model);
    }).ToArray();
    private CaseChatModelChoice Load(string profile)
    {
        if(!OllamaOptions.ProfileNames.Contains(profile))throw new CaseChatException(400,"請選擇已設定的模型，不能指定任意模型或主機。");
        try{
            // Start fresh: never inherit the currently active profile's overlay.
            // Retain the project's base -> profile -> local -> env precedence.
            var configuration=new ConfigurationBuilder().SetBasePath(environment.ContentRootPath)
                .AddJsonFile("appsettings.json").AddJsonFile($"appsettings.{environment.EnvironmentName}.json",true)
                .AddJsonFile($"Configuration/Ollama/{profile}.json").AddJsonFile("appsettings.Local.json",true).AddEnvironmentVariables().Build();
            var options=configuration.GetSection("Ollama").Get<OllamaOptions>()??new();options.Profile=profile;
            var limits=configuration.GetSection("CaseChat").Get<CaseChatOptions>()??new();
            Validator.ValidateObject(options,new ValidationContext(options),true);Validator.ValidateObject(limits,new ValidationContext(limits),true);
            if(!Uri.TryCreate(options.BaseUrl,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https"))throw new ValidationException();
            return new(profile,options,limits);
        }catch(CaseChatException){throw;}
        catch(Exception){throw new CaseChatException(503,"選定模型的設定暫時無法使用。",new("model_configuration","無法載入或驗證選定的伺服器模型設定；未呼叫模型。"));}
    }
    public CaseChatModelRuntime Select(string profile)=>FromChoice(Load(profile));
    public CaseChatModelRuntime FromChoice(CaseChatModelChoice choice)
    {
        var http=clients.CreateClient("case-chat-profiles");http.BaseAddress=new Uri(choice.Options.BaseUrl.TrimEnd('/')+"/");http.Timeout=TimeSpan.FromSeconds(choice.Options.TimeoutSeconds);owned.Add(http);
        return new(choice,new OllamaClient(http,Microsoft.Extensions.Options.Options.Create(choice.Options),logger,inspection:inspection));
    }
    public void Dispose(){foreach(var client in owned)client.Dispose();owned.Clear();}
}
