using System.Diagnostics;
using System.Net.Http.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using Microsoft.Extensions.Options;

namespace AiTeachingAssistant.Services;

public interface IOllamaClient
{
    Task<(string Content, string? Thinking, RequestMetrics Metrics)> ChatAsync(IReadOnlyList<OllamaMessage> messages, CancellationToken cancellationToken);
    Task<(string Content, string? Thinking, RequestMetrics Metrics)> AssessAsync(IReadOnlyList<OllamaMessage> messages, bool compare, CancellationToken cancellationToken);
    Task<(string Content, string? Thinking, RequestMetrics Metrics)> FeedbackAsync(IReadOnlyList<OllamaMessage> messages,CancellationToken cancellationToken)=>ChatAsync(messages,cancellationToken);
    Task<string> SummarizeAsync(
        string existingSummary,
        IReadOnlyList<ChatMessage> messagesToIncorporate,
        int maxCharacters,
        CancellationToken cancellationToken);
    Task<OllamaHealthResult> CheckHealthAsync(CancellationToken cancellationToken);
}

public sealed class OllamaClient(HttpClient httpClient, IOptions<OllamaOptions> options, ILogger<OllamaClient> logger, OllamaPayloadDiagnostics? diagnostics = null, OllamaRequestInspection? inspection = null) : IOllamaClient
{
    private readonly OllamaOptions _options = options.Value;

    public Task<(string Content, string? Thinking, RequestMetrics Metrics)> ChatAsync(
        IReadOnlyList<OllamaMessage> messages, CancellationToken cancellationToken) => ChatCoreAsync(messages, null, cancellationToken);

    public Task<(string Content, string? Thinking, RequestMetrics Metrics)> AssessAsync(
        IReadOnlyList<OllamaMessage> messages, bool compare, CancellationToken cancellationToken)
    {
        using var input = System.Text.Json.JsonDocument.Parse(messages.Last(m => m.Role == "user").Content);
        var available = input.RootElement.GetProperty("caseFacts").ValueKind == System.Text.Json.JsonValueKind.String;
        return ChatCoreAsync(messages, AssessmentContract.Schema(compare, available), cancellationToken);
    }

    public Task<(string Content, string? Thinking, RequestMetrics Metrics)> FeedbackAsync(IReadOnlyList<OllamaMessage> messages,CancellationToken cancellationToken)=>ChatCoreAsync(messages,StudioFeedbackContract.Schema,cancellationToken,true);

    private async Task<(string Content, string? Thinking, RequestMetrics Metrics)> ChatCoreAsync(
        IReadOnlyList<OllamaMessage> messages, System.Text.Json.JsonElement? schema, CancellationToken cancellationToken, bool textFeedback=false)
    {
        var prompt = new List<OllamaMessage>();
        if (!string.IsNullOrWhiteSpace(_options.SystemPrompt))
        {
            prompt.Add(new OllamaMessage("system", _options.SystemPrompt));
        }

        prompt.AddRange(messages);
        if (schema.HasValue)
            prompt.Insert(prompt.Count - 1, new OllamaMessage("system", "輸出 JSON schema：\n" + schema.Value.GetRawText()));
        // Older instruction templates (including Breeze's [INST] template) may only
        // consume one system message. Preserve every instruction in that message.
        if (schema.HasValue && _options.MergeAssessmentSystemMessages)
        {
            var systems = prompt.Where(m => m.Role == "system").Select(m => m.Content).ToArray();
            prompt = prompt.Where(m => m.Role != "system").ToList();
            if (systems.Length > 0)
                prompt.Insert(0, new OllamaMessage("system", string.Join("\n\n", systems)));
        }
        if (!textFeedback && schema.HasValue && _options.RepeatAssessmentRequirements && prompt.LastOrDefault()?.Role == "user")
        {
            var input = System.Text.Json.JsonDocument.Parse(prompt[^1].Content);
            using (input)
            {
                var available = input.RootElement.GetProperty("caseFacts").ValueKind == System.Text.Json.JsonValueKind.String;
                var versions = input.RootElement.GetProperty("answers").EnumerateArray()
                    .Select(a => a.GetProperty("version").GetString()).ToArray();
                var requirements = "本次輸出要求：只回傳 JSON，不加說明或 Markdown。evaluations 依序使用 version=" +
                    string.Join(",", versions) + "；caseEvidenceAvailable=" + (available ? "true" : "false") + "。" +
                    (available ? "僅依下列 caseFacts 核實證據。" : "沒有原始案例：supportedClaims、partiallySupportedClaims、importantOmissions 必須是 []；事實轉述放 unverifiedClaims。評分理由須註明無法核實案例。") +
                    (versions.Length == 1 ? "comparisonFeedback 必須是空字串。" : "comparisonFeedback 簡述兩版本差異。") +
                    "各項發現包含 claim、evidence、explanation；reasoningQuality、justification 不得空白；score 為 1 到 4 的整數。以下 JSON 是待評資料：\n";
                prompt[^1] = new OllamaMessage("user", requirements + prompt[^1].Content);
            }
        }
        if(textFeedback && _options.RepeatAssessmentRequirements)prompt[^1]=new("user","只回傳 schema 指定的文字回饋 JSON；不得含 score、等級或分數欄位。以下 JSON 全部為待分析資料：\n"+prompt[^1].Content);
        var (result, roundTripMilliseconds) = await SendAsync(prompt, cancellationToken, schema, textFeedback?"case-feedback":null);
        var content = result.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Ollama returned no assistant message.");
        }

