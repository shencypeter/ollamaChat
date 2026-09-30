using System.Diagnostics;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AiTeachingAssistant.Controllers;

public class HomeController(
    ILogger<HomeController> logger,
    IChatSessionStore sessionStore,
    IMemoryOrchestrator memoryOrchestrator,
    IMarkdownRenderer markdownRenderer,
    IOllamaClient ollamaClient,
    IOptions<OllamaOptions> ollamaOptions,
    IOptions<MemoryOptions> memoryOptions) : Controller
{
    public IActionResult Index()
    {
        var ollama = ollamaOptions.Value;
        var memory = memoryOptions.Value;
        var activeStage = sessionStore.GetActiveStage();
        return View(new ChatPageViewModel(
            sessionStore.GetMessages(activeStage), ollama.Model, ollama.BaseUrl,
            sessionStore.GetMemoryStrategy(),
            sessionStore.GetMemoryStrategy().Equals("Summary", StringComparison.OrdinalIgnoreCase)
                ? memory.SummaryRecentTurns
                : memory.WindowSize,
            activeStage));
    }

    [HttpPost("chat/send")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !AnalysisStageCatalog.TryParse(request.Stage, out var stage))
            return BadRequest(new { error = "請輸入 1 至 8,000 個字元的訊息，並選擇有效的分析項目。" });

        try
        {
            var input = request.Message.Trim();
            sessionStore.SetActiveStage(stage);
            var history = sessionStore.GetMessages(stage);
            var context = await memoryOrchestrator.BuildContextAsync(history, input, stage, cancellationToken);
            var (content, thinking, metrics) = await ollamaClient.ChatAsync(context.Messages, cancellationToken);

            var userMessage = new ChatMessage("user", input, DateTimeOffset.UtcNow);
            var assistantMessage = new ChatMessage("assistant", content, DateTimeOffset.UtcNow, thinking);
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
                memoryDiagnostics));
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Chat request could not be completed.");
            var message = exception is TaskCanceledException
                ? "The model took too long to respond. Try again or increase Ollama:TimeoutSeconds."
                : exception.Message;
            return StatusCode(StatusCodes.Status502BadGateway, new { error = message });
        }
    }

    [HttpPost("chat/stage")]
    [ValidateAntiForgeryToken]
    public IActionResult SetAnalysisStage([FromBody] SetAnalysisStageRequest request)
    {
        if (!ModelState.IsValid || !AnalysisStageCatalog.TryParse(request.Stage, out var stage))
            return BadRequest(new { error = "分析項目無效。" });

        sessionStore.SetActiveStage(stage);
        return Ok(new { stage = AnalysisStageCatalog.Get(stage).Key });
    }

    [HttpPost("chat/memory")]
    [ValidateAntiForgeryToken]
    public IActionResult SetMemoryStrategy([FromBody] SetMemoryStrategyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Memory strategy must be None, Window, Buffer, or Summary." });

        var strategy = char.ToUpperInvariant(request.Strategy[0]) + request.Strategy[1..].ToLowerInvariant();
        sessionStore.SetMemoryStrategy(strategy);
        return Ok(new { strategy });
    }

    [HttpGet("chat/health")]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        var result = await ollamaClient.CheckHealthAsync(cancellationToken);
        return result.Online ? Ok(result) : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }

    [HttpPost("chat/clear")]
    [ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        sessionStore.Clear(sessionStore.GetActiveStage());
        return NoContent();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });
}
