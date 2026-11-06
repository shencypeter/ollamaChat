using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
namespace AiTeachingAssistant.Controllers;
[ApiController,Authorize,Route("api/assignments/{assignment:guid}/discussions")]
public sealed class AssignmentDiscussionsController(NpgsqlDataSource source,AssignmentWorkflow workflow,LearnerExperience experience):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action){try{return await action();}catch(AssignmentWorkflowException e){return StatusCode(e.StatusCode,new{error=e.Message});}}
    public sealed class AskInput { [Required,StringLength(8000)]public string Question{get;set;}=""; }
    public sealed class ReplyInput { [StringLength(16000)]public string Response{get;set;}="";public bool IsPublic{get;set;} }
    [HttpGet]public Task<IActionResult> List(Guid assignment,CancellationToken ct)=>Run(async()=>{
        var a=await workflow.Get(assignment,Actor,ct);var staff=AssignmentPolicy.IsStaff(a.CourseRole);
        await using var cmd=source.CreateCommand("SELECT d.id,u.display_name,d.question,d.response,d.is_public,d.created_at,d.updated_at FROM assignment_discussions d JOIN app_users u ON u.id=d.author_id WHERE d.assignment_id=$1 AND ($2 OR d.is_public OR d.author_id=$3) ORDER BY d.created_at");cmd.Parameters.AddWithValue(assignment);cmd.Parameters.AddWithValue(staff);cmd.Parameters.AddWithValue(Actor);
        await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<object>();while(await r.ReadAsync(ct))result.Add(new{id=r.GetGuid(0),author=r.GetString(1),question=r.GetString(2),response=r.GetString(3),isPublic=r.GetBoolean(4),createdAt=r.GetFieldValue<DateTimeOffset>(5),updatedAt=r.GetFieldValue<DateTimeOffset>(6)});return Ok(result);
    });
    [HttpPost]public Task<IActionResult> Ask(Guid assignment,AskInput request,CancellationToken ct)=>Run(async()=>{
        var a=await workflow.Get(assignment,Actor,ct);if(a.CourseRole!="student"||experience.IsPractice)return Forbid();
        if(a.CourseStatus=="archived"||string.IsNullOrWhiteSpace(request.Question))return BadRequest(new{error="無法新增空白問題，封存課程停止提問。"});
        await using var cmd=source.CreateCommand("INSERT INTO assignment_discussions(assignment_id,author_id,question) VALUES($1,$2,$3) RETURNING id");cmd.Parameters.AddWithValue(assignment);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(request.Question.Trim());return Ok(new{id=await cmd.ExecuteScalarAsync(ct)});
    });
    [HttpPut("{id:guid}")]public Task<IActionResult> Reply(Guid assignment,Guid id,ReplyInput request,CancellationToken ct)=>Run(async()=>{
        var a=await workflow.Get(assignment,Actor,ct);if(!AssignmentPolicy.IsStaff(a.CourseRole)||a.CourseStatus=="archived")return Forbid();
        await using var cmd=source.CreateCommand("UPDATE assignment_discussions SET response=$3,is_public=$4,answered_by=$5,updated_at=now() WHERE id=$1 AND assignment_id=$2");cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(assignment);cmd.Parameters.AddWithValue(request.Response);cmd.Parameters.AddWithValue(request.IsPublic);cmd.Parameters.AddWithValue(Actor);return await cmd.ExecuteNonQueryAsync(ct)==0?NotFound():NoContent();
    });
}
