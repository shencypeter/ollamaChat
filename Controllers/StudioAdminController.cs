using System.Security.Claims;
using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
namespace AiTeachingAssistant.Controllers;

[ApiController,Authorize,Route("api/studio/admin")]
public sealed class StudioAdminController(NpgsqlDataSource source,StudioWorkflow workflow,IOptions<OllamaOptions> options):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action){try{return await action();}catch(AssignmentWorkflowException e){return StatusCode(e.StatusCode,new{error=e.Message});}}
    [HttpGet("overview"),Authorize(Roles=PortalRoles.Administrator)]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("SELECT (SELECT count(*) FROM portal_credentials),(SELECT count(*) FROM studio_cases),(SELECT count(*) FROM studio_cases WHERE active_version_id IS NOT NULL),(SELECT count(*) FROM studio_attempts WHERE submitted_at IS NOT NULL AND NOT is_practice)");await using var r=await cmd.ExecuteReaderAsync(ct);await r.ReadAsync(ct);return Ok(new{accounts=r.GetInt64(0),cases=r.GetInt64(1),published=r.GetInt64(2),submitted=r.GetInt64(3)});
    }
    [HttpGet("courses")]
    public async Task<IActionResult> Courses(CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("SELECT c.id,c.code,c.title,c.term_code,c.status FROM courses c JOIN course_members m ON m.course_id=c.id WHERE m.user_id=$1 AND m.status='active' AND m.course_role IN ('teacher','ta') ORDER BY c.created_at DESC");cmd.Parameters.AddWithValue(Actor);await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<object>();while(await r.ReadAsync(ct))result.Add(new{id=r.GetGuid(0),code=r.GetString(1),title=r.GetString(2),termCode=r.GetString(3),status=r.GetString(4)});return Ok(result);
    }
    [HttpGet("cases")]public Task<IActionResult> Cases(CancellationToken ct)=>Run(async()=>Ok(await workflow.Cases(Actor,true,ct)));
    [HttpGet("cases/{id:guid}")]public Task<IActionResult> Case(Guid id,CancellationToken ct)=>Run(async()=>{await using var conn=await source.OpenConnectionAsync(ct);return Ok(await workflow.LoadCase(conn,id,Actor,true,true,ct));});
    [HttpPost("cases"),HttpPut("cases/{id:guid}")]
    public Task<IActionResult> SaveCase(StudioCaseInput input,CancellationToken ct,Guid? id=null)=>Run(async()=>{
        if(string.IsNullOrWhiteSpace(input.Title)||string.IsNullOrWhiteSpace(input.Material)||input.Prompts==null||input.ReferencePoints==null||input.Prompts.Count!=4||input.ReferencePoints.Count!=4||StudioFeedbackContract.Stages.Any(s=>!input.Prompts.ContainsKey(s)||!input.ReferencePoints.ContainsKey(s))||input.Prompts.Any(x=>string.IsNullOrWhiteSpace(x.Value)||x.Value.Length>8000)||input.ReferencePoints.Any(x=>x.Value==null||x.Value.Length>8000))return BadRequest(new{error="請填寫案例原文、四階段結構提示與四階段重點清單（可空白），每階段最多 8000 字元。"});
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);var caseId=id??Guid.NewGuid();var version=Guid.NewGuid();
        await workflow.Access(conn,input.CourseId,Actor,true,ct);
        await using(var check=new NpgsqlCommand("SELECT status FROM courses WHERE id=$1 FOR SHARE",conn)){check.Parameters.AddWithValue(input.CourseId);var status=await check.ExecuteScalarAsync(ct) as string;if(status==null)return BadRequest(new{error="請選擇課程。"});if(status=="archived")return Conflict(new{error="封存課程不能維護案例。"});}
        if(id.HasValue){await using var check=new NpgsqlCommand("SELECT course_id,draft_version_id FROM studio_cases WHERE id=$1 FOR UPDATE",conn);check.Parameters.AddWithValue(id.Value);await using var r=await check.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return NotFound();if(r.GetGuid(0)!=input.CourseId)return BadRequest(new{error="既有案例不能改移課程。"});var prior=r.IsDBNull(1)?(Guid?)null:r.GetGuid(1);if(prior!=input.ExpectedDraftVersionId)return Conflict(new{error="案例已被修改，請重新載入。"});}
        else{await using var create=new NpgsqlCommand("INSERT INTO studio_cases(id,course_id) VALUES($1,$2)",conn);create.Parameters.AddWithValue(caseId);create.Parameters.AddWithValue(input.CourseId);await create.ExecuteNonQueryAsync(ct);}
        await using(var cmd=new NpgsqlCommand("INSERT INTO studio_case_versions(id,case_id,version_no,title,material,prompts,reference_points,created_by) SELECT $1,$2,COALESCE(max(version_no),0)+1,$3,$4,$5::jsonb,$6::jsonb,$7 FROM studio_case_versions WHERE case_id=$2",conn)){
            cmd.Parameters.AddWithValue(version);cmd.Parameters.AddWithValue(caseId);cmd.Parameters.AddWithValue(input.Title.Trim());cmd.Parameters.AddWithValue(input.Material);cmd.Parameters.AddWithValue(JsonSerializer.Serialize(input.Prompts,StudioFeedbackContract.Json));cmd.Parameters.AddWithValue(JsonSerializer.Serialize(input.ReferencePoints,StudioFeedbackContract.Json));cmd.Parameters.AddWithValue(Actor);await cmd.ExecuteNonQueryAsync(ct);
        }
        await using(var update=new NpgsqlCommand("UPDATE studio_cases SET draft_version_id=$2 WHERE id=$1",conn)){update.Parameters.AddWithValue(caseId);update.Parameters.AddWithValue(version);await update.ExecuteNonQueryAsync(ct);}
        await StudioWorkflow.Audit(conn,Actor,caseId,"studio.case.version.created",ct);await tx.CommitAsync(ct);return Ok(new{id=caseId,versionId=version});
    });
    public sealed class PublishInput {public Guid ExpectedDraftVersionId{get;set;}}
    [HttpPost("cases/{id:guid}/publish")]
    public Task<IActionResult> Publish(Guid id,PublishInput input,CancellationToken ct)=>Run(async()=>
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        await workflow.LoadCase(conn,id,Actor,true,false,ct);
        await using var cmd=new NpgsqlCommand("UPDATE studio_cases c SET active_version_id=draft_version_id FROM courses k WHERE c.course_id=k.id AND c.id=$1 AND c.draft_version_id=$2 AND k.status<>'archived' RETURNING c.id",conn);cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(input.ExpectedDraftVersionId);if(await cmd.ExecuteScalarAsync(ct)==null)return Conflict(new{error="案例版本已變更或課程封存，請重新載入。"});
        await StudioWorkflow.Audit(conn,Actor,id,"studio.case.published",ct);await tx.CommitAsync(ct);return NoContent();
    });
    [HttpGet("templates"),Authorize(Roles=PortalRoles.Administrator)]
    public async Task<IActionResult> Templates(CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("SELECT id,version_no,body,created_at FROM studio_templates ORDER BY version_no DESC LIMIT 20");await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<object>();while(await r.ReadAsync(ct))result.Add(new{id=r.GetGuid(0),versionNo=r.GetInt32(1),body=r.GetString(2),createdAt=r.GetFieldValue<DateTimeOffset>(3)});return Ok(result);
    }
    [HttpPost("templates"),Authorize(Roles=PortalRoles.Administrator)]
    public async Task<IActionResult> SaveTemplate(StudioTemplateInput input,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(input.Body))return BadRequest(new{error="模板不可空白。"});await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        await using(var gate=new NpgsqlCommand("SELECT pg_advisory_xact_lock(741026008)",conn))await gate.ExecuteNonQueryAsync(ct);
        await using(var version=new NpgsqlCommand("SELECT max(version_no) FROM studio_templates",conn)){if((int)(await version.ExecuteScalarAsync(ct))! != input.ExpectedVersion)return Conflict(new{error="模板已更新，請重新載入。"});}
        Guid id;await using(var cmd=new NpgsqlCommand("INSERT INTO studio_templates(version_no,body,created_by) SELECT max(version_no)+1,$1,$2 FROM studio_templates RETURNING id",conn)){cmd.Parameters.AddWithValue(input.Body);cmd.Parameters.AddWithValue(Actor);id=(Guid)(await cmd.ExecuteScalarAsync(ct))!;}
        await StudioWorkflow.Audit(conn,Actor,id,"studio.template.created",ct);await tx.CommitAsync(ct);return Ok(new{id});
    }
    [HttpGet("settings"),Authorize(Roles=PortalRoles.Administrator)]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("SELECT feedback_enabled,timeout_seconds,max_requests_per_revision,version FROM studio_settings WHERE id=true");await using var r=await cmd.ExecuteReaderAsync(ct);await r.ReadAsync(ct);return Ok(new{feedbackEnabled=r.GetBoolean(0),timeoutSeconds=r.GetInt32(1),maxRequestsPerRevision=r.GetInt32(2),version=r.GetInt32(3),model=options.Value.Model,profile=options.Value.Profile,deploymentTimeoutSeconds=options.Value.TimeoutSeconds});
    }
    [HttpPut("settings"),Authorize(Roles=PortalRoles.Administrator)]
    public async Task<IActionResult> SaveSettings(StudioSettingsInput input,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        await using var cmd=new NpgsqlCommand("UPDATE studio_settings SET feedback_enabled=$1,timeout_seconds=$2,max_requests_per_revision=$3,version=version+1,updated_by=$4,updated_at=now() WHERE id=true AND version=$5",conn);cmd.Parameters.AddWithValue(input.FeedbackEnabled);cmd.Parameters.AddWithValue(input.TimeoutSeconds);cmd.Parameters.AddWithValue(input.MaxRequestsPerRevision);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(input.ExpectedVersion);if(await cmd.ExecuteNonQueryAsync(ct)==0)return Conflict(new{error="設定已被更新，請重新載入。"});await StudioWorkflow.Audit(conn,Actor,Actor,"studio.settings.updated",ct);await tx.CommitAsync(ct);return NoContent();
    }
    [HttpGet("audit"),Authorize(Roles=PortalRoles.Administrator)]
    public async Task<IActionResult> Audit(CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("SELECT a.id,a.action,a.entity_type,a.entity_id,a.occurred_at,u.display_name FROM audit_events a LEFT JOIN app_users u ON u.id=a.actor_user_id ORDER BY a.id DESC LIMIT 200");await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<object>();while(await r.ReadAsync(ct))result.Add(new{id=r.GetInt64(0),action=r.GetString(1),entityType=r.GetString(2),entityId=r.GetGuid(3),occurredAt=r.GetFieldValue<DateTimeOffset>(4),actor=r.IsDBNull(5)?"系統":r.GetString(5)});return Ok(result);
    }
}
