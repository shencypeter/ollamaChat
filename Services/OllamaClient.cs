using System.Diagnostics;
using System.Net.Http.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using Microsoft.Extensions.Options;

namespace AiTeachingAssistant.Services;

public interface IOllamaClient
{
    Task<(string Content, string? Thinking, RequestMetrics Metrics)> ChatAsync(IReadOnlyList<OllamaMessage> messages, CancellationToken cancellationToken);
    Task<string> SummarizeAsync(
        string existingSummary,
        IReadOnlyList<ChatMessage> messagesToIncorporate,
        int maxCharacters,
        CancellationToken cancellationToken);
    Task<OllamaHealthResult> CheckHealthAsync(CancellationToken cancellationToken);
}

public sealed class OllamaClient(HttpClient httpClient, IOptions<OllamaOptions> options, ILogger<OllamaClient> logger) : IOllamaClient
{
    private readonly OllamaOptions _options = options.Value;

    public async Task<(string Content, string? Thinking, RequestMetrics Metrics)> ChatAsync(
        IReadOnlyList<OllamaMessage> messages,
        CancellationToken cancellationToken)
    {
        var prompt = new List<OllamaMessage>();
        if (!string.IsNullOrWhiteSpace(_options.SystemPrompt))
        {
            prompt.Add(new OllamaMessage("system", _options.SystemPrompt));
        }

        prompt.AddRange(messages);
        var (result, roundTripMilliseconds) = await SendAsync(prompt, cancellationToken);
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

        var (result, _) = await SendAsync(messages, cancellationToken);
        var summary = result.Message?.Content?.Trim();
        return string.IsNullOrWhiteSpace(summary)
            ? throw new InvalidOperationException("Ollama returned an empty conversation summary.")
            : summary;
    }

    private async Task<(OllamaChatResponse Result, long RoundTripMilliseconds)> SendAsync(
        IReadOnlyList<OllamaMessage> messages,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var response = await httpClient.PostAsJsonAsync("api/chat", new OllamaChatRequest
        {
            Model = _options.Model,
            Messages = messages,
            Stream = false
        }, cancellationToken);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Ollama chat failed with {StatusCode}: {Body}", response.StatusCode, error);
            throw new HttpRequestException($"Ollama returned {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Ollama returned an empty response.");
        return (result, stopwatch.ElapsedMilliseconds);
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
