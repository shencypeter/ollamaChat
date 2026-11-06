using System.Text.Json.Serialization;

namespace AiTeachingAssistant.Models;

public sealed record AssessmentFinding(string Claim, string Evidence, string Explanation);
public sealed record AssessmentEvaluation(
    string Version, bool CaseEvidenceAvailable,
    IReadOnlyList<AssessmentFinding> SupportedClaims,
    IReadOnlyList<AssessmentFinding> PartiallySupportedClaims,
    IReadOnlyList<AssessmentFinding> UnsupportedClaims,
    IReadOnlyList<AssessmentFinding> UnverifiedClaims,
    IReadOnlyList<AssessmentFinding> ImportantOmissions,
    string ReasoningQuality, int Score, string Justification);
public sealed record AssessmentResult(IReadOnlyList<AssessmentEvaluation> Evaluations, string ComparisonFeedback);
public sealed record AssessmentSampling(
    [property: JsonPropertyName("temperature")] double Temperature = 0,
    [property: JsonPropertyName("seed")] int Seed = 42,
    [property: JsonPropertyName("top_p")] double TopP = 0.9,
    [property: JsonPropertyName("top_k")] int TopK = 40,
    [property: JsonPropertyName("num_ctx")] int NumCtx = 16384,
    [property: JsonPropertyName("num_predict")] int NumPredict = 4096);
