using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;
namespace AiTeachingAssistant.Controllers;

[ApiController,Authorize]
public sealed class LearningAssessmentsController(NpgsqlDataSource source,StudioWorkflow studio,LearnerExperience experience,TimeProvider clock):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public sealed class QuizInput{[Required]public SaveAssignmentRequest Assignment{get;set;}=new();[Range(0.001,999999)]public decimal Weight{get;set;}=1;}
    public sealed class ReportInput{
        [Required,StringLength(200)]public string Title{get;set;}="";
        [StringLength(24000)]public string Instructions{get;set;}="";
        [Range(0.01,999999)]public decimal MaxScore{get;set;}=100;
        [Range(0.001,999999)]public decimal Weight{get;set;}=1;
        public DateTimeOffset? OpensAt{get;set;}public DateTimeOffset? DueAt{get;set;}public int ExpectedVersion{get;set;}
    }
    public sealed class ReportGradeInput{[Required]public decimal? Score{get;set;}[StringLength(16000)]public string Feedback{get;set;}="";public int ExpectedVersion{get;set;}}
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action){try{return await action();}catch(AssignmentWorkflowException e){return StatusCode(e.StatusCode,new{error=e.Message});}}
    private async Task<string> Access(NpgsqlConnection conn,Guid course,bool write,CancellationToken ct)
    {
        var role=await studio.Access(conn,course,Actor,false,ct);
        await using var cmd=new NpgsqlCommand("SELECT status FROM courses WHERE id=$1"+(write?" FOR SHARE":""),conn);cmd.Parameters.AddWithValue(course);if(write && await cmd.ExecuteScalarAsync(ct) as string=="archived")throw new AssignmentWorkflowException(409,"封存課程無法修改評量。");return role;
    }
    private static void Date(NpgsqlCommand cmd,DateTimeOffset? value)=>cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlDbType.TimestampTz,Value=(object?)value?.UtcDateTime??DBNull.Value});
    [HttpGet("api/courses/{course:guid}/assessments")]
    public Task<IActionResult> List(Guid course,CancellationToken ct)=>Run(async()=>{
        await using var conn=await source.OpenConnectionAsync(ct);var role=await Access(conn,course,false,ct);var staff=StudioWorkflow.Staff(role);var quizzes=new List<object>();var reports=new List<object>();
        await using(var cmd=new NpgsqlCommand("SELECT a.id,a.title,a.status,a.due_at,i.weight,COALESCE(s.status,'not_started') FROM assignments a JOIN course_grade_items i ON i.source_assignment_id=a.id LEFT JOIN submissions s ON s.assignment_id=a.id AND s.student_user_id=$2 AND s.attempt_no=1 AND s.is_practice=$4 WHERE a.course_id=$1 AND ($3 OR a.status<>'draft') ORDER BY a.created_at DESC",conn)){
            cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(staff);cmd.Parameters.AddWithValue(experience.IsPractice);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))quizzes.Add(new{id=r.GetGuid(0),title=r.GetString(1),status=r.GetString(2),dueAt=r.IsDBNull(3)?(DateTimeOffset?)null:r.GetFieldValue<DateTimeOffset>(3),weight=r.GetDecimal(4),myStatus=r.GetString(5)});
        }
        await using(var cmd=new NpgsqlCommand("SELECT a.id,a.title,a.status,a.due_at,i.weight,s.id IS NOT NULL FROM report_assignments a JOIN course_grade_items i ON i.source_report_id=a.id LEFT JOIN report_submissions s ON s.report_id=a.id AND s.student_id=$2 AND s.is_practice=$4 WHERE a.course_id=$1 AND ($3 OR a.status<>'draft') ORDER BY a.created_at DESC",conn)){
            cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(staff);cmd.Parameters.AddWithValue(experience.IsPractice);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))reports.Add(new{id=r.GetGuid(0),title=r.GetString(1),status=r.GetString(2),dueAt=r.IsDBNull(3)?(DateTimeOffset?)null:r.GetFieldValue<DateTimeOffset>(3),weight=r.GetDecimal(4),submitted=r.GetBoolean(5)});
        }
        return Ok(new{quizzes,reports,staff});
    });
    [HttpPost("api/courses/{course:guid}/quizzes")]
    public Task<IActionResult> CreateQuiz(Guid course,QuizInput input,CancellationToken ct)=>Run(async()=>{
        AssignmentWorkflow.Validate(input.Assignment);if(input.Assignment.AllowRevisions || input.Assignment.RubricId.HasValue)return BadRequest(new{error="線上測驗採一次提交與直接人工評分。"});GradebookPolicy.Validate(new(){Title=input.Assignment.Title,Category="quiz",MaxScore=input.Assignment.MaxScore,Weight=input.Weight});
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);AssignmentPolicy.RequireStaff(await Access(conn,course,true,ct));
        var id=Guid.NewGuid();await using(var cmd=new NpgsqlCommand("INSERT INTO assignments(id,course_id,title,instructions,opens_at,due_at,max_score,allow_revisions) VALUES($1,$2,$3,$4,$5,$6,$7,false)",conn)){
            cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(input.Assignment.Title.Trim());cmd.Parameters.AddWithValue(input.Assignment.Instructions);Date(cmd,input.Assignment.OpensAt);Date(cmd,input.Assignment.DueAt);cmd.Parameters.AddWithValue(input.Assignment.MaxScore);await cmd.ExecuteNonQueryAsync(ct);
        }
        await AssignmentWorkflow.WriteQuestions(conn,id,input.Assignment,ct);
        await LearningTime.SaveRequirements(conn,id,course,input.Assignment.LearningRequirements,ct);
        await using(var item=new NpgsqlCommand("INSERT INTO course_grade_items(course_id,title,category,max_score,weight,created_by,source_assignment_id) VALUES($1,$2,'quiz',$3,$4,$5,$6)",conn)){
            item.Parameters.AddWithValue(course);item.Parameters.AddWithValue(input.Assignment.Title.Trim());item.Parameters.AddWithValue(input.Assignment.MaxScore);item.Parameters.AddWithValue(input.Weight);item.Parameters.AddWithValue(Actor);item.Parameters.AddWithValue(id);await item.ExecuteNonQueryAsync(ct);
        }
        await StudioWorkflow.Audit(conn,Actor,id,"quiz.created",ct);await tx.CommitAsync(ct);return Ok(new{id});
    });
    [HttpPost("api/courses/{course:guid}/reports"),HttpPut("api/reports/{id:guid}")]
    public Task<IActionResult> SaveReport(ReportInput input,CancellationToken ct,Guid? course=null,Guid? id=null)=>Run(async()=>{
        GradebookPolicy.Validate(new(){Title=input.Title,Category="assignment",MaxScore=input.MaxScore,Weight=input.Weight});if(input.OpensAt.HasValue && input.DueAt<=input.OpensAt)return BadRequest(new{error="截止時間必須晚於開放時間。"});
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);Guid courseId;
        if(id.HasValue){await using var find=new NpgsqlCommand("SELECT course_id FROM report_assignments WHERE id=$1",conn);find.Parameters.AddWithValue(id.Value);if(await find.ExecuteScalarAsync(ct) is not Guid c)return NotFound();courseId=c;}else courseId=course??Guid.Empty;
        AssignmentPolicy.RequireStaff(await Access(conn,courseId,true,ct));var report=id??Guid.NewGuid();
        await using(var cmd=new NpgsqlCommand(id.HasValue?"UPDATE report_assignments SET title=$2,instructions=$3,max_score=$4,opens_at=$5,due_at=$6,version=version+1 WHERE id=$1 AND status='draft' AND version=$7":"INSERT INTO report_assignments(id,title,instructions,max_score,opens_at,due_at,course_id,created_by) VALUES($1,$2,$3,$4,$5,$6,$7,$8)",conn)){
            cmd.Parameters.AddWithValue(report);cmd.Parameters.AddWithValue(input.Title.Trim());cmd.Parameters.AddWithValue(input.Instructions);cmd.Parameters.AddWithValue(input.MaxScore);Date(cmd,input.OpensAt);Date(cmd,input.DueAt);if(id.HasValue)cmd.Parameters.AddWithValue(input.ExpectedVersion);else{cmd.Parameters.AddWithValue(courseId);cmd.Parameters.AddWithValue(Actor);}if(await cmd.ExecuteNonQueryAsync(ct)==0)return Conflict(new{error="僅可修改最新的未發布 PDF 作業草稿。"});
        }
        await using(var item=new NpgsqlCommand(id.HasValue?"UPDATE course_grade_items SET title=$2,max_score=$3,version=version+1 WHERE source_report_id=$1":"INSERT INTO course_grade_items(source_report_id,title,max_score,weight,category,course_id,created_by) VALUES($1,$2,$3,$4,'assignment',$5,$6)",conn)){
            item.Parameters.AddWithValue(report);item.Parameters.AddWithValue(input.Title.Trim());item.Parameters.AddWithValue(input.MaxScore);if(!id.HasValue){item.Parameters.AddWithValue(input.Weight);item.Parameters.AddWithValue(courseId);item.Parameters.AddWithValue(Actor);}await item.ExecuteNonQueryAsync(ct);
        }
        await StudioWorkflow.Audit(conn,Actor,report,"report.saved",ct);await tx.CommitAsync(ct);return Ok(new{id=report});
    });
    private sealed record Report(Guid Id,Guid CourseId,string Title,string Instructions,string Status,decimal MaxScore,DateTimeOffset? OpensAt,DateTimeOffset? DueAt,int Version,decimal Weight,Guid ItemId,string CourseRole,bool IsPractice=false);
    private async Task<Report> ReadReport(NpgsqlConnection conn,Guid id,bool update,CancellationToken ct)
    {
        Report report;await using(var cmd=new NpgsqlCommand("SELECT a.course_id,a.title,a.instructions,a.status,a.max_score,a.opens_at,a.due_at,a.version,i.weight,i.id FROM report_assignments a JOIN course_grade_items i ON i.source_report_id=a.id WHERE a.id=$1",conn)){
            cmd.Parameters.AddWithValue(id);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new AssignmentWorkflowException(404,"找不到 PDF 作業。");report=new(id,r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetDecimal(4),r.IsDBNull(5)?null:r.GetFieldValue<DateTimeOffset>(5),r.IsDBNull(6)?null:r.GetFieldValue<DateTimeOffset>(6),r.GetInt32(7),r.GetDecimal(8),r.GetGuid(9),"");
        }
        var role=await Access(conn,report.CourseId,update,ct);
        if(update){await using var gate=new NpgsqlCommand("SELECT id FROM report_assignments WHERE id=$1 FOR UPDATE",conn);gate.Parameters.AddWithValue(id);await gate.ExecuteScalarAsync(ct);return await ReadReport(conn,id,false,ct);}
        if(!StudioWorkflow.Staff(role)&&report.Status=="draft")throw new AssignmentWorkflowException(404,"找不到已發布的 PDF 作業。");return report with{CourseRole=role,IsPractice=experience.IsPractice};
    }
    [HttpGet("api/reports/{id:guid}")]
    public Task<IActionResult> Detail(Guid id,CancellationToken ct)=>Run(async()=>{await using var conn=await source.OpenConnectionAsync(ct);return Ok(await ReadReport(conn,id,false,ct));});
    [HttpPost("api/reports/{id:guid}/{actionName}")]
    public Task<IActionResult> Transition(Guid id,string actionName,CancellationToken ct)=>Run(async()=>{
        if(actionName is not ("publish" or "close"))return NotFound();await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);var report=await ReadReport(conn,id,true,ct);AssignmentPolicy.RequireStaff(report.CourseRole);
        if(report.Status!=(actionName=="publish"?"draft":"published"))return Conflict(new{error="作業狀態已變更。"});await using var cmd=new NpgsqlCommand("UPDATE report_assignments SET status=$2,version=version+1 WHERE id=$1",conn);cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(actionName=="publish"?"published":"closed");await cmd.ExecuteNonQueryAsync(ct);await StudioWorkflow.Audit(conn,Actor,id,"report."+actionName,ct);await tx.CommitAsync(ct);return NoContent();
    });
    [HttpPost("api/reports/{id:guid}/submit"),RequestSizeLimit(22*1024*1024)]
    public Task<IActionResult> Submit(Guid id,IFormFile file,CancellationToken ct)=>Run(async()=>{
        var name=Path.GetFileName(file.FileName.Replace('\\','/'));if(file.Length is <5 or >20*1024*1024 || name.Length>200 || !name.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase))return BadRequest(new{error="請上傳 20 MB 內的 PDF 報告。"});
        using var buffer=new MemoryStream();await file.CopyToAsync(buffer,ct);var bytes=buffer.ToArray();if(!bytes.AsSpan(0,5).SequenceEqual("%PDF-"u8))return BadRequest(new{error="檔案內容不是有效的 PDF 檔頭。"});
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);var report=await ReadReport(conn,id,true,ct);var now=clock.GetUtcNow();
        if(report.CourseRole!="student")return Forbid();if(report.Status!="published"||(report.OpensAt.HasValue&&now<report.OpensAt)||(report.DueAt.HasValue&&now>=report.DueAt))return Conflict(new{error="目前不在 PDF 作業繳交期間。"});
        try{await using var cmd=new NpgsqlCommand("INSERT INTO report_submissions(report_id,student_id,file_name,content,is_practice) VALUES($1,$2,$3,$4,$5)",conn);cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(name);cmd.Parameters.AddWithValue(bytes);cmd.Parameters.AddWithValue(experience.IsPractice);await cmd.ExecuteNonQueryAsync(ct);}catch(PostgresException e)when(e.SqlState==PostgresErrorCodes.UniqueViolation){return Conflict(new{error="此 PDF 作業已繳交，不能覆寫原報告。"});}
        await StudioWorkflow.Audit(conn,Actor,id,"report.submitted",ct);await tx.CommitAsync(ct);return NoContent();
    });
    [HttpGet("api/reports/{id:guid}/submissions")]
    public Task<IActionResult> Submissions(Guid id,CancellationToken ct)=>Run(async()=>{
        await using var conn=await source.OpenConnectionAsync(ct);var report=await ReadReport(conn,id,false,ct);var staff=StudioWorkflow.Staff(report.CourseRole);
        await using var cmd=new NpgsqlCommand("SELECT s.id,u.display_name,u.student_number,s.file_name,s.submitted_at,s.score,s.feedback,s.version FROM report_submissions s JOIN app_users u ON u.id=s.student_id WHERE s.report_id=$1 AND s.is_practice=$4 AND ($2 OR s.student_id=$3) ORDER BY u.student_number NULLS LAST,u.display_name",conn);cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(staff);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(experience.IsPractice);await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<object>();while(await r.ReadAsync(ct))result.Add(new{id=r.GetGuid(0),studentName=r.GetString(1),studentNumber=r.IsDBNull(2)?null:r.GetString(2),fileName=r.GetString(3),submittedAt=r.GetFieldValue<DateTimeOffset>(4),score=r.IsDBNull(5)?(decimal?)null:r.GetDecimal(5),feedback=r.GetString(6),version=r.GetInt32(7)});return Ok(result);
    });
    [HttpPost("api/reports/{id:guid}/submissions/{submission:guid}/download")]
    public Task<IActionResult> Download(Guid id,Guid submission,CancellationToken ct)=>ReadFile(id,submission,false,ct);
    [HttpGet("api/reports/{id:guid}/submissions/{submission:guid}/preview")]
    public Task<IActionResult> Preview(Guid id,Guid submission,CancellationToken ct)=>ReadFile(id,submission,true,ct);
    private Task<IActionResult> ReadFile(Guid id,Guid submission,bool preview,CancellationToken ct)=>Run(async()=>{
        await using var conn=await source.OpenConnectionAsync(ct);var report=await ReadReport(conn,id,false,ct);
        byte[] bytes;string name;await using(var cmd=new NpgsqlCommand("SELECT file_name,content FROM report_submissions WHERE id=$1 AND report_id=$2 AND is_practice=$5 AND ($3 OR student_id=$4)",conn)){cmd.Parameters.AddWithValue(submission);cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(StudioWorkflow.Staff(report.CourseRole));cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(experience.IsPractice);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return NotFound();name=r.GetString(0);bytes=r.GetFieldValue<byte[]>(1);}
        if(preview && FilePreviewPolicy.ContentType(name,bytes)!="application/pdf")return StatusCode(415,new{error="此檔案無法預覽，請下載後開啟。"});
        await StudioWorkflow.Audit(conn,Actor,submission,preview?"report.previewed":"report.downloaded",ct);Response.Headers["X-Content-Type-Options"]="nosniff";return preview?File(bytes,"application/pdf"):File(bytes,"application/octet-stream",name);
    });
    [HttpPut("api/reports/{id:guid}/submissions/{submission:guid}/grade")]
    public Task<IActionResult> Grade(Guid id,Guid submission,ReportGradeInput input,CancellationToken ct)=>Run(async()=>{
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);var report=await ReadReport(conn,id,true,ct);AssignmentPolicy.RequireStaff(report.CourseRole);GradebookPolicy.ValidateScore(input.Score,report.MaxScore);
        Guid student;await using(var cmd=new NpgsqlCommand("UPDATE report_submissions SET score=$3,feedback=$4,graded_by=$5,graded_at=now(),version=version+1 WHERE id=$1 AND report_id=$2 AND version=$6 AND NOT is_practice RETURNING student_id",conn)){cmd.Parameters.AddWithValue(submission);cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(input.Score!.Value);cmd.Parameters.AddWithValue(input.Feedback);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(input.ExpectedVersion);if(await cmd.ExecuteScalarAsync(ct) is not Guid user)return Conflict(new{error="繳交或評分版本已變更，請重新載入。"});student=user;}
        await AssessmentGradeSync.SetScore(conn,report.ItemId,report.CourseId,student,input.Score!.Value,Actor,ct);await StudioWorkflow.Audit(conn,Actor,submission,"report.graded",ct);await tx.CommitAsync(ct);return NoContent();
    });
}
