using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AiTeachingAssistant.Controllers;
[ApiController, Authorize]
[Route("api/assignments/{assignmentId:guid}/questions/{questionId:guid}/ai-feedback")]
public sealed class FormativeFeedbackController : ControllerBase
{
    [HttpGet, HttpPost]
    public IActionResult Disabled() => StatusCode(403, new { error = "作業 AI 評分與回饋目前已停用，請使用獨立 AI 入口。" });
}
