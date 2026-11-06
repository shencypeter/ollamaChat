using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
namespace AiTeachingAssistant.Controllers;

[ApiController,Authorize,Route("api/courses/{course:guid}/exams")]
public sealed class ExamsController(NpgsqlDataSource source,LearnerExperience experience,TimeProvider clock):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public sealed class ExamInput {
        [Required,StringLength(200)]public string Title{get;set;}="";
        public string? ExpectedEditVersion{get;set;}
        [StringLength(24000)]public string Instructions{get;set;}="";
        public DateTimeOffset? OpensAt{get;set;} public DateTimeOffset? DueAt{get;set;}
        [Required,MinLength(1),MaxLength(20)]public List<AssignmentQuestionInput> Questions{get;set;}=[];
    }
    public sealed class AttemptInput { public int ExpectedVersion{get;set;} public bool Submit{get;set;} public Dictionary<string,string> Answers{get;set;}=[]; }
    public sealed class GradeInput { [Range(0,100)]public decimal Score{get;set;} [StringLength(16000)]public string Feedback{get;set;}="";public int ExpectedVersion{get;set;} }
    private async Task<(string Role,string Status)> Access(Guid course,CancellationToken ct) {
        await using var cmd=source.CreateCommand("SELECT m.course_role,c.status FROM course_members m JOIN courses c ON c.id=m.course_id WHERE m.course_id=$1 AND m.user_id=$2 AND m.status='active'");cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(Actor);
        await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new AssignmentWorkflowException(403,"你不是本課程有效成員。");return(experience.IsPractice?"student":r.GetString(0),r.GetString(1));
    }
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action) {try{return await action();}catch(AssignmentWorkflowException e){return StatusCode(e.StatusCode,new{error=e.Message});}}
    [HttpGet]public Task<IActionResult> List(Guid course,CancellationToken ct)=>Run(async()=>{
        var access=await Access(course,ct);await using var cmd=source.CreateCommand("SELECT id,title,status,opens_at,due_at FROM exams WHERE course_id=$1 AND ($2 OR status<>'draft') ORDER BY created_at DESC");cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(AssignmentPolicy.IsStaff(access.Role));await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<object>();while(await r.ReadAsync(ct))result.Add(new{id=r.GetGuid(0),title=r.GetString(1),status=r.GetString(2),opensAt=r.IsDBNull(3)?(DateTimeOffset?)null:r.GetFieldValue<DateTimeOffset>(3),dueAt=r.IsDBNull(4)?(DateTimeOffset?)null:r.GetFieldValue<DateTimeOffset>(4)});return Ok(result);
    });
    [HttpPost, HttpPut("{id:guid}")]public Task<IActionResult> Create(Guid course,ExamInput input,CancellationToken ct,Guid? id=null)=>Run(async()=>{
        var access=await Access(course,ct);if(!AssignmentPolicy.IsStaff(access.Role)||access.Status=="archived")return Forbid();
        if(string.IsNullOrWhiteSpace(input.Title)||input.Questions.Any(q=>q==null||string.IsNullOrWhiteSpace(q.PromptText)) || (input.OpensAt.HasValue&&input.DueAt<=input.OpensAt))return BadRequest(new{error="請確認考試名稱、題目與時間。"});
        foreach(var q in input.Questions)QuestionPolicy.Validate(q);
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        if(id.HasValue){var old=await Load(conn,course,id.Value,true,ct);if(old.Status!="draft")return Conflict(new{error="只能修改考試草稿。"});await using var version=new NpgsqlCommand("SELECT md5(title || instructions || questions::text || coalesce(opens_at::text,'') || coalesce(due_at::text,'')) FROM exams WHERE id=$1",conn);version.Parameters.AddWithValue(id.Value);if((string?)await version.ExecuteScalarAsync(ct)!=input.ExpectedEditVersion)return Conflict(new{error="考試已被修改，請重新載入。"});}
        await using var cmd=new NpgsqlCommand(id.HasValue?"UPDATE exams SET title=$2,instructions=$3,opens_at=$4,due_at=$5,questions=$6::jsonb WHERE id=$1 RETURNING id":"INSERT INTO exams(course_id,title,instructions,opens_at,due_at,questions) VALUES($1,$2,$3,$4,$5,$6::jsonb) RETURNING id",conn);cmd.Parameters.AddWithValue(id??course);cmd.Parameters.AddWithValue(input.Title.Trim());cmd.Parameters.AddWithValue(input.Instructions);cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.TimestampTz,Value=(object?)input.OpensAt?.UtcDateTime??DBNull.Value});cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.TimestampTz,Value=(object?)input.DueAt?.UtcDateTime??DBNull.Value});cmd.Parameters.AddWithValue(JsonSerializer.Serialize(input.Questions,Json));var result=await cmd.ExecuteScalarAsync(ct);await tx.CommitAsync(ct);return Ok(new{id=result});
    });
    private async Task<(string Status,DateTimeOffset? Open,DateTimeOffset? Due,List<AssignmentQuestionInput> Questions,string Title,string Instructions)> Load(NpgsqlConnection conn,Guid course,Guid id,bool staff,CancellationToken ct) {
        await using var cmd=new NpgsqlCommand("SELECT status,opens_at,due_at,questions::text,title,instructions FROM exams WHERE id=$1 AND course_id=$2 FOR UPDATE",conn);cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct)||(!staff&&r.GetString(0)=="draft"))throw new AssignmentWorkflowException(404,"找不到考試。");return(r.GetString(0),r.IsDBNull(1)?null:r.GetFieldValue<DateTimeOffset>(1),r.IsDBNull(2)?null:r.GetFieldValue<DateTimeOffset>(2),JsonSerializer.Deserialize<List<AssignmentQuestionInput>>(r.GetString(3),Json)!,r.GetString(4),r.GetString(5));
    }
    [HttpGet("{id:guid}")]public Task<IActionResult> Get(Guid course,Guid id,CancellationToken ct)=>Run(async()=>{
        var access=await Access(course,ct);var staff=AssignmentPolicy.IsStaff(access.Role);await using var conn=await source.OpenConnectionAsync(ct);var exam=await Load(conn,course,id,staff,ct);
        var visible=staff||!exam.Open.HasValue||clock.GetUtcNow()>=exam.Open;
        await using var version=source.CreateCommand("SELECT md5(title || instructions || questions::text || coalesce(opens_at::text,'') || coalesce(due_at::text,'')) FROM exams WHERE id=$1");version.Parameters.AddWithValue(id);var editVersion=staff?(string?)await version.ExecuteScalarAsync(ct):null;
        return Ok(new{id,editVersion,title=exam.Title,instructions=exam.Instructions,status=exam.Status,opensAt=exam.Open,dueAt=exam.Due,staff,isPractice=experience.IsPractice,questions=visible?exam.Questions.Select((q,i)=>new{id=i.ToString(),q.PromptText,q.StageCode,q.QuestionType,options=q.QuestionType=="true_false"?new List<string>{"是","非"}:q.Options,standardAnswer=staff?q.StandardAnswer:null}).ToArray():[]});
    });
    [HttpPost("{id:guid}/{actionName}")]public Task<IActionResult> Transition(Guid course,Guid id,string actionName,CancellationToken ct)=>Run(async()=>{
        var access=await Access(course,ct);if(!AssignmentPolicy.IsStaff(access.Role)||access.Status=="archived")return Forbid();
        if(actionName is not ("publish" or "close"))return NotFound();await using var cmd=source.CreateCommand("UPDATE exams SET status=$3 WHERE id=$1 AND course_id=$2 AND status=$4");cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(actionName=="publish"?"published":"closed");cmd.Parameters.AddWithValue(actionName=="publish"?"draft":"published");return await cmd.ExecuteNonQueryAsync(ct)==1?NoContent():Conflict(new{error="考試狀態已變更。"});
    });
    [HttpGet("{id:guid}/attempt")]
    public Task<IActionResult> Own(Guid course,Guid id,CancellationToken ct)=>Run(async()=>{
        var access=await Access(course,ct);if(access.Role!="student"||experience.IsPractice)return Forbid();await using var conn=await source.OpenConnectionAsync(ct);await Load(conn,course,id,false,ct);
        await using var cmd=new NpgsqlCommand("SELECT answers::text,submitted_at,score,feedback,version FROM exam_attempts WHERE exam_id=$1 AND student_id=$2",conn);cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(Actor);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return Ok(new{answers=new Dictionary<string,string>(),submittedAt=(DateTimeOffset?)null,score=(decimal?)null,feedback="",version=0});return Ok(new{answers=JsonSerializer.Deserialize<Dictionary<string,string>>(r.GetString(0)),submittedAt=r.IsDBNull(1)?(DateTimeOffset?)null:r.GetFieldValue<DateTimeOffset>(1),score=r.IsDBNull(2)?(decimal?)null:r.GetDecimal(2),feedback=r.GetString(3),version=r.GetInt32(4)});
    });
    [HttpPut("{id:guid}/attempt")]
    public Task<IActionResult> Save(Guid course,Guid id,AttemptInput input,CancellationToken ct)=>Run(async()=>{
        var access=await Access(course,ct);if(access.Role!="student"||experience.IsPractice)return Forbid();await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);var exam=await Load(conn,course,id,false,ct);var now=clock.GetUtcNow();
        if(access.Status=="archived"||exam.Status!="published"||(exam.Open.HasValue&&now<exam.Open)||(exam.Due.HasValue&&now>=exam.Due))return Conflict(new{error="目前不在考試作答期間。"});
        if(input.Answers==null||input.Answers.Count>exam.Questions.Count||input.Answers.Keys.Any(k=>!int.TryParse(k,out var n)||n<0||n>=exam.Questions.Count||k!=n.ToString())||input.Answers.Values.Any(v=>v==null||v.Length>16000))return BadRequest(new{error="答案格式無效。"});
        for(var i=0;i<exam.Questions.Count;i++){var q=exam.Questions[i];var text=input.Answers.GetValueOrDefault(i.ToString(),"");if(input.Submit&&string.IsNullOrWhiteSpace(text))return BadRequest(new{error="請完成全部題目。"});QuestionPolicy.ValidateAnswer(new(Guid.Empty,q.PromptText,null,i,q.QuestionType,q.QuestionType=="true_false"?["是","非"]:q.Options),text,input.Submit);}
        await using(var ensure=new NpgsqlCommand("INSERT INTO exam_attempts(exam_id,student_id) VALUES($1,$2) ON CONFLICT DO NOTHING",conn)){ensure.Parameters.AddWithValue(id);ensure.Parameters.AddWithValue(Actor);await ensure.ExecuteNonQueryAsync(ct);}
        await using var cmd=new NpgsqlCommand("UPDATE exam_attempts SET answers=$3::jsonb,version=version+1,submitted_at=CASE WHEN $4 THEN now() ELSE NULL END WHERE exam_id=$1 AND student_id=$2 AND version=$5 AND submitted_at IS NULL",conn);cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(JsonSerializer.Serialize(input.Answers));cmd.Parameters.AddWithValue(input.Submit);cmd.Parameters.AddWithValue(input.ExpectedVersion);if(await cmd.ExecuteNonQueryAsync(ct)==0)return Conflict(new{error="已提交或版本過期，請重新載入。"});await tx.CommitAsync(ct);return NoContent();
    });
    [HttpGet("{id:guid}/results")]
    public Task<IActionResult> Results(Guid course,Guid id,string sort="student",CancellationToken ct=default)=>Run(async()=>{
        var access=await Access(course,ct);if(!AssignmentPolicy.IsStaff(access.Role))return Forbid();await using var conn=await source.OpenConnectionAsync(ct);await Load(conn,course,id,true,ct);
        await using var cmd=new NpgsqlCommand("SELECT a.id,u.display_name,u.student_number,a.answers::text,a.submitted_at,a.score,a.feedback,a.version,CASE WHEN a.score IS NOT NULL THEN dense_rank() OVER(ORDER BY a.score DESC NULLS LAST) END FROM exam_attempts a JOIN app_users u ON u.id=a.student_id WHERE a.exam_id=$1 AND a.submitted_at IS NOT NULL ORDER BY "+(sort=="score"?"a.score DESC NULLS LAST,u.student_number NULLS LAST,u.display_name,a.id":"u.student_number NULLS LAST,u.display_name,a.id"),conn);cmd.Parameters.AddWithValue(id);await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<object>();while(await r.ReadAsync(ct))result.Add(new{id=r.GetGuid(0),studentName=r.GetString(1),studentNumber=r.IsDBNull(2)?null:r.GetString(2),answers=JsonSerializer.Deserialize<Dictionary<string,string>>(r.GetString(3)),submittedAt=r.GetFieldValue<DateTimeOffset>(4),score=r.IsDBNull(5)?(decimal?)null:r.GetDecimal(5),feedback=r.GetString(6),version=r.GetInt32(7),rank=r.IsDBNull(8)?(long?)null:r.GetInt64(8)});return Ok(result);
    });
    [HttpPut("{id:guid}/results/{attempt:guid}")]
    public Task<IActionResult> Grade(Guid course,Guid id,Guid attempt,GradeInput input,CancellationToken ct)=>Run(async()=>{
        var access=await Access(course,ct);if(!AssignmentPolicy.IsStaff(access.Role)||access.Status=="archived")return Forbid();if(Math.Round(input.Score,2)!=input.Score)return BadRequest(new{error="成績最多兩位小數。"});await using var conn=await source.OpenConnectionAsync(ct);await Load(conn,course,id,true,ct);
        await using var cmd=new NpgsqlCommand("UPDATE exam_attempts SET score=$3,feedback=$4,graded_by=$5,version=version+1 WHERE id=$1 AND exam_id=$2 AND submitted_at IS NOT NULL AND version=$6",conn);cmd.Parameters.AddWithValue(attempt);cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(input.Score);cmd.Parameters.AddWithValue(input.Feedback);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(input.ExpectedVersion);return await cmd.ExecuteNonQueryAsync(ct)==1?NoContent():Conflict(new{error="評分版本已變更，請重新載入。"});
    });
}
