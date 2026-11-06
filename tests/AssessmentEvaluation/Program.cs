using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

// This opt-in harness sends only synthetic fixtures; never send real clinical records here.
string Arg(string name, string fallback) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
var profile = Arg("--profile", "");
OllamaOptions? profileOptions = null;
if (profile.Length > 0)
{
    if (!new[] { "breeze", "llama3", "qwen" }.Contains(profile)) throw new ArgumentException("Unknown profile");
    using var config = JsonDocument.Parse(await File.ReadAllTextAsync($"Configuration/Ollama/{profile}.json"));
    profileOptions = config.RootElement.GetProperty("Ollama").Deserialize<OllamaOptions>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
}
if (args.Contains("--export-payloads"))
{
    var settings = profileOptions ?? throw new ArgumentException("Use --profile with --export-payloads");
    var destination = Arg("--output", $"docs/ollama/payloads/{profile}");
    Directory.CreateDirectory(destination);
    using var capture = new RecordingHandler { ExportOnly = true };
    using var transport = new HttpClient(capture) { BaseAddress = new Uri(settings.BaseUrl) };
    var exporter = new OllamaClient(transport, Options.Create(settings), NullLogger<OllamaClient>.Instance);
    foreach (var stage in Enum.GetValues<AnalysisStage>().Where(s => s != AnalysisStage.GeneralChat))
    foreach (var compare in new[] { false, true })
    foreach (var hasCase in new[] { false, true })
    {
        await exporter.AssessAsync([new("system", AnalysisStageInstructions.Get(stage)), new("user",
            AssessmentContract.BuildInput("合成作答：資料不足，應限制推論範圍。", compare ? "合成舊作答：尚待整理。" : null,
                hasCase ? "合成案例：在提示下可完成指定任務；未提供其他資料。" : null))], compare, default);
        await File.WriteAllTextAsync(Path.Combine(destination, $"{stage}-{(compare ? "compare" : "review")}-{(hasCase ? "case" : "no-case")}.json"),
            JsonSerializer.Serialize(new { profile, promptVersion = AnalysisStageInstructions.Version, synthetic = true,
                liveVerified = false, endpoint = settings.BaseUrl, payload = JsonDocument.Parse(capture.Request!).RootElement },
                new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
    Console.WriteLine($"Exported 16 synthetic wire payloads to {destination}; no inference calls made.");
    return 0;
}
var endpoint = Arg("--base-url", profileOptions?.BaseUrl ?? "http://IAN02:11434");
var model = Arg("--model", profileOptions?.Model ?? "llama3.1:8b");
var repeats = int.Parse(Arg("--repeats", "2"));
var limit = int.Parse(Arg("--max-cases", "100"));
var only = Arg("--only", "").Split(',', StringSplitOptions.RemoveEmptyEntries);
var output = Path.GetFullPath(Arg("--output", "tests/AssessmentEvaluation/results"));
Directory.CreateDirectory(output);
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = false };
var fixtures = JsonSerializer.Deserialize<List<Fixture>>(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "fixtures.json")), jsonOptions)!;
var selected = fixtures.Where(f => only.Length == 0 || only.Contains(f.Id)).Take(limit).ToList();
if (selected.Count == 0 || repeats < 1) throw new ArgumentException("Select at least one fixture and repeat.");
var sampling = profileOptions?.AssessmentSampling ?? new AssessmentSampling();
using var handler = new RecordingHandler();
using var http = new HttpClient(handler) { BaseAddress = new Uri(endpoint.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(180) };
var client = new OllamaClient(http, Options.Create(new OllamaOptions
{
    BaseUrl = endpoint, Model = model, AssessmentSampling = sampling,
    MergeAssessmentSystemMessages = profileOptions?.MergeAssessmentSystemMessages ?? true,
    RepeatAssessmentRequirements = profileOptions?.RepeatAssessmentRequirements ?? true,
    SystemPrompt = "所有回覆一律使用臺灣繁體中文（zh-TW），不得使用簡體中文（zh-CN）。"
}), NullLogger<OllamaClient>.Instance);
string? serverVersion = null, modelDigest = null;
try
{
    using var metadata = new HttpClient { BaseAddress = http.BaseAddress, Timeout = TimeSpan.FromSeconds(10) };
    serverVersion = await metadata.GetStringAsync("api/version");
    using var tags = JsonDocument.Parse(await metadata.GetStringAsync("api/tags"));
    var tag = tags.RootElement.GetProperty("models").EnumerateArray().FirstOrDefault(m => m.GetProperty("name").GetString() == model || m.GetProperty("name").GetString() == model + ":latest");
    if (tag.ValueKind == JsonValueKind.Object) modelDigest = tag.GetProperty("digest").GetString();
}
catch (Exception e) { Console.WriteLine("Metadata unavailable: " + e.Message); }
var runId = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ");
var rows = new List<Run>();
var stop = false;
foreach (var fixture in selected)
{
    if (stop) break;
    AnalysisStageCatalog.TryParse(fixture.Stage, out var stage);
    var system = AnalysisStageInstructions.Get(stage);
    var promptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(system)));
    for (var repeat = 0; repeat < repeats; repeat++)
    {
        AssessmentResult? findings = null; string? error = null; string? raw = null;
        handler.Request = null;
        try
        {
            var response = await client.AssessAsync([new("system", system), new("user", AssessmentContract.BuildInput(fixture.Answer, null, fixture.CaseFacts))], false, default);
            raw = response.Content;
            findings = AssessmentResultParser.Parse(raw, false, !string.IsNullOrWhiteSpace(fixture.CaseFacts));
        }
        catch (Exception e) { error = e.Message; }
        var row = new Run(fixture.Id, repeat, findings?.Evaluations[0].Score, error, findings);
        rows.Add(row);
        await File.AppendAllTextAsync(Path.Combine(output, runId + ".jsonl"), JsonSerializer.Serialize(new
        {
            runId, fixture.Id, repeat, timestamp = DateTimeOffset.UtcNow,
            model, modelDigest, serverVersion, sampling, promptVersion = AnalysisStageInstructions.Version, promptHash,
            fixture.Stage, submittedAnswer = fixture.Answer, fixture.CaseFacts,
            fixture.ProvisionalTarget, fixture.HumanReviewed, fixture.HistoricalPreviouslyExposed,
            payload = handler.Request is null ? (JsonElement?)null : JsonDocument.Parse(handler.Request).RootElement.Clone(),
            structuredFindings = findings, assignedScore = row.Score, rawModelContent = raw, error,
            humanReview = new { evidenceCorrect = (bool?)null, feedbackGradeAgreement = (bool?)null, assessorGrade = (int?)null, notes = "" }
        }, jsonOptions) + Environment.NewLine);
        Console.WriteLine($"{fixture.Id} repeat {repeat}: {(error is null ? $"score {row.Score}" : error)}");
        // Stop a failing endpoint rather than repeating expensive transport failures.
        if (error is not null && raw is null) { stop = true; break; }
    }
}
var groups = selected.Where(f => !f.HistoricalPreviouslyExposed).GroupBy(f => f.Group).Select(g => new
{
    group = g.Key,
    provisionalRanking = g.Select(f => new { f.Id, f.ProvisionalTarget, scores = rows.Where(r => r.Id == f.Id).Select(r => r.Score).ToArray() }).ToArray()
}).ToArray();
double? Mean(string id)
{
    var scores = rows.Where(r => r.Id == id && r.Score.HasValue).Select(r => (double)r.Score!.Value).ToArray();
    return scores.Length == 0 ? null : scores.Average();
}
var report = new
{
    runId, promptVersion = AnalysisStageInstructions.Version, model, modelDigest, serverVersion,
    plannedAttempts = selected.Count * repeats, notExecuted = selected.Count * repeats - rows.Count,
    attempts = rows.Count, validContracts = rows.Count(r => r.Error is null), failures = rows.Where(r => r.Error != null).ToArray(),
    provisionalComparisons = groups,
    ordinalProbes = selected.Where(f => !f.HistoricalPreviouslyExposed && f.ProvisionalTarget <= 2)
        .SelectMany(weak => selected.Where(strong => strong.Group == weak.Group && strong.ProvisionalTarget == 4)
            .Select(strong => new { weak = weak.Id, strong = strong.Id, weakMean = Mean(weak.Id), strongMean = Mean(strong.Id),
                weakBelowStrong = Mean(weak.Id).HasValue && Mean(strong.Id).HasValue ? (bool?)(Mean(weak.Id) < Mean(strong.Id)) : null })).ToArray(),
    provisionalTargetAgreement = selected.Where(f => !f.HistoricalPreviouslyExposed).Select(f => new
    {
        f.Id, f.ProvisionalTarget, meanScore = Mean(f.Id), absoluteDifference = Mean(f.Id).HasValue ? Math.Abs(Mean(f.Id)!.Value - f.ProvisionalTarget) : (double?)null
    }).ToArray(),
    repeatConsistency = rows.Where(r => r.Score.HasValue).GroupBy(r => r.Id).Select(g => new { id = g.Key, count = g.Count(), spread = g.Max(r => r.Score) - g.Min(r => r.Score) }).ToArray(),
    wordingConsistency = selected.Where(f => f.EquivalentTo != null).Select(f => new
    {
        f.Id, f.EquivalentTo,
        scores = rows.Where(r => r.Id == f.Id).Select(r => r.Score).ToArray(),
        equivalentScores = rows.Where(r => r.Id == f.EquivalentTo).Select(r => r.Score).ToArray(),
        absoluteMeanDifference = Mean(f.Id).HasValue && Mean(f.EquivalentTo!).HasValue ? Math.Abs(Mean(f.Id)!.Value - Mean(f.EquivalentTo!)!.Value) : (double?)null
    }).ToArray(),
    unsupportedConclusionProbes = selected.Where(f => f.ExpectsUnsupported).Select(f => new
    {
        f.Id, observations = rows.Where(r => r.Id == f.Id).Select(r => new { r.Score, flaggedClaims = r.Findings?.Evaluations[0].UnsupportedClaims.Count }).ToArray()
    }).ToArray(),
    limitations = "Targets are provisional, not human-validated. Contract validity, ranking and claim counts do not establish evidence accuracy or feedback/grade agreement. Human review fields must be completed. Historical original is not an unseen generalization test."
};
var reportPath = Path.Combine(output, runId + "-report.json");
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions(jsonOptions) { WriteIndented = true }));
Console.WriteLine(reportPath);
return rows.Any(r => r.Error != null) ? 1 : 0;

record Fixture(string Id, string Stage, string? CaseFacts, string Answer, int ProvisionalTarget, bool HumanReviewed, string Group, bool ExpectsUnsupported, string? EquivalentTo = null, bool HistoricalPreviouslyExposed = false);
record Run(string Id, int Repeat, int? Score, string? Error, AssessmentResult? Findings);
sealed class RecordingHandler : DelegatingHandler
{
    public bool ExportOnly;
    public string? Request;
    public RecordingHandler() : base(new HttpClientHandler()) { }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Request = await request.Content!.ReadAsStringAsync(ct);
        if (ExportOnly) return new(System.Net.HttpStatusCode.OK) { Content = new StringContent("""{"message":{"role":"assistant","content":"{}"}}""") };
        return await base.SendAsync(request, ct);
    }
}
