using Microsoft.AspNetCore.Authorization;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AiTeachingAssistant.Controllers;

[ApiController, Route("api/ai"), Authorize]
public class AiController(
    ILogger<AiController> logger,
    IChatSessionStore sessionStore,
    IMemoryOrchestrator memoryOrchestrator,
    IMarkdownRenderer markdownRenderer,
    IOllamaClient ollamaClient,
    IOptions<OllamaOptions> ollamaOptions,
    OllamaRequestInspection requestInspection) : ControllerBase
{
    [HttpGet("configuration")]
    public IActionResult Configuration() => Ok(new {
        model = ollamaOptions.Value.Model,
        stages = AnalysisStageCatalog.All,
        memoryStrategy = sessionStore.GetMemoryStrategy(),
        messages = sessionStore.GetMessages(AnalysisStage.GeneralChat).Select(m => new { m.Role, m.Content, m.Timestamp, renderedContent = m.Role == "assistant" ? markdownRenderer.ToHtml(m.Content) : null })
    });

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !AnalysisStageCatalog.TryParse(request.Stage, out var stage))
            return BadRequest(new { error = "請輸入 1 至 8,000 個字元的內容，並選擇有效的評閱項目。" });

        if (request.Compare && stage == AnalysisStage.GeneralChat)
            return BadRequest(new { error = "一般對話不支援版本比較。" });

        if (request.Compare && string.IsNullOrWhiteSpace(request.BeforeMessage))
            return BadRequest(new { error = "版本比較需要同時提供修改前與修改後版本。" });

        try
        {
            var currentVersion = request.Message.Trim();
            var beforeVersion = request.BeforeMessage?.Trim();
            var caseAvailable = !string.IsNullOrWhiteSpace(request.CaseFacts);
            var input = stage == AnalysisStage.GeneralChat ? currentVersion :
                AssessmentContract.BuildInput(currentVersion, request.Compare ? beforeVersion : null, request.CaseFacts);
            sessionStore.SetActiveStage(stage);
            var history = stage == AnalysisStage.GeneralChat
                ? sessionStore.GetMessages(stage)
                : [];
            var context = await memoryOrchestrator.BuildContextAsync(history, input, stage, cancellationToken);
            var (content, thinking, metrics) = stage == AnalysisStage.GeneralChat
                ? await ollamaClient.ChatAsync(context.Messages, cancellationToken)
                : await ollamaClient.AssessAsync(context.Messages, request.Compare, cancellationToken);
            AssessmentScores? scores = null;
            AssessmentResult? assessment = null;
            if (stage != AnalysisStage.GeneralChat)
            {
                assessment = AssessmentResultParser.Parse(content, request.Compare, caseAvailable);
                content = AssessmentResultParser.ToMarkdown(assessment);
                scores = AssessmentResultParser.Scores(assessment);
                thinking = null; // Findings are public evidence summaries, not private model thinking.
            }

            thinking = null;
            var userMessage = new ChatMessage("user", input, DateTimeOffset.UtcNow);
            var assistantMessage = new ChatMessage("assistant", content, DateTimeOffset.UtcNow, thinking);
            if (stage == AnalysisStage.GeneralChat)
                sessionStore.SaveMessages(stage, history.Append(userMessage).Append(assistantMessage));
            var summaryUpdated = await memoryOrchestrator.CommitTurnAsync(stage, userMessage, assistantMessage, cancellationToken);
            var memoryDiagnostics = context.Diagnostics with
            {
                SummaryUpdated = context.Diagnostics.SummaryUpdated || summaryUpdated
            };

            return Ok(new ChatReply(
                assistantMessage,
                markdownRenderer.ToHtml(assistantMessage.Content),
                metrics,
                memoryDiagnostics.Strategy,
                context.Messages.Count,
                memoryDiagnostics,
                scores,
                assessment,
                requestInspection.Current));
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Chat request could not be completed.");
            var message = exception is TaskCanceledException
                ? "The model took too long to respond. Try again or increase Ollama:TimeoutSeconds."
                : exception.Message;
            return StatusCode(StatusCodes.Status502BadGateway, new AiFailure(message,requestInspection.Current));
        }
    }

    [HttpPost("stage")]
    public IActionResult SetAnalysisStage([FromBody] SetAnalysisStageRequest request)
    {
        if (!ModelState.IsValid || !AnalysisStageCatalog.TryParse(request.Stage, out var stage))
            return BadRequest(new { error = "分析項目無效。" });

        sessionStore.SetActiveStage(stage);
        return Ok(new { stage = AnalysisStageCatalog.Get(stage).Key });
    }

    [HttpPost("memory")]
    public IActionResult SetMemoryStrategy([FromBody] SetMemoryStrategyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Memory strategy must be None, Window, Buffer, or Summary." });

        var strategy = char.ToUpperInvariant(request.Strategy[0]) + request.Strategy[1..].ToLowerInvariant();
        sessionStore.SetMemoryStrategy(strategy);
        return Ok(new { strategy });
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        var result = await ollamaClient.CheckHealthAsync(cancellationToken);
        return result.Online ? Ok(result) : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }

    [HttpPost("clear")]
    public IActionResult Clear()
    {
        sessionStore.Clear(AnalysisStage.GeneralChat);
        return NoContent();
    }

}
