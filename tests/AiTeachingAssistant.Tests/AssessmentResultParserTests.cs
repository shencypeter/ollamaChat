using System.Text.Json;
using System.Text.Json.Nodes;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;

namespace AiTeachingAssistant.Tests;

public class AssessmentResultParserTests
{
    public static JsonObject Valid(bool available = true, string version = "current", int score = 3) => new()
    {
        ["evaluations"] = new JsonArray(new JsonObject
        {
            ["version"] = version, ["caseEvidenceAvailable"] = available,
            ["supportedClaims"] = new JsonArray(), ["partiallySupportedClaims"] = new JsonArray(),
            ["unsupportedClaims"] = new JsonArray(), ["unverifiedClaims"] = new JsonArray(),
            ["importantOmissions"] = new JsonArray(), ["reasoningQuality"] = "推論範圍適當", ["score"] = score,
            ["justification"] = "核心分析正確，有少量細節遺漏。"
        }),
        ["comparisonFeedback"] = ""
    };

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void ValidScoresArePreservedWithoutClaimingGradingAccuracy(int score)
    {
        var result = AssessmentResultParser.Parse(Valid(score: score).ToJsonString(), false, true);
        Assert.Equal(score, AssessmentResultParser.Scores(result).Score);
        Assert.Contains("評分理由", AssessmentResultParser.ToMarkdown(result));
    }

    [Theory]
    [InlineData("missing")] [InlineData("null")] [InlineData("empty")]
    [InlineData("range")] [InlineData("fraction")] [InlineData("stringScore")]
    [InlineData("extra")] [InlineData("version")] [InlineData("caseFlag")]
    [InlineData("finding")] [InlineData("duplicate")]
    public void InvalidContractsAreRejected(string mutation)
    {
        var root = Valid(); var e = root["evaluations"]![0]!.AsObject();
        switch (mutation)
        {
            case "missing": e.Remove("unsupportedClaims"); break;
            case "null": e["importantOmissions"] = null; break;
            case "empty": e["justification"] = " "; break;
            case "range": e["score"] = 5; break;
            case "fraction": e["score"] = 2.5; break;
            case "stringScore": e["score"] = "3"; break;
            case "extra": e["other"] = "x"; break;
            case "version": e["version"] = "before"; break;
            case "caseFlag": e["caseEvidenceAvailable"] = false; break;
            case "finding": e["supportedClaims"] = new JsonArray(new JsonObject { ["claim"] = "x" }); break;
        }
        var json = root.ToJsonString();
        if (mutation == "duplicate") json = json.Replace("\"score\":3", "\"score\":3,\"score\":4");
        Assert.Throws<InvalidOperationException>(() => AssessmentResultParser.Parse(json, false, true));
    }

    [Theory]
    [InlineData("supportedClaims")] [InlineData("partiallySupportedClaims")] [InlineData("importantOmissions")]
    public void MissingCaseCannotBeTreatedAsVerifiedEvidence(string field)
    {
        var root = Valid(false);
        root["evaluations"]![0]![field] = new JsonArray(new JsonObject { ["claim"] = "x", ["evidence"] = "y", ["explanation"] = "z" });
        Assert.Throws<InvalidOperationException>(() => AssessmentResultParser.Parse(root.ToJsonString(), false, false));
    }

    [Fact]
    public void MissingCaseAllowsUnverifiedClaimsAndUnsupportedInferences()
    {
        var root = Valid(false);
        root["evaluations"]![0]!["unverifiedClaims"] = new JsonArray(new JsonObject { ["claim"] = "轉述", ["evidence"] = "未提供案例", ["explanation"] = "未核實" });
        var result = AssessmentResultParser.Parse(root.ToJsonString(), false, false);
        Assert.Single(result.Evaluations[0].UnverifiedClaims);
        Assert.Contains("暫定", AssessmentResultParser.ToMarkdown(result));
    }

    [Theory]
    [InlineData(2, 4)] [InlineData(3, 3)] [InlineData(4, 1)]
    public void ComparisonKeepsIndependentGrades(int before, int after)
    {
        var root = Valid(version: "before", score: before);
        root["evaluations"]!.AsArray().Add(Valid(version: "after", score: after)["evaluations"]![0]!.DeepClone());
        root["comparisonFeedback"] = "兩版有變化";
        var result = AssessmentResultParser.Parse(root.ToJsonString(), true, true);
        Assert.Equal(new AssessmentScores(null, before, after), AssessmentResultParser.Scores(result));
    }

    [Theory]
    [InlineData("[]")] [InlineData("not json")] [InlineData("{\"evaluations\":null,\"comparisonFeedback\":\"\"}")]
    [InlineData("```json\n{}\n```")]
    public void MalformedResultsAreRejected(string content) =>
        Assert.Throws<InvalidOperationException>(() => AssessmentResultParser.Parse(content, false, false));
}
