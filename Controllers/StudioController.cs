using System.Security.Claims;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
namespace AiTeachingAssistant.Controllers;
[ApiController,Authorize,Route("api/studio")]
public sealed class StudioController(NpgsqlDataSource source,StudioWorkflow workflow,IOllamaClient model,IOptions<OllamaOptions> options):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action){try{return await action();}catch(AssignmentWorkflowException e){return StatusCode(e.StatusCode,new{error=e.Message});}}
    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("SELECT u.display_name,u.student_number,COALESCE(p.department_year,''),length(trim(u.display_name))>0 AND length(trim(u.student_number))>0,u.department,u.grade_level FROM app_users u LEFT JOIN studio_profiles p ON p.user_id=u.id WHERE u.id=$1");cmd.Parameters.AddWithValue(Actor);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return Unauthorized();return Ok(new{displayName=r.GetString(0),studentNumber=r.IsDBNull(1)?"":r.GetString(1),departmentYear=r.GetString(2),completed=!r.IsDBNull(3)&&r.GetBoolean(3),department=r.IsDBNull(4)?null:r.GetString(4),gradeLevel=r.IsDBNull(5)?null:r.GetString(5)});
    }
    [HttpPut("profile")]
    public IActionResult SaveProfile(StudioProfileInput input)=>StatusCode(403,new{error="學籍資料由校方維護。"});
    [HttpGet("cases")]
    public Task<IActionResult> Cases(CancellationToken ct)=>Run(async()=>Ok(await workflow.Cases(Actor,false,ct)));
    [HttpGet("cases/{id:guid}")]
    public Task<IActionResult> Case(Guid id,CancellationToken ct)=>Run(async()=>{await using var conn=await source.OpenConnectionAsync(ct);return Ok(await workflow.LoadCase(conn,id,Actor,false,false,ct));});
    [HttpGet("cases/{id:guid}/attempt")]
    public Task<IActionResult> Own(Guid id,CancellationToken ct)=>Run(async()=>Ok(await workflow.Own(id,Actor,ct)));
    [HttpGet("attempts/{id:guid}")]
    public Task<IActionResult> OwnContext(Guid id,CancellationToken ct)=>Run(async()=>{var(c,a)=await workflow.OwnContext(id,Actor,ct);return Ok(new{caseDetail=c,attempt=a});});
    [HttpPut("attempts/{id:guid}")]
    public Task<IActionResult> Save(Guid id,StudioSaveInput input,CancellationToken ct)=>Run(async()=>Ok(await workflow.Save(id,Actor,input,ct)));
    [HttpPost("attempts/{id:guid}/feedback")]
    public Task<IActionResult> Ask(Guid id,StudioFeedbackInput input,CancellationToken ct)=>Run(async()=>Ok(await workflow.Ask(id,Actor,input.AnswerVersionId,model,options.Value,ct)));
}
