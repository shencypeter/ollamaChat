namespace AiTeachingAssistant.Services;

public sealed record CaseCodingDefinition(string Code, string Label, string Phase,
    [property:System.Text.Json.Serialization.JsonIgnore] string? PromptLabel = null);

/// <summary>Reusable classification vocabulary; recognition never certifies clinical correctness.</summary>
public static class CaseCodingCatalog
{
    public static IReadOnlyList<CaseCodingDefinition> All { get; } = Array.AsReadOnly<CaseCodingDefinition>(
    [
        new("A1", "人口學資料", "A"),
        new("A1a", "年齡", "A"), new("A1b", "性別", "A"),
        new("A1c", "教育程度", "A"), new("A1d", "職業", "A"),
        new("A1e", "疾病史", "A"), new("A1f", "利手", "A"),
        new("A1g", "慣用語言", "A"), new("A1h", "家族史", "A"),
        new("A2", "照護需求程度", "A"), new("A2a", "日常生活活動（ADLs）", "A"),
        new("A2b", "家事性活動（IADLs）", "A"),
        new("A3", "健康行為", "A"), new("A4", "物質使用情形", "A"),
        new("B1", "受傷資訊", "B"), new("B1a", "受傷年齡", "B"),
        new("B1b", "受傷時間", "B"), new("B1c", "受傷機制", "B"),
        new("B1d", "與工作相關（職災）", "B"),
        new("B2", "疾病嚴重度", "B"), new("B2a", "昏迷指數（最低完整GCS）", "B"),
        new("B2b", "失去意識時間（LOC）", "B"),
        new("B3", "影像學檢查、病灶", "B"), new("B4", "疾病病程", "B"),
        new("B5", "手術資訊", "B"), new("B6", "診斷", "B"),
        new("B7", "治療計畫", "B"), new("B7a", "醫療治療", "B"),
        new("B7b", "復健計畫", "B"), new("B7c", "出院準備服務", "B"),
        new("C1", "意識狀態與心智功能", "C"), new("C2", "Muscle Power（肌力與相關動作檢查）", "C", "Muscle Power"),
        new("C3", "吞嚥與構音功能", "C"), new("C4", "知覺系統", "C"),
        new("C5", "日常生活活動", "C"), new("C6", "神經與生理功能", "C"),
        new("D1", "婚姻狀態", "D"), new("D2", "住家條件", "D"),
        new("D3", "家庭支持", "D"), new("D3a", "同住成員", "D"),
        new("D3b", "主要照顧者", "D"), new("D4", "其他支持資源", "D"),
        new("D5", "宗教信仰", "D")
    ]);

    private static readonly HashSet<string> Codes = All.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

    public static bool IsValid(string code) => code is not null && Codes.Contains(code);
}
