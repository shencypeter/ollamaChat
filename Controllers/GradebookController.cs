using System.Security.Claims;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
namespace AiTeachingAssistant.Controllers;

[ApiController,Authorize,Route("api/courses/{course:guid}/gradebook")]
public sealed class GradebookController(NpgsqlDataSource source,LearnerExperience experience):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<string> Access(NpgsqlConnection conn,Guid course,bool write,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("SELECT m.course_role,c.status FROM course_members m JOIN courses c ON c.id=m.course_id WHERE m.course_id=$1 AND m.user_id=$2 AND m.status='active'"+(write?" FOR UPDATE OF c":""),conn);
        cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(Actor);await using var r=await cmd.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))throw new AssignmentWorkflowException(403,"你不是本課程有效成員。");
        var role=experience.IsPractice?"student":r.GetString(0);
        if(write && (!AssignmentPolicy.IsStaff(role)||r.GetString(1)=="archived"))throw new AssignmentWorkflowException(403,"只有未封存課程的教師／助教可登錄成績。");
        return role;
    }
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {try{return await action();}catch(AssignmentWorkflowException e){return StatusCode(e.StatusCode,new{error=e.Message});}}
    [HttpGet]
    public Task<IActionResult> Read(Guid course,CancellationToken ct)=>Run(async()=>{
        await using var conn=await source.OpenConnectionAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        var staff=AssignmentPolicy.IsStaff(await Access(conn,course,false,ct));
        var items=new List<GradeItem>();var students=new List<GradeStudent>();var scores=new List<GradeScore>();
        await using(var cmd=new NpgsqlCommand("SELECT id,title,category,max_score,weight,version,source_assignment_id,source_report_id FROM course_grade_items WHERE course_id=$1 ORDER BY created_at,id",conn)){
            cmd.Parameters.AddWithValue(course);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))items.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetDecimal(3),r.GetDecimal(4),r.GetInt32(5),r.IsDBNull(6)?null:r.GetGuid(6),r.IsDBNull(7)?null:r.GetGuid(7)));
        }
        await using(var cmd=new NpgsqlCommand("SELECT u.id,u.display_name,u.student_number FROM course_members m JOIN app_users u ON u.id=m.user_id WHERE m.course_id=$1 AND m.course_role='student' AND m.status='active' AND u.status='active' ORDER BY u.student_number NULLS LAST,u.display_name,u.id",conn)){
            cmd.Parameters.AddWithValue(course);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))students.Add(new(r.GetGuid(0),r.GetString(1),r.IsDBNull(2)?null:r.GetString(2)));
        }
        await using(var cmd=new NpgsqlCommand("SELECT s.item_id,s.student_id,s.score,s.version FROM course_grade_scores s JOIN course_members m ON m.course_id=s.course_id AND m.user_id=s.student_id JOIN app_users u ON u.id=m.user_id WHERE s.course_id=$1 AND m.course_role='student' AND m.status='active' AND u.status='active'",conn)){
            cmd.Parameters.AddWithValue(course);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))scores.Add(new(r.GetGuid(0),r.GetGuid(1),r.IsDBNull(2)?null:r.GetDecimal(2),r.GetInt32(3)));
        }
        var summary=GradebookPolicy.Summarize(items,students,scores);
        var visibleStudents=staff?students:students.Where(x=>x.Id==Actor).ToList();
        var visibleScores=staff?scores:scores.Where(x=>x.StudentId==Actor).ToList();
        var visibleAverages=staff?summary.Students:summary.Students.Where(x=>x.StudentId==Actor).ToArray();
        await tx.CommitAsync(ct);
        return Ok(new{items,students=visibleStudents,scores=visibleScores,averages=visibleAverages,itemAverages=summary.Items,summary.ClassAverage,summary.GradedStudentCount,staff,isPractice=experience.IsPractice});
    });
    [HttpPost("items"),HttpPut("items/{id:guid}")]
    public Task<IActionResult> SaveItem(Guid course,GradeItemInput input,CancellationToken ct,Guid? id=null)=>Run(async()=>{
        GradebookPolicy.Validate(input);await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);await Access(conn,course,true,ct);
        var item=id??Guid.NewGuid();
        if(id.HasValue){
            await using(var linked=new NpgsqlCommand("SELECT title,category,max_score FROM course_grade_items WHERE id=$1 AND course_id=$2 AND (source_assignment_id IS NOT NULL OR source_report_id IS NOT NULL)",conn)){linked.Parameters.AddWithValue(id.Value);linked.Parameters.AddWithValue(course);await using var r=await linked.ExecuteReaderAsync(ct);if(await r.ReadAsync(ct) && (r.GetString(0)!=input.Title.Trim()||r.GetString(1)!=input.Category||r.GetDecimal(2)!=input.MaxScore))return BadRequest(new{error="連動評量項目只可調整權重；名稱、類型與滿分請由原評量維護。"});}
            await using var check=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM course_grade_scores WHERE item_id=$1 AND course_id=$2 AND score>$3)",conn);check.Parameters.AddWithValue(item);check.Parameters.AddWithValue(course);check.Parameters.AddWithValue(input.MaxScore);
            if(await check.ExecuteScalarAsync(ct) is true)return BadRequest(new{error="滿分不能低於已登錄得分。"});
        }
        await using var cmd=new NpgsqlCommand(id.HasValue?"UPDATE course_grade_items SET title=$3,category=$4,max_score=$5,weight=$6,version=version+1 WHERE id=$1 AND course_id=$2 AND version=$7 RETURNING id":"INSERT INTO course_grade_items(id,course_id,title,category,max_score,weight,created_by) VALUES($1,$2,$3,$4,$5,$6,$7) RETURNING id",conn);
        cmd.Parameters.AddWithValue(item);cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(input.Title.Trim());cmd.Parameters.AddWithValue(input.Category);cmd.Parameters.AddWithValue(input.MaxScore);cmd.Parameters.AddWithValue(input.Weight);
        if(id.HasValue)cmd.Parameters.AddWithValue(input.ExpectedVersion);else cmd.Parameters.AddWithValue(Actor);
        if(await cmd.ExecuteScalarAsync(ct)==null)return Conflict(new{error="項目不存在或已被修改，請重新載入。"});
        await Audit(conn,item,"gradebook.item.saved",ct);await tx.CommitAsync(ct);return Ok(new{id=item});
    });
    [HttpPut("items/{id:guid}/score")]
    public Task<IActionResult> SaveScore(Guid course,Guid id,GradeScoreInput input,CancellationToken ct)=>Run(async()=>{
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);await Access(conn,course,true,ct);
        await using(var check=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM course_members m JOIN app_users u ON u.id=m.user_id WHERE m.course_id=$1 AND m.user_id=$2 AND m.course_role='student' AND m.status='active' AND u.status='active')",conn)){
            check.Parameters.AddWithValue(course);check.Parameters.AddWithValue(input.StudentId);if(await check.ExecuteScalarAsync(ct) is not true)return BadRequest(new{error="只能登錄本課程有效學生的得分。"});
        }
        await using(var linked=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM course_grade_items WHERE id=$1 AND course_id=$2 AND (source_assignment_id IS NOT NULL OR source_report_id IS NOT NULL))",conn)){linked.Parameters.AddWithValue(id);linked.Parameters.AddWithValue(course);if(await linked.ExecuteScalarAsync(ct) is true)return Conflict(new{error="此成績由線上測驗或 PDF 作業連動，請從原評量評閱並給分。"});}
        decimal max;
        await using(var item=new NpgsqlCommand("SELECT max_score FROM course_grade_items WHERE id=$1 AND course_id=$2 FOR UPDATE",conn)){
            item.Parameters.AddWithValue(id);item.Parameters.AddWithValue(course);if(await item.ExecuteScalarAsync(ct) is not decimal value)return NotFound(new{error="找不到評分項目。"});max=value;
        }
        GradebookPolicy.ValidateScore(input.Score,max);
        int version=0;await using(var old=new NpgsqlCommand("SELECT version FROM course_grade_scores WHERE item_id=$1 AND student_id=$2",conn)){
            old.Parameters.AddWithValue(id);old.Parameters.AddWithValue(input.StudentId);if(await old.ExecuteScalarAsync(ct) is int v)version=v;
        }
        if(version!=input.ExpectedVersion)return Conflict(new{error="得分已被修改，請重新載入。"});
        // Clearing retains a tombstone version, preventing stale tabs from resurrecting old scores.
        await using(var score=new NpgsqlCommand("INSERT INTO course_grade_scores(item_id,course_id,student_id,score,updated_by) VALUES($1,$2,$3,$4,$5) ON CONFLICT(item_id,student_id) DO UPDATE SET score=EXCLUDED.score,version=course_grade_scores.version+1,updated_by=EXCLUDED.updated_by,updated_at=now()",conn)){
            score.Parameters.AddWithValue(id);score.Parameters.AddWithValue(course);score.Parameters.AddWithValue(input.StudentId);score.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Numeric,Value=(object?)input.Score??DBNull.Value});score.Parameters.AddWithValue(Actor);await score.ExecuteNonQueryAsync(ct);
        }
        await Audit(conn,id,"gradebook.score.saved",ct);await tx.CommitAsync(ct);return NoContent();
    });
    private async Task Audit(NpgsqlConnection conn,Guid item,string action,CancellationToken ct)
    {await using var cmd=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action) VALUES($1,'course_grade_items',$2,$3)",conn);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(item);cmd.Parameters.AddWithValue(action);await cmd.ExecuteNonQueryAsync(ct);}
}