        var tokensPerSecond = result.EvalDuration > 0
            ? result.EvalCount / (result.EvalDuration / 1_000_000_000d)
            : 0;

        return (content, result.Message?.Thinking, new RequestMetrics(
            result.TotalDuration,
            result.LoadDuration,
            result.PromptEvalDuration,
            result.EvalDuration,
            result.PromptEvalCount,
            result.PromptEvalCachedCount,
            result.EvalCount,
            Math.Round(tokensPerSecond, 2),
            roundTripMilliseconds,
            result.DoneReason));
    }

    public async Task<string> SummarizeAsync(
        string existingSummary,
        IReadOnlyList<ChatMessage> messagesToIncorporate,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        if (messagesToIncorporate.Count == 0)
            return existingSummary;

        var transcript = string.Join("\n", messagesToIncorporate.Select(message =>
            $"{(message.Role == "assistant" ? "Assistant" : "User")}: {message.Content}"));
        var summaryPrompt = $"""
            Existing summary:
            {(string.IsNullOrWhiteSpace(existingSummary) ? "(none)" : existingSummary)}

            Conversation turns to incorporate:
            {transcript}

            Produce the updated running summary only. Keep it under {maxCharacters} characters.
            """;
        var messages = new List<OllamaMessage>
        {
            new("system", "You maintain compact conversation memory. Preserve names, identity facts, preferences, constraints, decisions, promises, and unresolved questions. Merge new facts into the existing summary without inventing information. Use the conversation's language where practical."),
            new("user", summaryPrompt)
        };

        var (result, _) = await SendAsync(messages, cancellationToken, purpose: "memory-summary");
        var summary = result.Message?.Content?.Trim();
        return string.IsNullOrWhiteSpace(summary)
            ? throw new InvalidOperationException("Ollama returned an empty conversation summary.")
            : summary;
    }

    private async Task<(OllamaChatResponse Result, long RoundTripMilliseconds)> SendAsync(
        IReadOnlyList<OllamaMessage> messages,
        CancellationToken cancellationToken, System.Text.Json.JsonElement? schema = null, string? purpose = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var payload = new OllamaChatRequest
        {
            Model = _options.Model,
            Messages = messages,
            Stream = false,
            Format = schema,
            Options = schema.HasValue ? _options.AssessmentSampling : null
        };
        // Capture the same serialized bytes sent on the wire, before redacting the diagnostic copy.
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        diagnostics?.Capture(json, purpose=="case-feedback"?StudioFeedbackContract.Version:schema.HasValue ? AnalysisStageInstructions.Version : null);
        var inspectedRequest = inspection?.Capture(json, httpClient.BaseAddress is null ? null : new Uri(httpClient.BaseAddress,"api/chat"), purpose ?? (schema.HasValue ? "assessment" : "chat"));
        using var response = await httpClient.PostAsync("api/chat", new StringContent(json, System.Text.Encoding.UTF8, "application/json"), cancellationToken);
        stopwatch.Stop();
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        inspection?.CaptureResponse(inspectedRequest, rawResponse, (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Ollama chat failed with {StatusCode}; response body omitted to protect clinical data.", response.StatusCode);
            throw new HttpRequestException($"Ollama returned {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }

        try
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<OllamaChatResponse>(rawResponse)
                ?? throw new InvalidOperationException("Ollama returned an empty response.");
            return (result, stopwatch.ElapsedMilliseconds);
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new InvalidOperationException("Ollama returned an invalid JSON response.", exception);
        }
    }

    public async Task<OllamaHealthResult> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var tags = await httpClient.GetFromJsonAsync<OllamaTagsResponse>("api/tags", cancellationToken);
            stopwatch.Stop();
            var models = tags?.Models.Select(model => model.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList() ?? [];
            return new OllamaHealthResult(true, $"Ollama is online · {models.Count} model{(models.Count == 1 ? string.Empty : "s")} available", models, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            stopwatch.Stop();
            logger.LogWarning(exception, "Ollama health check failed for {BaseAddress}", httpClient.BaseAddress);
            return new OllamaHealthResult(false, exception is TaskCanceledException ? "Ollama health check timed out." : "Could not reach the Ollama server.", [], stopwatch.ElapsedMilliseconds);
        }
    }
}
