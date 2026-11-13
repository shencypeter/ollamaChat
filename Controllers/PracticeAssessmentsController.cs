using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
namespace AiTeachingAssistant.Controllers;

[ApiController,Authorize,Route("api/courses/{course:guid}/practice")]
public sealed class PracticeAssessmentsController(NpgsqlDataSource source,LearnerExperience experience):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<bool> Allowed(NpgsqlConnection conn,Guid course,bool write,CancellationToken ct)
    {
        if(write&&experience.IsPractice)return false;
        await using var cmd=new NpgsqlCommand("SELECT m.course_role,c.status FROM course_members m JOIN courses c ON c.id=m.course_id WHERE m.course_id=$1 AND m.user_id=$2 AND m.status='active'"+(write?" FOR UPDATE OF c":""),conn);
        cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(Actor);await using var r=await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) && r.GetString(0) is "teacher" or "ta" && (!write||r.GetString(1)!="archived");
    }
    private sealed record Record(string Kind,Guid Id,Guid TargetId,string Title,decimal MaxScore,decimal Weight,DateTimeOffset SubmittedAt,decimal? Score,string Feedback,int Version,Guid? ExpectedGradeId,JsonElement? Answers,string? FileName,QuizGradingResult? Quiz=null,Dictionary<Guid,decimal>? EssayScores=null);
    public sealed class GradeInput {
        [Required] public decimal? Score {get;set;}
        [StringLength(16000)] public string Feedback {get;set;}="";
        [Required] public Dictionary<Guid,decimal> EssayScores {get;set;}=[];
        public Guid? ExpectedGradeId {get;set;}
        public int ExpectedVersion {get;set;}
    }
    [HttpGet]
    public async Task<IActionResult> List(Guid course,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        if(!await Allowed(conn,course,false,ct))return Forbid();var records=new List<Record>();
        await using(var cmd=new NpgsqlCommand("""
            SELECT r.id,a.id,a.title,a.max_score,1::numeric,r.submitted_at,g.score,COALESCE(g.feedback,''),g.id,
              (SELECT jsonb_agg(jsonb_build_object('prompt',q.prompt_text,'text',v->>'text','questionType',q.question_type,'options',q.options,'standardAnswer',CASE WHEN $3::boolean THEN q.standard_answer ELSE NULL END,'gradingGuidance',CASE WHEN $3::boolean THEN q.grading_guidance ELSE NULL END) ORDER BY q.sort_order)::text
               FROM jsonb_array_elements(r.answers) v JOIN assignment_questions q ON q.id=(v->>'questionId')::uuid)
            FROM submissions s JOIN assignments a ON a.id=s.assignment_id JOIN course_grade_items i ON i.source_assignment_id=a.id
            JOIN LATERAL (SELECT * FROM submission_revisions WHERE submission_id=s.id ORDER BY revision_no DESC LIMIT 1) r ON true
            LEFT JOIN LATERAL (SELECT * FROM grades WHERE submission_revision_id=r.id ORDER BY graded_at DESC,id DESC LIMIT 1) g ON true
            WHERE a.course_id=$1 AND s.student_user_id=$2 AND s.is_practice AND s.status='submitted' ORDER BY r.submitted_at DESC
            """,conn)){
            cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(!experience.IsPractice);await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))records.Add(new("quiz",r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetDecimal(3),r.GetDecimal(4),r.GetFieldValue<DateTimeOffset>(5),r.IsDBNull(6)?null:r.GetDecimal(6),r.GetString(7),0,r.IsDBNull(8)?null:r.GetGuid(8),r.IsDBNull(9)?null:JsonSerializer.Deserialize<JsonElement>(r.GetString(9)),null));
        }
        await using(var cmd=new NpgsqlCommand("SELECT s.id,a.id,a.title,a.max_score,1::numeric,s.submitted_at,s.score,s.feedback,s.version,s.file_name FROM report_submissions s JOIN report_assignments a ON a.id=s.report_id JOIN course_grade_items i ON i.source_report_id=a.id WHERE a.course_id=$1 AND s.student_id=$2 AND s.is_practice ORDER BY s.submitted_at DESC",conn)){
            cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(Actor);await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))records.Add(new("report",r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetDecimal(3),r.GetDecimal(4),r.GetFieldValue<DateTimeOffset>(5),r.IsDBNull(6)?null:r.GetDecimal(6),r.GetString(7),r.GetInt32(8),null,null,r.GetString(9)));
        }
        for(var index=0;index<records.Count;index++){
            var record=records[index];if(record.Kind!="quiz")continue;var marks=await QuizGrading.Read(conn,record.Id,ct);if(marks==null)continue;
            Dictionary<Guid,decimal> essays=[];if(record.ExpectedGradeId.HasValue){await using var cmd=new NpgsqlCommand("SELECT essay_scores::text FROM grades WHERE id=$1",conn);cmd.Parameters.AddWithValue(record.ExpectedGradeId.Value);essays=JsonSerializer.Deserialize<Dictionary<Guid,decimal>>((string)(await cmd.ExecuteScalarAsync(ct))!)!;}
            var pending=marks.Count(m=>m.QuestionType=="essay"&&!essays.ContainsKey(m.QuestionId));records[index]=record with{Quiz=new(marks,marks.Sum(m=>m.AutoScore??0),pending,pending==0),EssayScores=essays};
        }
        var scored=records.Where(r=>r.Score.HasValue).ToArray();decimal? average=scored.Length==0?null:Math.Round(scored.Average(r=>r.Score!.Value/r.MaxScore*100),2,MidpointRounding.AwayFromZero);
        await tx.CommitAsync(ct);return Ok(new{records,average,canGrade=!experience.IsPractice,isPractice=true});
    }
    [HttpPut("{kind}/{id:guid}/grade")]
    public async Task<IActionResult> Grade(Guid course,string kind,Guid id,GradeInput input,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);if(!await Allowed(conn,course,true,ct))return Forbid();
        if(kind is not ("quiz" or "report"))return NotFound();
        decimal max;
        var sql=kind=="quiz"?"SELECT a.max_score FROM submission_revisions r JOIN submissions s ON s.id=r.submission_id JOIN assignments a ON a.id=s.assignment_id JOIN course_grade_items i ON i.source_assignment_id=a.id WHERE r.id=$1 AND a.course_id=$2 AND s.student_user_id=$3 AND s.is_practice AND s.status='submitted' AND r.id=(SELECT id FROM submission_revisions WHERE submission_id=s.id ORDER BY revision_no DESC LIMIT 1) FOR UPDATE OF a":"SELECT a.max_score FROM report_submissions s JOIN report_assignments a ON a.id=s.report_id WHERE s.id=$1 AND a.course_id=$2 AND s.student_id=$3 AND s.is_practice FOR UPDATE OF a,s";
        await using(var cmd=new NpgsqlCommand(sql,conn)){cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(Actor);if(await cmd.ExecuteScalarAsync(ct) is not decimal value)return NotFound();max=value;}
        try{GradebookPolicy.ValidateScore(input.Score,max);}catch(AssignmentWorkflowException e){return StatusCode(e.StatusCode,new{error=e.Message});}
        if(kind=="quiz"){
            var marks=await QuizGrading.Read(conn,id,ct);
            if(marks!=null){try{if(marks.All(m=>m.QuestionType!="essay"))return BadRequest(new{error="客觀題已自動評分，不能手動改分。"});input.EssayScores=QuizGrading.Essays(marks,input.EssayScores,input.Score!.Value);if(QuizGrading.Total(marks,input.EssayScores)!=input.Score.Value)return BadRequest(new{error="總分須等於自動得分加申論題得分。"});}catch(AssignmentWorkflowException e){return StatusCode(e.StatusCode,new{error=e.Message});}}
            Guid? latest=null;await using(var cmd=new NpgsqlCommand("SELECT id FROM grades WHERE submission_revision_id=$1 ORDER BY graded_at DESC,id DESC LIMIT 1",conn)){cmd.Parameters.AddWithValue(id);if(await cmd.ExecuteScalarAsync(ct) is Guid prior)latest=prior;}
            if(latest!=input.ExpectedGradeId)return Conflict(new{error="練習成績已變更，請重新載入。"});
            await using var insert=new NpgsqlCommand("INSERT INTO grades(submission_revision_id,grader_user_id,score,feedback,released_at,supersedes_grade_id,essay_scores) VALUES($1,$2,$3,$4,now(),$5,$6)",conn);
            insert.Parameters.AddWithValue(id);insert.Parameters.AddWithValue(Actor);insert.Parameters.AddWithValue(input.Score!.Value);insert.Parameters.AddWithValue(input.Feedback);insert.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Uuid,Value=(object?)latest??DBNull.Value});insert.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Jsonb,JsonSerializer.Serialize(input.EssayScores));await insert.ExecuteNonQueryAsync(ct);
        }else{
            await using var update=new NpgsqlCommand("UPDATE report_submissions SET score=$3,feedback=$4,graded_by=$2,graded_at=now(),version=version+1 WHERE id=$1 AND student_id=$2 AND is_practice AND version=$5",conn);
            update.Parameters.AddWithValue(id);update.Parameters.AddWithValue(Actor);update.Parameters.AddWithValue(input.Score!.Value);update.Parameters.AddWithValue(input.Feedback);update.Parameters.AddWithValue(input.ExpectedVersion);if(await update.ExecuteNonQueryAsync(ct)!=1)return Conflict(new{error="練習成績已變更，請重新載入。"});
        }
        await StudioWorkflow.Audit(conn,Actor,id,"practice.grade_saved",ct);await tx.CommitAsync(ct);return NoContent();
    }
    [HttpGet("reports/{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid course,Guid id,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);if(!await Allowed(conn,course,false,ct))return Forbid();
        await using var cmd=new NpgsqlCommand("SELECT s.file_name,s.content FROM report_submissions s JOIN report_assignments a ON a.id=s.report_id WHERE s.id=$1 AND a.course_id=$2 AND s.student_id=$3 AND s.is_practice",conn);cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(Actor);
        await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return NotFound();var bytes=r.GetFieldValue<byte[]>(1);if(FilePreviewPolicy.ContentType(r.GetString(0),bytes)!="application/pdf")return StatusCode(415);
        Response.Headers["X-Content-Type-Options"]="nosniff";return File(bytes,"application/pdf");
    }
}
