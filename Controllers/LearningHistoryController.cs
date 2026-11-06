using System.Security.Claims;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AiTeachingAssistant.Controllers;

[ApiController,Authorize,Route("api/learning-history")]
public sealed class LearningHistoryController(LearningHistory history):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("courses")]
    public async Task<IActionResult> Courses(CancellationToken ct)=>Ok(await history.Courses(Actor,ct));
    [HttpGet]
    public async Task<IActionResult> Read(CancellationToken ct,Guid? course=null,string? term=null,string? kind=null,DateTimeOffset? from=null,DateTimeOffset? until=null,int page=1,int pageSize=20,bool todo=false)
    {
        try{return Ok(await history.Activities(Actor,course,term,kind,from,until,page,pageSize,todo,ct));}
        catch(AssignmentWorkflowException e){return StatusCode(e.StatusCode,new{error=e.Message});}
    }
}
