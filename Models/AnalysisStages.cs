namespace AiTeachingAssistant.Models;

public enum AnalysisStage
{
    Premorbid,
    IllnessCourse,
    FunctionalAssessment,
    Psychosocial,
    GeneralChat
}

public sealed record AnalysisStageOption(
    AnalysisStage Stage,
    string Key,
    string Label,
    string Placeholder);

public static class AnalysisStageCatalog
{
    public static IReadOnlyList<AnalysisStageOption> All { get; } =
    [
        new(AnalysisStage.Premorbid, "premorbid", "病前功能", "請輸入個案病前功能分析…"),
        new(AnalysisStage.IllnessCourse, "illnessCourse", "疾病病程", "請輸入個案疾病病程分析…"),
        new(AnalysisStage.FunctionalAssessment, "functionalAssessment", "功能評估結果", "請輸入個案功能評估結果分析…"),
        new(AnalysisStage.Psychosocial, "psychosocial", "心理社會條件", "請輸入個案心理社會條件分析…"),
        new(AnalysisStage.GeneralChat, "generalChat", "一般對話", "想和 AI 聊些什麼？")
    ];

    public static AnalysisStageOption Get(AnalysisStage stage) =>
        All.First(option => option.Stage == stage);

    public static bool TryParse(string? key, out AnalysisStage stage)
    {
        var option = All.FirstOrDefault(option =>
            option.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        stage = option?.Stage ?? AnalysisStage.Premorbid;
        return option is not null;
    }
}
