using System.Text.Json;
using System.Text.RegularExpressions;
using AiTeachingAssistant.Models;

namespace AiTeachingAssistant.Services;

public static class AssessmentResultParser
{
    public static AssessmentResult Parse(string content, bool compare, bool caseAvailable)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            Fields(doc.RootElement, "evaluations", "comparisonFeedback");
            var evaluations = doc.RootElement.GetProperty("evaluations");
            if (evaluations.ValueKind != JsonValueKind.Array || evaluations.GetArrayLength() != (compare ? 2 : 1)) throw new JsonException();
            foreach (var e in evaluations.EnumerateArray())
            {
                Fields(e, "version", "caseEvidenceAvailable", "supportedClaims", "partiallySupportedClaims", "unsupportedClaims", "unverifiedClaims", "importantOmissions", "reasoningQuality", "score", "justification");
                foreach (var key in new[] { "supportedClaims", "partiallySupportedClaims", "unsupportedClaims", "unverifiedClaims", "importantOmissions" })
                {
                    var findings = e.GetProperty(key);
                    if (findings.ValueKind != JsonValueKind.Array) throw new JsonException();
                    foreach (var f in findings.EnumerateArray())
                    {
                        Fields(f, "claim", "evidence", "explanation");
                        foreach (var field in f.EnumerateObject()) NonEmpty(field.Value);
                    }
                }
                NonEmpty(e.GetProperty("reasoningQuality"));
                NonEmpty(e.GetProperty("justification"));
            }
            var result = JsonSerializer.Deserialize<AssessmentResult>(content, AssessmentContract.JsonOptions)!;
            var expected = compare ? new[] { "before", "after" } : new[] { "current" };
            if (!result.Evaluations.Select(e => e.Version).Order().SequenceEqual(expected.Order()) || result.ComparisonFeedback is null ||
                (compare && string.IsNullOrWhiteSpace(result.ComparisonFeedback)) || (!compare && result.ComparisonFeedback != "")) throw new JsonException();
            foreach (var e in result.Evaluations)
            {
                if (e.Score is < 1 or > 4 || e.CaseEvidenceAvailable != caseAvailable) throw new JsonException();
                if (!caseAvailable && (e.SupportedClaims.Count != 0 || e.PartiallySupportedClaims.Count != 0 || e.ImportantOmissions.Count != 0)) throw new JsonException();
            }
            // This validates the contract, not the clinical accuracy or semantic quality of the grade.
            return result;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException)
        {
            throw new InvalidOperationException("AI 評閱結果不符合證據與評分格式，請重新評閱。", e);
        }
    }
    private static void Fields(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new JsonException();
        var actual = value.EnumerateObject().Select(p => p.Name).ToArray();
        if (actual.Length != names.Length || !actual.Order().SequenceEqual(names.Order())) throw new JsonException();
    }
    private static void NonEmpty(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())) throw new JsonException();
    }
    public static AssessmentScores Scores(AssessmentResult result) => result.Evaluations.Count == 1
        ? new(result.Evaluations[0].Score, null, null)
        : new(null, result.Evaluations.Single(e => e.Version == "before").Score, result.Evaluations.Single(e => e.Version == "after").Score);
    public static string ToMarkdown(AssessmentResult result)
    {
        string Literal(string text) => Regex.Replace(text, @"([\\`*_{}\[\]()<>#+.!|~-])", @"\$1").Replace("\r", " ").Replace("\n", " ");
        var sections = result.Evaluations.OrderBy(e => e.Version == "before" ? 0 : 1).Select(e =>
        {
            string Findings(string label, IReadOnlyList<AssessmentFinding> findings) => $"### {label}\n\n" +
                (findings.Count == 0 ? "無\n" : string.Join("\n", findings.Select(f => $"- {Literal(f.Claim)}｜依據：{Literal(f.Evidence)}｜{Literal(f.Explanation)}"))) + "\n";
            return $"## {(e.Version == "before" ? "修改前" : e.Version == "after" ? "修改後" : "本次作答")}\n\n" +
                (e.CaseEvidenceAvailable ? "依本次案例資料評閱。\n\n" : "未提供原始案例，事實與完整性未核實；分數為暫定。\n\n") +
                Findings("有據支持", e.SupportedClaims) + Findings("部分支持", e.PartiallySupportedClaims) +
                Findings("缺乏支持或矛盾", e.UnsupportedClaims) + Findings("未核實的事實轉述", e.UnverifiedClaims) +
                Findings("重要遺漏", e.ImportantOmissions) + $"### 臨床推理品質\n\n{Literal(e.ReasoningQuality)}\n\n### 評分理由\n\n{Literal(e.Justification)}\n";
        });
        return string.Join("\n", sections) + (result.ComparisonFeedback.Length > 0 ? $"\n## 版本比較\n\n{Literal(result.ComparisonFeedback)}" : "");
    }
}
