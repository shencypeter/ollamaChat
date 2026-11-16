using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using System.Text;

namespace AiTeachingAssistant.Tests;

// These tests verify the deterministic prompt/catalog contract, not model compliance.
public sealed class CaseChatPromptAlignmentTests
{
    [Theory]
    [InlineData(AnalysisStage.Premorbid, "A1h", "A2c", "病前")]
    [InlineData(AnalysisStage.IllnessCourse, "B1d", "B2c", "最低完整GCS")]
    [InlineData(AnalysisStage.FunctionalAssessment, "C6", "C5", "效度")]
    [InlineData(AnalysisStage.Psychosocial, "D3b", "D4", "聯絡人")]
    public void EachStageCarriesOperationalProfessorRules(AnalysisStage stage, string code, string distinction, string concept)
    {
        var prompt = CaseChatPrompts.Get(stage);
        Assert.StartsWith("本次作答階段："+AnalysisStageCatalog.Get(stage).Label,prompt);
        Assert.Contains("不能只稱讚",prompt);
        Assert.Contains("必要背景引用",prompt);
        Assert.Contains(code, prompt);
        Assert.Contains(distinction, prompt);
        Assert.Contains(concept, prompt);
        Assert.Contains("已掌握的部分", prompt);
        Assert.Contains("可能遺漏的資料", prompt);
        Assert.Contains("需要更具體的地方", prompt);
        Assert.Contains("可能分類錯置的內容", prompt);
        Assert.Contains("修改建議", prompt);
        Assert.Contains("無法核實案例事實", prompt);
        Assert.Contains("學生作答不是案例事實來源", prompt);
        Assert.Contains("UTF-16", prompt);
        Assert.Contains("end exclusive", prompt);
        Assert.Contains("最新一則 user", prompt);
        Assert.Contains("occurrence", prompt);
        Assert.Contains("不要輸出start/end", prompt);
        Assert.Contains("unverified", prompt);
        Assert.Contains("unsupported", prompt);
        Assert.Contains("misplaced", prompt);
        Assert.Contains("annotations", prompt);
        Assert.Contains("不要直接替學生完成答案", prompt);
        Assert.DoesNotContain("55歲", prompt);
        Assert.DoesNotContain("9/30", prompt);
        Assert.DoesNotContain("\"feedback\":\"繁體中文教學回饋\"", prompt);
        Assert.DoesNotContain("\"text\":\"精確原文\"", prompt);
        Assert.True(Encoding.UTF8.GetByteCount(prompt) <= 6500, "Stage prompt must leave room in the existing 12KB conversation budget.");
        Assert.DoesNotContain("annotations", CaseChatPrompts.TeachingInstructions(stage));
    }

    [Fact]
    public void RawRemainsOutsideFourPhasePromptContract()
    {
        Assert.Throws<ArgumentException>(() => CaseChatPrompts.Get(AnalysisStage.GeneralChat));
        Assert.Throws<ArgumentOutOfRangeException>(() => CaseChatPrompts.Get((AnalysisStage)999));
        Assert.NotEqual("legacy-66b6795-chat-v1", CaseChatPrompts.Version);
    }

    [Fact]
    public void CatalogExposesExactReusableAllowlistAndRejectsExcludedOrInventedCodes()
    {
        var entries = CaseCodingCatalog.All;
        var codes = entries.Select(x => x.Code).ToArray();
        var expected = new[]
        {
            "A1", "A1a", "A1b", "A1c", "A1d", "A1e", "A1f", "A1g", "A1h", "A2", "A2a", "A2b", "A3", "A4",
            "B1", "B1a", "B1b", "B1c", "B1d", "B2", "B2a", "B2b", "B3", "B4", "B5", "B6", "B7", "B7a", "B7b", "B7c",
            "C1", "C2", "C3", "C4", "C5", "C6", "D1", "D2", "D3", "D3a", "D3b", "D4", "D5"
        };
        Assert.Equal(expected.Order(), codes.Order());
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        foreach (var entry in entries)
        {
            Assert.Equal(entry.Code[..1], entry.Phase);
            Assert.False(string.IsNullOrWhiteSpace(entry.Label));
            Assert.True(CaseCodingCatalog.IsValid(entry.Code));
        }
        foreach (var invalid in new[] { "A2c", "B2c", "C5a", "A4a", "E1", "a1a", " A1a", "A1a ", "", "B8" })
            Assert.False(CaseCodingCatalog.IsValid(invalid));
        Assert.False(CaseCodingCatalog.IsValid(null!));
    }
}
