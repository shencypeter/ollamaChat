using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiTeachingAssistant.Services;

// Debugger-only, process-local, single redacted snapshot. No HTTP endpoint or persistent log.
public sealed class OllamaPayloadDiagnostics(IHostEnvironment environment, IConfiguration configuration)
{
    private string? _latest;
    public string? LatestRedactedPayload => Volatile.Read(ref _latest);

    public void Capture(string serializedPayload, string? promptVersion)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("Diagnostics:CaptureOllamaPayload")) return;
        var payload = JsonNode.Parse(serializedPayload)!;
        foreach (var message in payload["messages"]!.AsArray())
        {
            // Only known application-owned system prompts may be retained. Summary system
            // messages can contain clinical text too, so redact all other system messages.
            var content = message!["content"]!.GetValue<string>();
            var safeSystem = message["role"]!.GetValue<string>() == "system" &&
                (content == configuration["Ollama:SystemPrompt"] ||
                 Enum.GetValues<Models.AnalysisStage>().Where(s => s != Models.AnalysisStage.GeneralChat)
                     .Any(s => content == AnalysisStageInstructions.Get(s)) ||
                 content == "輸出 JSON schema：\n" + AssessmentContract.Schema(false).GetRawText() ||
                 content == "輸出 JSON schema：\n" + AssessmentContract.Schema(true).GetRawText());
            if (!safeSystem && message["role"]!.GetValue<string>() == "system")
                safeSystem = Enum.GetValues<Models.AnalysisStage>().Where(s => s != Models.AnalysisStage.GeneralChat)
                    .Any(s => new[] { false, true }.Any(compare => new[] { false, true }.Any(available =>
                        content == string.Join("\n\n", new[] { configuration["Ollama:SystemPrompt"], AnalysisStageInstructions.Get(s),
                            "輸出 JSON schema：\n" + AssessmentContract.Schema(compare, available).GetRawText() }.Where(v => !string.IsNullOrWhiteSpace(v))))));
            if (!safeSystem)
            {
                // Preserve assessment structure and case availability without retaining text.
                try
                {
                    var context = JsonNode.Parse(content) as JsonObject;
                    if (context is not null && context.ContainsKey("caseFacts") && context["answers"] is JsonArray answers)
                    {
                        // Build a whitelist projection; arbitrary pasted JSON fields can also contain PII.
                        message["content"] = JsonSerializer.Serialize(new
                        {
                            caseFacts = context["caseFacts"] is null ? null : "[REDACTED: case facts]",
                            answers = answers.Select(a => new
                            {
                                version = new[] { "current", "before", "after" }.FirstOrDefault(v => a?["version"]?.ToString() == v) ?? "[REDACTED]",
                                text = "[REDACTED: student answer]"
                            }).ToArray()
                        });
                    }
                    else message["content"] = "[REDACTED: request content]";
                }
                catch (Exception e) when (e is JsonException or InvalidOperationException)
                {
                    message["content"] = "[REDACTED: request content]";
                }
            }
            if (message.AsObject().ContainsKey("thinking")) message["thinking"] = "[REDACTED]";
        }
        Volatile.Write(ref _latest, JsonSerializer.Serialize(new { promptVersion, capturedAt = DateTimeOffset.UtcNow, payload }));
    }
}
