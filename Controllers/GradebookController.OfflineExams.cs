using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
namespace AiTeachingAssistant.Controllers;

public sealed partial class GradebookController
{
    [CourseCapability("grading"),HttpPut("items/{id:guid}/scores")]
    public Task<IActionResult> SaveExamScores(Guid course,Guid id,OfflineExamBatchInput input,CancellationToken ct)=>ExamBatch(course,id,input,false,ct);

    [CourseCapability("grading"),HttpPost("items/{id:guid}/publish-scores")]
    public Task<IActionResult> PublishExamScores(Guid course,Guid id,OfflineExamBatchInput input,CancellationToken ct)=>ExamBatch(course,id,input,true,ct);

    private Task<IActionResult> ExamBatch(Guid course,Guid id,OfflineExamBatchInput input,bool publish,CancellationToken ct)=>Run(async()=>{
        await using var conn=await source.OpenConnectionAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(ct);
        await Access(conn,course,true,ct);
        decimal max;int version;
        await using(var cmd=new NpgsqlCommand("SELECT max_score,version,category,source_assignment_id,source_report_id FROM course_grade_items WHERE id=$1 AND course_id=$2 FOR UPDATE",conn)){
            cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);await using var r=await cmd.ExecuteReaderAsync(ct);
            if(!await r.ReadAsync(ct))return NotFound(new{error="找不到本課程的考試項目。"});
            max=r.GetDecimal(0);version=r.GetInt32(1);
            if(r.GetString(2) is not ("midterm" or "final")||!r.IsDBNull(3)||!r.IsDBNull(4))return BadRequest(new{error="此入口僅供紙本期中／期末考全班登分，線上評量請從原評量維護。"});
        }
        // Lock the current roster and scores until the entire exam is saved/published.
        var students=new List<Guid>();var scores=new List<GradeScore>();
        await using(var cmd=new NpgsqlCommand("SELECT m.user_id FROM course_members m JOIN app_users u ON u.id=m.user_id WHERE m.course_id=$1 AND m.course_role='student' AND m.status='active' AND u.status='active' ORDER BY m.user_id FOR SHARE OF m,u",conn)){
            cmd.Parameters.AddWithValue(course);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))students.Add(r.GetGuid(0));
        }
        await using(var cmd=new NpgsqlCommand("SELECT student_id,score,version,published_score FROM course_grade_scores WHERE item_id=$1 AND course_id=$2 FOR UPDATE",conn)){
            cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))scores.Add(new(id,r.GetGuid(0),r.IsDBNull(1)?null:r.GetDecimal(1),r.GetInt32(2),r.IsDBNull(3)?null:r.GetDecimal(3)));
        }
        OfflineExamGradePolicy.Validate(input,version,students,scores,max,publish);
        if(publish){
            await using var cmd=new NpgsqlCommand("UPDATE course_grade_scores SET published_score=score,version=version+1,updated_by=$3,updated_at=now() WHERE item_id=$1 AND course_id=$2 AND student_id=ANY($4)",conn);
            cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(students.ToArray());await cmd.ExecuteNonQueryAsync(ct);
        }else foreach(var score in input.Scores){
            await using var cmd=new NpgsqlCommand("INSERT INTO course_grade_scores(item_id,course_id,student_id,score,updated_by) VALUES($1,$2,$3,$4,$5) ON CONFLICT(item_id,student_id) DO UPDATE SET score=EXCLUDED.score,version=course_grade_scores.version+1,updated_by=EXCLUDED.updated_by,updated_at=now()",conn);
            cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(score.StudentId);cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Numeric,Value=(object?)score.Score??DBNull.Value});cmd.Parameters.AddWithValue(Actor);await cmd.ExecuteNonQueryAsync(ct);
        }
        await Audit(conn,id,publish?"gradebook.exam.published":"gradebook.exam.drafts.saved",ct);
        await tx.CommitAsync(ct);return NoContent();
    });
}
