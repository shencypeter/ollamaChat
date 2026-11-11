using System.Security.Claims;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
namespace AiTeachingAssistant.Controllers;

[ApiController,Authorize]
public sealed class LearningTimeController(LearningTime learning,AssignmentWorkflow assignments,NpgsqlDataSource source,LearnerExperience experience):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action){try{return await action();}catch(AssignmentWorkflowException e){return StatusCode(e.StatusCode,new{error=e.Message});}}
    [HttpGet("api/courses/{course:guid}/resources/{id:guid}/learning")]
    public Task<IActionResult> Resource(Guid course,Guid id,CancellationToken ct)=>Run(async()=>Ok(await learning.Resource(course,id,Actor,experience.IsPractice,ct)));
    [HttpGet("api/courses/{course:guid}/resources/{id:guid}/learning/content")]
    public Task<IActionResult> Content(Guid course,Guid id,CancellationToken ct)=>Run(async()=>{
        var meta=await learning.Resource(course,id,Actor,experience.IsPractice,ct);
        if(meta.YouTubeVideoId!=null)return BadRequest(new{error="YouTube 回放請使用平台內嵌播放器。"});
        await using var cmd=source.CreateCommand("SELECT content FROM course_resources WHERE id=$1 AND course_id=$2");cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);
        if(await cmd.ExecuteScalarAsync(ct) is not byte[] bytes)return NotFound();
        Response.Headers["X-Content-Type-Options"]="nosniff";return File(bytes,meta.MediaType);
    });
    [AiTeachingAssistant.Services.CourseCapability("statistics")]
    [HttpGet("api/courses/{course:guid}/resources/{id:guid}/learning/statistics")]
    public Task<IActionResult> Statistics(Guid course,Guid id,CancellationToken ct)=>Run(async()=>Ok(await learning.Statistics(course,id,Actor,experience.IsPractice,ct)));
    [HttpPost("api/courses/{course:guid}/resources/{id:guid}/learning/sessions")]
    public Task<IActionResult> Start(Guid course,Guid id,LearningHeartbeat input,CancellationToken ct)=>Run(async()=>Ok(await learning.Start(course,id,Actor,experience.IsPractice,input.Position,ct,input.PlaybackRate)));
    [HttpPost("api/courses/{course:guid}/resources/{id:guid}/learning/sessions/{session:guid}")]
    public Task<IActionResult> Heartbeat(Guid course,Guid id,Guid session,LearningHeartbeat input,CancellationToken ct)=>Run(async()=>Ok(await learning.Heartbeat(course,id,session,Actor,experience.IsPractice,input,ct)));
    [HttpPost("api/assignments/{id:guid}/start")]
    public Task<IActionResult> StartQuiz(Guid id,CancellationToken ct)=>Run(async()=>{await assignments.StartLearningQuiz(id,Actor,ct);return NoContent();});
}
