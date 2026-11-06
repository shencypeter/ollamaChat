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
public sealed class StudioController(NpgsqlDataSource source,StudioWorkflow workflow,LearnerExperience experience,IOllamaClient model,IOptions<OllamaOptions> options):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action){try{return await action();}catch(AssignmentWorkflowException e){return StatusCode(e.StatusCode,new{error=e.Message});}}
    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("SELECT u.display_name,u.student_number,COALESCE(p.department_year,''),p.completed_at IS NOT NULL AND length(trim(u.display_name))>0 AND length(trim(u.student_number))>0 FROM app_users u LEFT JOIN studio_profiles p ON p.user_id=u.id WHERE u.id=$1");cmd.Parameters.AddWithValue(Actor);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return Unauthorized();return Ok(new{displayName=r.GetString(0),studentNumber=r.IsDBNull(1)?"":r.GetString(1),departmentYear=r.GetString(2),completed=!r.IsDBNull(3)&&r.GetBoolean(3)});
    }
    [HttpPut("profile")]
    public async Task<IActionResult> SaveProfile(StudioProfileInput input,CancellationToken ct)
    {
        if(experience.IsPractice)return Forbid();
        if(string.IsNullOrWhiteSpace(input.DisplayName)||string.IsNullOrWhiteSpace(input.StudentNumber))return BadRequest(new{error="姓名與學號為必填。"});
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        try{
            await using(var cmd=new NpgsqlCommand("UPDATE app_users SET display_name=$2,student_number=$3 WHERE id=$1",conn)){cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(input.DisplayName.Trim());cmd.Parameters.AddWithValue(input.StudentNumber.Trim());await cmd.ExecuteNonQueryAsync(ct);}
            await using(var cmd=new NpgsqlCommand("INSERT INTO studio_profiles(user_id,department_year) VALUES($1,$2) ON CONFLICT(user_id) DO UPDATE SET department_year=EXCLUDED.department_year,completed_at=now()",conn)){cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(input.DepartmentYear.Trim());await cmd.ExecuteNonQueryAsync(ct);}
            await StudioWorkflow.Audit(conn,Actor,Actor,"studio.profile.updated",ct);await tx.CommitAsync(ct);return NoContent();
        }catch(PostgresException e)when(e.SqlState==PostgresErrorCodes.UniqueViolation){return Conflict(new{error="此學號已使用，請核對或聯絡管理者。"});}
    }
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
