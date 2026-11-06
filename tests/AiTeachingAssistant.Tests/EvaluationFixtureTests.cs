using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;

namespace AiTeachingAssistant.Tests;

public class EvaluationFixtureTests
{
    [Fact]
    public void FixturesCoverFourLevelsAndStagesWithoutEnteringProductionPrompts()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "evaluation-fixtures.json")));
        var fixtures = document.RootElement.EnumerateArray().ToArray();
        foreach (var stage in AnalysisStageCatalog.All.Where(s => s.Stage != AnalysisStage.GeneralChat))
        {
            var stageFixtures = fixtures.Where(f => f.GetProperty("stage").GetString() == stage.Key &&
                !f.TryGetProperty("equivalentTo", out _) && !f.TryGetProperty("historicalPreviouslyExposed", out _)).ToArray();
            Assert.Equal(new[] { 1, 2, 3, 4 }, stageFixtures.Select(f => f.GetProperty("provisionalTarget").GetInt32()).Order());
            foreach (var f in fixtures)
            {
                Assert.False(f.GetProperty("humanReviewed").GetBoolean());
                Assert.DoesNotContain(f.GetProperty("answer").GetString()!, AnalysisStageInstructions.Get(stage.Stage));
            }
        }
    }

    [Fact]
    public void RuntimeContextIncludesOnlyAnswerAndCaseNeverEvaluationLabels()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "evaluation-fixtures.json")));
        foreach (var fixture in document.RootElement.EnumerateArray())
        {
            var input = AssessmentContract.BuildInput(fixture.GetProperty("answer").GetString()!, null, fixture.GetProperty("caseFacts").GetString());
            using var payload = JsonDocument.Parse(input);
            Assert.Equal(new[] { "caseFacts", "answers" }, payload.RootElement.EnumerateObject().Select(p => p.Name));
            Assert.DoesNotContain("provisionalTarget", input);
            Assert.DoesNotContain("humanReviewed", input);
            Assert.DoesNotContain("expectsUnsupported", input);
        }
    }
}
