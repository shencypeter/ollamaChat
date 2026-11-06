using System.Security.Claims;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
namespace AiTeachingAssistant.Controllers;

[ApiController,Route("api/courses"),Authorize]
public sealed class CoursesController(NpgsqlDataSource source,LearnerExperience experience):ControllerBase
{
    private Guid UserId=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("""
            SELECT c.id,c.code,c.title,c.term_code,c.status,CASE WHEN $2 THEN 'student' ELSE m.course_role END,
              (SELECT count(*)::int FROM course_members WHERE course_id=c.id AND status='active'),
              (SELECT count(*)::int FROM assignments WHERE course_id=c.id AND submission_mode='individual' AND ((NOT $2 AND m.course_role IN ('teacher','ta')) OR status IN ('published','closed'))),
              (SELECT count(*)::int FROM course_resources r WHERE r.course_id=c.id),
              (SELECT count(*)::int FROM course_members x JOIN app_users u ON u.id=x.user_id WHERE x.course_id=c.id AND x.course_role='student' AND x.status='active' AND u.status='active'),
              (SELECT count(*)::int FROM assignments a JOIN course_grade_items i ON i.source_assignment_id=a.id WHERE a.course_id=c.id AND ((NOT $2 AND m.course_role IN ('teacher','ta')) OR a.status IN ('published','closed'))),
              (SELECT count(*)::int FROM report_assignments a WHERE a.course_id=c.id AND ((NOT $2 AND m.course_role IN ('teacher','ta')) OR a.status IN ('published','closed'))),
              c.owner_user_id=$1 AND NOT $2,m.course_role IN ('teacher','ta')
            FROM courses c JOIN course_members m ON m.course_id=c.id
            WHERE m.user_id=$1 AND m.status='active' ORDER BY c.created_at DESC
            """);
        cmd.Parameters.AddWithValue(UserId);cmd.Parameters.AddWithValue(experience.IsPractice);await using var r=await cmd.ExecuteReaderAsync(ct);var courses=new List<CourseSummary>();
        while(await r.ReadAsync(ct)) courses.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetInt32(6),r.GetInt32(7),r.GetInt32(8),r.GetInt32(9),r.GetInt32(10),r.GetInt32(11),r.GetBoolean(12),r.GetBoolean(13)));
        return Ok(courses);
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id,CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("""
            SELECT c.id,c.code,c.title,c.term_code,c.status,CASE WHEN $3 THEN 'student' ELSE m.course_role END,
              (SELECT count(*)::int FROM course_members WHERE course_id=c.id AND status='active'),
              (SELECT count(*)::int FROM assignments WHERE course_id=c.id AND submission_mode='individual' AND ((NOT $3 AND m.course_role IN ('teacher','ta')) OR status IN ('published','closed'))),
              (SELECT count(*)::int FROM course_resources r WHERE r.course_id=c.id),
              (SELECT count(*)::int FROM course_members x JOIN app_users u ON u.id=x.user_id WHERE x.course_id=c.id AND x.course_role='student' AND x.status='active' AND u.status='active'),
              (SELECT count(*)::int FROM assignments a JOIN course_grade_items i ON i.source_assignment_id=a.id WHERE a.course_id=c.id AND ((NOT $3 AND m.course_role IN ('teacher','ta')) OR a.status IN ('published','closed'))),
              (SELECT count(*)::int FROM report_assignments a WHERE a.course_id=c.id AND ((NOT $3 AND m.course_role IN ('teacher','ta')) OR a.status IN ('published','closed'))),
              c.owner_user_id=$2 AND NOT $3,m.course_role IN ('teacher','ta')
            FROM courses c JOIN course_members m ON m.course_id=c.id AND m.user_id=$2 AND m.status='active' WHERE c.id=$1
            """);
        cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(UserId);cmd.Parameters.AddWithValue(experience.IsPractice);await using var r=await cmd.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))return NotFound(new{error="找不到你參與的課程。"});
        return Ok(new CourseSummary(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetInt32(6),r.GetInt32(7),r.GetInt32(8),r.GetInt32(9),r.GetInt32(10),r.GetInt32(11),r.GetBoolean(12),r.GetBoolean(13)));
    }
    [HttpPost]
    public async Task<IActionResult> Create(CreateCourseRequest request,CancellationToken ct)
    {
        if(experience.IsPractice)return Forbid();
        await using(var permission=source.CreateCommand("SELECT can_create_courses FROM portal_credentials WHERE user_id=$1")){permission.Parameters.AddWithValue(UserId);if(await permission.ExecuteScalarAsync(ct) is not true)return Forbid();}
        if(new[]{request.Code,request.Title,request.TermCode}.Any(string.IsNullOrWhiteSpace))return BadRequest(new{error="請填寫課程代碼、名稱與學期。"});
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);var id=Guid.NewGuid();
        try
        {
            await using(var cmd=new NpgsqlCommand("INSERT INTO courses(id,code,title,term_code,owner_user_id) VALUES($1,$2,$3,$4,$5)",conn,tx))
            {cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(request.Code.Trim());cmd.Parameters.AddWithValue(request.Title.Trim());cmd.Parameters.AddWithValue(request.TermCode.Trim());cmd.Parameters.AddWithValue(UserId);await cmd.ExecuteNonQueryAsync(ct);}
            await using(var cmd=new NpgsqlCommand("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,'teacher')",conn,tx))
            {cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(UserId);await cmd.ExecuteNonQueryAsync(ct);}
            await using(var audit=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action) VALUES($1,'courses',$2,'course.created')",conn,tx))
            {audit.Parameters.AddWithValue(UserId);audit.Parameters.AddWithValue(id);await audit.ExecuteNonQueryAsync(ct);}
            await tx.CommitAsync(ct);return Ok(new CourseSummary(id,request.Code.Trim(),request.Title.Trim(),request.TermCode.Trim(),"draft","teacher",1,0,IsOwner:true,CanPractice:true));
        }
        catch(PostgresException e)when(e.SqlState==PostgresErrorCodes.UniqueViolation){return Conflict(new{error="此學期已有相同課程代碼。"});}
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> Members(Guid id,CancellationToken ct)
    {
        await using var access=source.CreateCommand("SELECT EXISTS(SELECT 1 FROM course_members WHERE course_id=$1 AND user_id=$2 AND course_role IN ('teacher','ta') AND status='active')");
        access.Parameters.AddWithValue(id);access.Parameters.AddWithValue(UserId);
        if(await access.ExecuteScalarAsync(ct) is not true)return Forbid();
        await using var cmd=source.CreateCommand("SELECT u.id,u.display_name,u.email,m.course_role FROM course_members m JOIN app_users u ON u.id=m.user_id WHERE m.course_id=$1 AND m.status='active' ORDER BY u.display_name");
        cmd.Parameters.AddWithValue(id);await using var r=await cmd.ExecuteReaderAsync(ct);var members=new List<object>();
        while(await r.ReadAsync(ct))members.Add(new{id=r.GetGuid(0),displayName=r.GetString(1),email=r.IsDBNull(2)?null:r.GetString(2),courseRole=r.GetString(3)});
        return Ok(members);
    }
    [HttpGet("{id:guid}/eligible-accounts")]
    public async Task<IActionResult> EligibleAccounts(Guid id,CancellationToken ct){
        if(experience.IsPractice)return Forbid();
        await using var check=source.CreateCommand("SELECT EXISTS(SELECT 1 FROM courses c JOIN course_members m ON m.course_id=c.id WHERE c.id=$1 AND c.owner_user_id=$2 AND m.user_id=$2 AND m.course_role='teacher' AND m.status='active')");check.Parameters.AddWithValue(id);check.Parameters.AddWithValue(UserId);if(await check.ExecuteScalarAsync(ct) is not true)return Forbid();
        await using var cmd=source.CreateCommand("SELECT u.id,u.display_name,u.email FROM app_users u JOIN portal_credentials c ON c.user_id=u.id WHERE u.status='active' ORDER BY u.display_name");await using var r=await cmd.ExecuteReaderAsync(ct);var rows=new List<object>();while(await r.ReadAsync(ct))rows.Add(new{id=r.GetGuid(0),displayName=r.GetString(1),email=r.GetString(2)});return Ok(rows);
    }
    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> Enroll(Guid id,EnrollRequest request,CancellationToken ct)
    {
        if(request.CourseRole is not ("teacher" or "ta" or "student") || request.UserId==Guid.Empty)return BadRequest(new{error="請選擇有效的成員與課程角色。"});
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        await using var owner=new NpgsqlCommand("SELECT c.owner_user_id FROM courses c JOIN course_members m ON m.course_id=c.id AND m.user_id=c.owner_user_id AND m.status='active' AND m.course_role='teacher' WHERE c.id=$1 AND c.status<>'archived' FOR UPDATE OF c",conn,tx);owner.Parameters.AddWithValue(id);
        if(await owner.ExecuteScalarAsync(ct) is not Guid ownerId || ownerId!=UserId)return Forbid();
        if(request.UserId==ownerId && request.CourseRole!="teacher")return BadRequest(new{error="課程擁有者必須保留授課教師角色。"});
        await using var target=new NpgsqlCommand("SELECT u.status FROM portal_credentials c JOIN app_users u ON u.id=c.user_id WHERE c.user_id=$1 AND u.status='active'",conn,tx);target.Parameters.AddWithValue(request.UserId);
        var role=await target.ExecuteScalarAsync(ct) as string;
        if(role is null)return BadRequest(new{error="成員不存在或已停用。"});
        if(request.CourseRole!="student") {
            await using var close=new NpgsqlCommand("UPDATE course_group_members gm SET left_at=now() FROM course_groups g WHERE gm.group_id=g.id AND g.course_id=$1 AND gm.user_id=$2 AND gm.left_at IS NULL",conn,tx);
            close.Parameters.AddWithValue(id);close.Parameters.AddWithValue(request.UserId);await close.ExecuteNonQueryAsync(ct);
        }
        await using var cmd=new NpgsqlCommand("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,$3) ON CONFLICT(course_id,user_id) DO UPDATE SET course_role=EXCLUDED.course_role,status='active'",conn,tx);
        cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(request.UserId);cmd.Parameters.AddWithValue(request.CourseRole);await cmd.ExecuteNonQueryAsync(ct);
        await using(var audit=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata) VALUES($1,'courses',$2,'course.member.updated',jsonb_build_object('userId',$3::text,'courseRole',$4::text))",conn,tx))
        {audit.Parameters.AddWithValue(UserId);audit.Parameters.AddWithValue(id);audit.Parameters.AddWithValue(request.UserId.ToString());audit.Parameters.AddWithValue(request.CourseRole);await audit.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct);return NoContent();
    }
    public sealed class EnrollRequest {public Guid UserId {get;set;} public string CourseRole {get;set;}="student";}
}
