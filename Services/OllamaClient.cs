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
    Task<(string Content, string? Thinking, RequestMetrics Metrics)> ConversationAsync(IReadOnlyList<OllamaMessage> messages, CancellationToken cancellationToken)
        => throw new NotSupportedException("This provider does not support isolated conversations.");
    Task<(string Content, string? Thinking, RequestMetrics Metrics)> CaseFeedbackAsync(IReadOnlyList<OllamaMessage> messages, CancellationToken cancellationToken)
        => throw new NotSupportedException("This provider does not support annotated case feedback.");
    Task<(string Content, string? Thinking, RequestMetrics Metrics)> CaseFeedbackAsync(IReadOnlyList<OllamaMessage> messages, AnalysisStage stage, CancellationToken cancellationToken)
        => CaseFeedbackAsync(messages,cancellationToken);
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

    public Task<(string Content, string? Thinking, RequestMetrics Metrics)> ConversationAsync(
        IReadOnlyList<OllamaMessage> messages, CancellationToken cancellationToken)=>IsolatedConversationAsync(messages,null,cancellationToken);

    public Task<(string Content, string? Thinking, RequestMetrics Metrics)> CaseFeedbackAsync(
        IReadOnlyList<OllamaMessage> messages, CancellationToken cancellationToken)=>CaseFeedbackCoreAsync(messages,null,cancellationToken);

    public Task<(string Content, string? Thinking, RequestMetrics Metrics)> CaseFeedbackAsync(
        IReadOnlyList<OllamaMessage> messages, AnalysisStage stage, CancellationToken cancellationToken)=>CaseFeedbackCoreAsync(messages,stage,cancellationToken);

    private Task<(string Content, string? Thinking, RequestMetrics Metrics)> CaseFeedbackCoreAsync(
        IReadOnlyList<OllamaMessage> messages,AnalysisStage? stage,CancellationToken cancellationToken)
    {
        // Formal feedback never re-ingests prior drafts or assistant replies.
        // Keep only the supplied system instructions and the current answer.
        // Raw Chat uses ConversationAsync and retains its independent memory.
        var answer=messages.LastOrDefault(m=>m.Role=="user")??throw new InvalidOperationException("Case feedback requires a current answer.");
        var request=messages.Where(m=>m.Role=="system").Select(m=>new OllamaMessage(m.Role,m.Content)).Append(new OllamaMessage("user",answer.Content)).ToArray();
        var current=request.Length-1;
        request[current]=new("user",System.Text.Json.JsonSerializer.Serialize(new {
            currentPhase=stage.HasValue?AnalysisStageCatalog.PhaseCode(stage.Value):null,
            currentStage=stage.HasValue?AnalysisStageCatalog.Get(stage.Value).Key:null,
            currentStageLabel=stage.HasValue?AnalysisStageCatalog.Get(stage.Value).Label:null,
            outputRequirements=CaseChatPrompts.FeedbackRequirements(stage),
            answerText=request[current].Content
        },new System.Text.Json.JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
        return IsolatedConversationAsync(request,CaseAnnotationContract.SchemaFor(_options.CompactCaseFeedback,annotationsFirst:stage==AnalysisStage.IllnessCourse),cancellationToken,_options.CaseFeedbackMaxTokens);
    }

    private async Task<(string Content, string? Thinking, RequestMetrics Metrics)> IsolatedConversationAsync(
        IReadOnlyList<OllamaMessage> messages,System.Text.Json.JsonElement? schema,CancellationToken cancellationToken,int? maxOutputTokens=null)
    {
        try
        {
            return await ChatCoreAsync(messages.Select(m => new OllamaMessage(m.Role, m.Content)).ToArray(), schema, cancellationToken, conversation: true,maxOutputTokens:maxOutputTokens);
        }
        catch (OperationCanceledException) when(!cancellationToken.IsCancellationRequested)
        {
            throw new CaseChatException(502,"模型暫時無法回覆，請稍後重試。",new("provider_timeout","等候模型服務超時；沒有取得完整回覆，並非確認模型拒答。"));
        }
        catch (OperationCanceledException)
        {
            throw new OperationCanceledException("Ollama conversation request was canceled.", cancellationToken);
        }
        catch (CaseChatException){throw;}
        catch(HttpRequestException)
        {
            throw new CaseChatException(502,"模型暫時無法回覆，請稍後重試。",new("provider_unreachable","無法連線至模型服務，或連線在回覆完成前中斷。"));
        }
        catch (Exception)
        {
            // Never retain provider body text through an inner exception or log.
            throw new InvalidOperationException("Ollama conversation request failed.");
        }
    }

    private async Task<(string Content, string? Thinking, RequestMetrics Metrics)> ChatCoreAsync(
        IReadOnlyList<OllamaMessage> messages, System.Text.Json.JsonElement? schema, CancellationToken cancellationToken, bool textFeedback=false, bool conversation=false,int? maxOutputTokens=null)
    {
        var prompt = new List<OllamaMessage>();
        if (!conversation && !string.IsNullOrWhiteSpace(_options.SystemPrompt))
        {
            prompt.Add(new OllamaMessage("system", _options.SystemPrompt));
        }

        prompt.AddRange(messages);
        if (schema.HasValue && !conversation)
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
        if (!conversation && !textFeedback && schema.HasValue && _options.RepeatAssessmentRequirements && prompt.LastOrDefault()?.Role == "user")
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
        var (result, roundTripMilliseconds) = await SendAsync(prompt, cancellationToken, schema, textFeedback?"case-feedback":null, conversation,maxOutputTokens);
        if(conversation&&schema.HasValue)CaseAnnotationContract.EnsureComplete(result.DoneReason);
        var content = result.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            if(conversation)throw new CaseChatException(502,"模型暫時無法回覆，請稍後重試。",new("empty_reply","模型服務回傳成功狀態，但沒有非空白的 assistant 回覆。"));
            throw new InvalidOperationException("Ollama returned no assistant message.");
        }

        var tokensPerSecond = result.EvalDuration > 0
            ? result.EvalCount / (result.EvalDuration / 1_000_000_000d)
            : 0;

        return (content, conversation ? null : result.Message?.Thinking, new RequestMetrics(
            result.TotalDuration,
            result.LoadDuration,
            result.PromptEvalDuration,
            result.EvalDuration,
            result.PromptEvalCount,
            result.PromptEvalCachedCount,
            result.EvalCount,
            Math.Round(tokensPerSecond, 2),
            roundTripMilliseconds,
            result.DoneReason){AvailableFields=result.AvailableMetrics});
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
        CancellationToken cancellationToken, System.Text.Json.JsonElement? schema = null, string? purpose = null, bool conversation = false,int? maxOutputTokens=null)
    {
        var stopwatch = Stopwatch.StartNew();
        var sampling=maxOutputTokens.HasValue?_options.AssessmentSampling with{NumPredict=maxOutputTokens.Value}:_options.AssessmentSampling;
        var payload = new OllamaChatRequest
        {
            Model = _options.Model,
            Messages = messages,
            Stream = false,
            Format = schema,
            Options = schema.HasValue || conversation ? sampling : null
        };
        // Capture the same serialized bytes sent on the wire, before redacting the diagnostic copy.
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        if (!conversation) diagnostics?.Capture(json, purpose=="case-feedback"?StudioFeedbackContract.Version:schema.HasValue ? AnalysisStageInstructions.Version : null);
        var endpoint=httpClient.BaseAddress is null?null:new Uri(httpClient.BaseAddress,"api/chat");
        var inspectedRequest = conversation
            ?inspection?.CaptureConversation(json,endpoint,schema.HasValue?"case-analysis":"raw-chat")
            :inspection?.Capture(json,endpoint,purpose ?? (schema.HasValue ? "assessment" : "chat"));
        using var response = await httpClient.PostAsync("api/chat", new StringContent(json, System.Text.Encoding.UTF8, "application/json"), cancellationToken);
        stopwatch.Stop();
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!conversation) inspection?.CaptureResponse(inspectedRequest, rawResponse, (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            if(conversation)throw new CaseChatException(502,"模型暫時無法回覆，請稍後重試。",CaseChatFailure.FromHttp((int)response.StatusCode,rawResponse));
            if (!conversation) logger.LogWarning("Ollama chat failed with {StatusCode}; response body omitted to protect clinical data.", response.StatusCode);
            throw new HttpRequestException($"Ollama returned {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }

        try
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<OllamaChatResponse>(rawResponse)
                ?? throw new InvalidOperationException("Ollama returned an empty response.");
            if(conversation)
            {
                using var document=System.Text.Json.JsonDocument.Parse(rawResponse);
                var available=new List<string>{"roundTripMilliseconds"};
                foreach(var (key,name) in new[]{("total_duration","totalDurationNanoseconds"),("load_duration","loadDurationNanoseconds"),("prompt_eval_duration","promptEvalDurationNanoseconds"),("eval_duration","evalDurationNanoseconds"),("prompt_eval_count","promptTokens"),("prompt_eval_cached_count","cachedPromptTokens"),("eval_count","responseTokens")})
                    if(document.RootElement.TryGetProperty(key,out var number)&&number.ValueKind==System.Text.Json.JsonValueKind.Number&&number.TryGetDouble(out var value)&&double.IsFinite(value)&&value>=0)available.Add(name);
                if(available.Contains("responseTokens")&&available.Contains("evalDurationNanoseconds")&&result.EvalDuration>0)available.Add("tokensPerSecond");
                result.AvailableMetrics=available;
            }
            return (result, stopwatch.ElapsedMilliseconds);
        }
        catch (System.Text.Json.JsonException exception)
        {
            if(conversation)throw new CaseChatException(502,"模型暫時無法回覆，請稍後重試。",new("provider_json","模型服務回傳的 HTTP 本文不是可解析的 Ollama JSON 回應。"));
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
