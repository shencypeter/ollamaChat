using System.Text.Json;
using AiTeachingAssistant.Models;
namespace AiTeachingAssistant.Services;

// The same reviewed-source note and writing directions are bundled into Vue.
// These directions never add coding rules or constitute case evidence.
public static class CaseWritingGuidance
{
    private static readonly JsonDocument Document=Load();
    private static JsonDocument Load()
    {
        using var stream=typeof(CaseWritingGuidance).Assembly.GetManifestResourceStream("AiTeachingAssistant.Configuration.CaseAnalysis.stage-guidance.json")
            ??throw new InvalidOperationException("Missing phase writing guidance.");
        return JsonDocument.Parse(stream);
    }
    public static string ReviewRequirements(AnalysisStage? stage)
    {
        if(stage is null||stage==AnalysisStage.GeneralChat)return "";
        var key=AnalysisStageCatalog.Get(stage.Value).Key;
        var guide=Document.RootElement.GetProperty("stages").EnumerateArray().Single(g=>g.GetProperty("key").GetString()==key);
        return "\n階段指引（尚待與原始教材核對；不是案例事實或必填清單）："
            +guide.GetProperty("coreQuestion").GetString()+(stage==AnalysisStage.IllnessCourse?"":" "+guide.GetProperty("description").GetString())
            +" 參考面向："+string.Join("、",guide.GetProperty("topics").EnumerateArray().Select(t=>t.GetString()))
            +"。撰寫重點："+guide.GetProperty("writingFocus").GetString()
            +" 不新增分類碼或覆寫課內規則；不能據此判定學生遺漏。";
    }
}
