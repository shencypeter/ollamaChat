using System.Security.Claims;
using System.Text;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
namespace AiTeachingAssistant.Controllers;

[ApiController,Authorize,Route("api/studio/teaching")]
public sealed class StudioTeachingController(NpgsqlDataSource source,StudioWorkflow workflow):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action){try{return await action();}catch(AssignmentWorkflowException e){return StatusCode(e.StatusCode,new{error=e.Message});}}
    [HttpGet("courses")]
    public async Task<IActionResult> Courses(CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("SELECT c.id,c.title,c.code FROM courses c JOIN course_members m ON m.course_id=c.id WHERE m.user_id=$1 AND m.status='active' AND m.course_role IN ('teacher','ta') ORDER BY c.created_at DESC");cmd.Parameters.AddWithValue(Actor);await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<object>();while(await r.ReadAsync(ct))result.Add(new{id=r.GetGuid(0),title=r.GetString(1),code=r.GetString(2)});return Ok(result);
    }
    [HttpGet("courses/{course:guid}/students")]
    public Task<IActionResult> Students(Guid course,CancellationToken ct)=>Run(async()=>{
        await using var conn=await source.OpenConnectionAsync(ct);await workflow.Access(conn,course,Actor,true,ct);
        await using var cmd=new NpgsqlCommand("""
            SELECT u.id,u.display_name,u.student_number,
              count(a.id) FILTER(WHERE a.submitted_at IS NOT NULL),max(a.submitted_at)
            FROM course_members m JOIN app_users u ON u.id=m.user_id
            LEFT JOIN studio_attempts a ON a.student_id=u.id AND NOT a.is_practice AND a.submitted_at IS NOT NULL
              AND EXISTS(SELECT 1 FROM studio_case_versions v JOIN studio_cases c ON c.id=v.case_id WHERE v.id=a.case_version_id AND c.course_id=$1)
            WHERE m.course_id=$1 AND m.course_role='student' AND m.status='active' AND u.status='active'
            GROUP BY u.id ORDER BY u.student_number NULLS LAST,u.display_name,u.id
            """,conn);cmd.Parameters.AddWithValue(course);await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<object>();while(await r.ReadAsync(ct))result.Add(new{id=r.GetGuid(0),displayName=r.GetString(1),studentNumber=r.IsDBNull(2)?null:r.GetString(2),submittedCount=r.GetInt64(3),lastSubmittedAt=r.IsDBNull(4)?(DateTimeOffset?)null:r.GetFieldValue<DateTimeOffset>(4)});return Ok(result);
    });
    private async Task<List<(Guid Student,string Name,string? Number,StudioCase Case,StudioAttempt Attempt)>> Records(NpgsqlConnection conn,Guid course,Guid? student,CancellationToken ct)
    {
        await workflow.Access(conn,course,Actor,true,ct);
        var ids=new List<(Guid Attempt,Guid Case,Guid Version,Guid Student,string Name,string? Number)>();
        await using(var cmd=new NpgsqlCommand("""
            SELECT a.id,c.id,v.id,u.id,u.display_name,u.student_number FROM studio_attempts a
            JOIN studio_case_versions v ON v.id=a.case_version_id JOIN studio_cases c ON c.id=v.case_id JOIN app_users u ON u.id=a.student_id
            JOIN course_members m ON m.course_id=c.course_id AND m.user_id=u.id AND m.course_role='student' AND m.status='active'
            WHERE c.course_id=$1 AND a.submitted_at IS NOT NULL AND NOT a.is_practice AND u.status='active' AND ($2::uuid IS NULL OR a.student_id=$2)
            ORDER BY u.student_number NULLS LAST,u.display_name,a.submitted_at DESC
            """,conn)){
            cmd.Parameters.AddWithValue(course);cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Uuid,Value=(object?)student??DBNull.Value});await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))ids.Add((r.GetGuid(0),r.GetGuid(1),r.GetGuid(2),r.GetGuid(3),r.GetString(4),r.IsDBNull(5)?null:r.GetString(5)));
        }
        var records=new List<(Guid,string,string?,StudioCase,StudioAttempt)>();
        foreach(var row in ids){
            var c=await workflow.LoadCase(conn,row.Case,Actor,false,false,ct,row.Version);
            var a=await workflow.ReadAttempt(conn,row.Attempt,ct);
            // Staff see only the final submitted revision per stage, never private draft history.
            var answers=a.Answers.Where(x=>x.CreatedAt<=a.SubmittedAt).GroupBy(x=>x.Stage).Select(g=>g.OrderByDescending(x=>x.RevisionNo).First()).ToArray();
            a=a with{Answers=answers,Feedback=a.Feedback.Where(f=>answers.Any(x=>x.Id==f.AnswerVersionId) && f.CreatedAt<=a.SubmittedAt).ToArray()};
            records.Add((row.Student,row.Name,row.Number,c,a));
        }
        return records;
    }
    [HttpGet("courses/{course:guid}/students/{student:guid}")]
    public Task<IActionResult> Student(Guid course,Guid student,CancellationToken ct)=>Run(async()=>{
        await using var conn=await source.OpenConnectionAsync(ct);await workflow.Access(conn,course,Actor,true,ct);
        object person;await using(var cmd=new NpgsqlCommand("SELECT u.display_name,u.student_number FROM app_users u JOIN course_members m ON m.user_id=u.id WHERE m.course_id=$1 AND u.id=$2 AND m.status='active' AND m.course_role='student' AND u.status='active'",conn)){
            cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(student);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return NotFound(new{error="找不到本課程有效學生。"});person=new{id=student,displayName=r.GetString(0),studentNumber=r.IsDBNull(1)?null:r.GetString(1)};
        }
        var records=await Records(conn,course,student,ct);return Ok(new{student=person,records=records.Select(x=>new{caseDetail=x.Case,attempt=x.Attempt})});
    });
    [HttpGet("courses/{course:guid}/statistics")]
    public Task<IActionResult> Statistics(Guid course,CancellationToken ct)=>Run(async()=>{
        await using var conn=await source.OpenConnectionAsync(ct);var records=await Records(conn,course,null,ct);long students,cases;
        await using(var cmd=new NpgsqlCommand("SELECT count(*) FROM course_members m JOIN app_users u ON u.id=m.user_id WHERE m.course_id=$1 AND m.course_role='student' AND m.status='active' AND u.status='active'",conn)){cmd.Parameters.AddWithValue(course);students=(long)(await cmd.ExecuteScalarAsync(ct))!;}
        await using(var cmd=new NpgsqlCommand("SELECT count(*) FROM studio_cases WHERE course_id=$1 AND active_version_id IS NOT NULL",conn)){cmd.Parameters.AddWithValue(course);cases=(long)(await cmd.ExecuteScalarAsync(ct))!;}
        return Ok(new{studentCount=students,caseCount=cases,submittedStudents=records.Select(x=>x.Student).Distinct().Count(),submittedAttempts=records.Count,stages=StudioFeedbackContract.Stages.Select(stage=>new{stage,submittedAnswers=records.Sum(x=>x.Attempt.Answers.Count(a=>a.Stage==stage)),completedFeedback=records.Count(x=>x.Attempt.Answers.Where(a=>a.Stage==stage).Any(a=>x.Attempt.Feedback.Any(f=>f.AnswerVersionId==a.Id&&f.Status=="completed")))})});
    });
    public static string CsvCell(string? text)
    {
        text??="";if(text.TrimStart().StartsWith('=')||text.TrimStart().StartsWith('+')||text.TrimStart().StartsWith('-')||text.TrimStart().StartsWith('@')||text.StartsWith('\t')||text.StartsWith('\r'))text="'"+text;
        return "\""+text.Replace("\"","\"\"")+"\"";
    }
    [HttpPost("courses/{course:guid}/export")]
    public Task<IActionResult> Export(Guid course,CancellationToken ct)=>Run(async()=>{
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);var records=await Records(conn,course,null,ct);
        var csv=new StringBuilder("學號,姓名,案例,案例版本,階段,提交時間,正式作答,AI文字回饋\r\n");
        foreach(var record in records)foreach(var a in record.Attempt.Answers){var f=record.Attempt.Feedback.FirstOrDefault(x=>x.AnswerVersionId==a.Id && x.Status=="completed");csv.AppendLine(string.Join(",",new[]{record.Number,record.Name,record.Case.Title,record.Case.VersionNo.ToString(),a.Stage,record.Attempt.SubmittedAt?.ToString("O"),a.Text,f?.Content==null?"":System.Text.Json.JsonSerializer.Serialize(f.Content,StudioFeedbackContract.Json)}.Select(CsvCell)));}
        await tx.CommitAsync(ct);await StudioWorkflow.Audit(conn,Actor,course,"studio.export.downloaded",ct);return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),"text/csv; charset=utf-8","case-learning.csv");
    });
}
