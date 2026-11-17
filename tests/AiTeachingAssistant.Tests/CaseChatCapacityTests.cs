using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Configuration;

namespace AiTeachingAssistant.Tests;

public sealed class CaseChatCapacityTests
{
    [Fact] public void ConfiguredOmniAcceptsFiveParagraphChineseProseOf2125CharactersWithoutHistory()
    {
        var cfg=new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory,"Configuration","Ollama","qwen25-omni.json")).Build();
        var model=cfg.GetSection("Ollama").Get<OllamaOptions>()!;
        var limits=cfg.GetSection("CaseChat").Get<CaseChatOptions>()??new();
        var prose=string.Join("\n\n",Enumerable.Repeat(new string('測',423),5))+"測測";
        Assert.Equal(2125,prose.Length);
        CaseChatBudget.EnsureFits([new("system",CaseChatPrompts.Get(AnalysisStage.IllnessCourse)),new("user",prose)],limits,model,model.CaseFeedbackMaxTokens);
    }
    [Fact] public void LongAsciiStillCannotExceedContextAndByteSafetyLimits()
    {
        var model=new OllamaOptions();var limits=new CaseChatOptions();
        Assert.Throws<CaseChatException>(()=>CaseChatBudget.EnsureFits([new("user",new string('X',20000))],limits,model));
    }
    [Fact] public void OmniStillRejectsTokenOverflowAndByteOverflowIndependently()
    {
        var model=new OllamaOptions{CjkContextTokensPerCharacter=2,ContextTokenOverhead=512};
        var limits=new CaseChatOptions{MaxContextBytes=24000};
        Assert.Equal("context_limit",Assert.Throws<CaseChatException>(()=>CaseChatBudget.EnsureFits([new("user",new string('X',15000))],limits,model)).Diagnostic!.Code);
        limits.MaxContextBytes=512;
        Assert.Throws<CaseChatException>(()=>CaseChatBudget.EnsureFits([new("user",new string('測',200))],limits,model));
    }
}
