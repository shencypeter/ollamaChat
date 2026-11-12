using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using AiTeachingAssistant.Services;
namespace AiTeachingAssistant.Controllers;

[ApiController,Authorize,Route("api/courses/{course:guid}")]
public sealed partial class CourseToolsController(NpgsqlDataSource source,LearnerExperience experience):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<string?> Access(Guid course,bool staff,CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("SELECT m.course_role FROM course_members m JOIN courses c ON c.id=m.course_id WHERE m.course_id=$1 AND m.user_id=$2 AND m.status='active' AND (NOT $3 OR c.status<>'archived')");
        cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(staff);
        var role=await cmd.ExecuteScalarAsync(ct) as string;
        if(experience.IsPractice && role!=null)role="student";
        return staff && role is not ("teacher" or "ta")?null:role;
    }
    [HttpGet("classmates")]
    public async Task<IActionResult> Classmates(Guid course,CancellationToken ct)
    {
        if(!StudioWorkflow.Staff(await Access(course,false,ct)??""))return Forbid();
        await using var cmd=source.CreateCommand("SELECT u.id,u.display_name,u.student_number,m.course_id FROM course_members m JOIN app_users u ON u.id=m.user_id WHERE m.course_id=$1 AND m.status='active' AND m.course_role='student' AND u.status='active' ORDER BY u.student_number NULLS LAST,u.display_name");cmd.Parameters.AddWithValue(course);
        await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<object>();
        while(await r.ReadAsync(ct))result.Add(new{id=r.GetGuid(0),displayName=r.GetString(1),studentNumber=r.IsDBNull(2)?null:r.GetString(2)});
        return Ok(result);
    }
    [HttpGet("resources")]
    public async Task<IActionResult> Resources(Guid course,CancellationToken ct,[FromQuery,StringLength(200)] string? q=null,[FromQuery] string? teachingFrom=null,[FromQuery] string? teachingTo=null,[FromQuery] bool undated=false,[FromQuery] string? sort=null)
    {
        var role=await Access(course,false,ct);if(role==null)return Forbid();
        DateOnly? from,to;string order;
        try{from=ResourceMetadataPolicy.Date(teachingFrom);to=ResourceMetadataPolicy.Date(teachingTo);order=ResourceMetadataPolicy.Order(sort);if(from>to || undated&&(from!=null||to!=null))return BadRequest(new{error="請提供有效日期區間，未指定日期不可與區間並用。"});}
        catch(ArgumentException e){return BadRequest(new{error=e.Message});}
        await using var cmd=source.CreateCommand("SELECT id,file_name,octet_length(content),download_count,created_at,substring(content from 1 for 16),display_title,title_version,resource_type,youtube_video_id,teaching_date,summary,metadata_version,notify_students,unit_id FROM course_resources WHERE course_id=$1 AND ($2::text IS NULL OR strpos(lower(display_title||' '||file_name||' '||summary),lower($2))>0) AND ($3::date IS NULL OR teaching_date >= $3) AND ($4::date IS NULL OR teaching_date <= $4) AND (NOT $5 OR teaching_date IS NULL) ORDER BY "+order);cmd.Parameters.AddWithValue(course);
        cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Text,Value=string.IsNullOrWhiteSpace(q)?DBNull.Value:q.Trim()});
        cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Date,Value=(object?)from??DBNull.Value});cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Date,Value=(object?)to??DBNull.Value});cmd.Parameters.AddWithValue(undated);
        await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<object>();
        while(await r.ReadAsync(ct))result.Add(new{id=r.GetGuid(0),fileName=r.GetString(1),size=r.GetInt32(2),downloadCount=r.GetInt64(3),createdAt=r.GetFieldValue<DateTimeOffset>(4),previewType=FilePreviewPolicy.ContentType(r.GetString(1),r.GetFieldValue<byte[]>(5)),learningType=LearningTimePolicy.MediaType(r.GetString(1),r.GetFieldValue<byte[]>(5),r.GetString(8)),title=r.GetString(6),titleVersion=r.GetInt32(7),resourceType=r.GetString(8),youtubeVideoId=r.IsDBNull(9)?null:r.GetString(9),teachingDate=r.IsDBNull(10)?(DateOnly?)null:r.GetFieldValue<DateOnly>(10),summary=r.GetString(11),metadataVersion=r.GetInt32(12),notifyStudents=r.GetBoolean(13),unitId=r.IsDBNull(14)?(Guid?)null:r.GetGuid(14)});
        return Ok(result);
    }
    [AiTeachingAssistant.Services.CourseCapability("materials")]
    [HttpPost("resources"),RequestSizeLimit(22*1024*1024)]
    public async Task<IActionResult> Upload(Guid course,IFormFile file,[FromForm,StringLength(200)] string? title,CancellationToken ct,[FromForm] string? teachingDate=null,[FromForm,StringLength(10000)] string? summary=null,[FromForm] bool notifyStudents=false,[FromForm] Guid unitId=default)
    {
        if(await Access(course,true,ct)==null)return Forbid();
        DateOnly? date;try{date=ResourceMetadataPolicy.Date(teachingDate);summary=ResourceMetadataPolicy.Summary(summary);}catch(ArgumentException e){return BadRequest(new{error=e.Message});}
        if(file.Length is <=0 or >20*1024*1024)return BadRequest(new{error="請上傳 1 byte 至 20 MB 的檔案。"});
        var name=Path.GetFileName(file.FileName.Replace('\\','/'));if(string.IsNullOrWhiteSpace(name)||name.Length>200)return BadRequest(new{error="檔名無效或超過 200 字元。"});

        using var buffer=new MemoryStream();await file.CopyToAsync(buffer,ct);
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        await using(var gate=new NpgsqlCommand("SELECT status FROM courses WHERE id=$1 AND EXISTS(SELECT 1 FROM course_units WHERE id=$2 AND course_id=$1) FOR SHARE",conn)){gate.Parameters.AddWithValue(course);gate.Parameters.AddWithValue(unitId);if(await gate.ExecuteScalarAsync(ct) is not string status)return BadRequest(new{error="請選擇本課程的單元。"});if(status=="archived")return Forbid();}
        var unitOrder=await CourseUnitActivityOrder.NextAsync(conn,course,unitId,ct);
        await using var cmd=new NpgsqlCommand("INSERT INTO course_resources(course_id,file_name,content,uploaded_by,display_title,teaching_date,summary,notify_students,unit_id,unit_sort_order) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10) RETURNING id",conn);
        cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(name);cmd.Parameters.AddWithValue(buffer.ToArray());cmd.Parameters.AddWithValue(Actor);
        cmd.Parameters.AddWithValue(title?.Trim()??"");cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Date,Value=(object?)date??DBNull.Value});cmd.Parameters.AddWithValue(summary);cmd.Parameters.AddWithValue(notifyStudents);cmd.Parameters.AddWithValue(unitId);cmd.Parameters.AddWithValue(unitOrder);
        var id=(Guid)(await cmd.ExecuteScalarAsync(ct))!;
        await using(var audit=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action) VALUES($1,'course_resources',$2,'resource.file_uploaded')",conn)){audit.Parameters.AddWithValue(Actor);audit.Parameters.AddWithValue(id);await audit.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct);return Ok(new{id});
    }
    public sealed class ResourceTitleInput {
        [Required(AllowEmptyStrings=true),StringLength(200)] public string Title {get;set;}="";
        [Range(1,int.MaxValue)] public int ExpectedVersion {get;set;}
    }
    [AiTeachingAssistant.Services.CourseCapability("materials")]
    [HttpPut("resources/{id:guid}/title")]
    public async Task<IActionResult> ResourceTitle(Guid course,Guid id,ResourceTitleInput input,CancellationToken ct)
    {
        if(await Access(course,true,ct)==null)return Forbid();
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        await using(var gate=new NpgsqlCommand("SELECT status FROM courses WHERE id=$1 FOR SHARE",conn)){gate.Parameters.AddWithValue(course);if(await gate.ExecuteScalarAsync(ct) is not string status || status=="archived")return Forbid();}
        int version;
        await using(var update=new NpgsqlCommand("UPDATE course_resources SET display_title=$3,title_version=title_version+1 WHERE course_id=$1 AND id=$2 AND title_version=$4 RETURNING title_version",conn)){
            update.Parameters.AddWithValue(course);update.Parameters.AddWithValue(id);update.Parameters.AddWithValue(input.Title.Trim());update.Parameters.AddWithValue(input.ExpectedVersion);
            if(await update.ExecuteScalarAsync(ct) is not int value){await using var check=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM course_resources WHERE course_id=$1 AND id=$2)",conn);check.Parameters.AddWithValue(course);check.Parameters.AddWithValue(id);return await check.ExecuteScalarAsync(ct) is true?Conflict(new{error="文件名稱已被其他操作更新，請取消編輯後重新開啟。"}):NotFound();}version=value;
        }
        await using(var audit=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata) VALUES($1,'course_resources',$2,'resource.title_updated',jsonb_build_object('version',$3::int))",conn)){audit.Parameters.AddWithValue(Actor);audit.Parameters.AddWithValue(id);audit.Parameters.AddWithValue(version);await audit.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct);return Ok(new{titleVersion=version});
    }
    [HttpPost("resources/{id:guid}/download")]
    public async Task<IActionResult> Download(Guid course,Guid id,CancellationToken ct)
    {
        var role=await Access(course,false,ct);if(role==null)return Forbid();
        // One authorized download request counts once; range/retry traffic is not counted separately.
        await using var cmd=source.CreateCommand("UPDATE course_resources SET download_count=download_count+1 WHERE id=$1 AND course_id=$2 AND resource_type='file' RETURNING file_name,content");cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);
        await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return NotFound();
        Response.Headers["X-Content-Type-Options"]="nosniff";return File(r.GetFieldValue<byte[]>(1),"application/octet-stream",r.GetString(0));
    }
    [HttpGet("resources/{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid course,Guid id,CancellationToken ct)
    {
        var role=await Access(course,false,ct);if(role==null)return Forbid();
        await using var cmd=source.CreateCommand("SELECT file_name,content FROM course_resources WHERE id=$1 AND course_id=$2 AND resource_type='file'");
        cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);
        await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return NotFound();
        var bytes=r.GetFieldValue<byte[]>(1);var type=FilePreviewPolicy.ContentType(r.GetString(0),bytes);
        if(type==null)return StatusCode(415,new{error="此檔案無法預覽，請下載後開啟。"});
        Response.Headers["X-Content-Type-Options"]="nosniff";return File(bytes,type);
    }
    public sealed class GroupInput {public Guid? LeaderId{get;set;}  [Required,StringLength(100)]public string Name{get;set;}=""; public List<Guid> Members{get;set;}=[]; }
    [HttpGet("groups")]
    public async Task<IActionResult> Groups(Guid course,CancellationToken ct)
    {
        var role=await Access(course,false,ct);if(role==null)return Forbid();
        await using var cmd=source.CreateCommand("SELECT g.id,g.name,u.id,u.display_name,gm.is_leader FROM course_groups g LEFT JOIN course_group_members gm ON gm.group_id=g.id AND gm.left_at IS NULL LEFT JOIN app_users u ON u.id=gm.user_id AND EXISTS(SELECT 1 FROM course_members m WHERE m.course_id=g.course_id AND m.user_id=u.id AND m.status='active' AND m.course_role='student') WHERE g.course_id=$1 AND g.dissolved_at IS NULL AND ($2 OR EXISTS(SELECT 1 FROM course_group_members own WHERE own.group_id=g.id AND own.user_id=$3 AND own.left_at IS NULL)) ORDER BY g.name,u.display_name");cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(StudioWorkflow.Staff(role));cmd.Parameters.AddWithValue(Actor);
        await using var r=await cmd.ExecuteReaderAsync(ct);var groups=new Dictionary<Guid,(string Name,List<object> Members)>();
        while(await r.ReadAsync(ct)){var id=r.GetGuid(0);if(!groups.ContainsKey(id))groups[id]=(r.GetString(1),[]);if(!r.IsDBNull(2))groups[id].Members.Add(new{id=r.GetGuid(2),displayName=r.GetString(3),isLeader=r.GetBoolean(4)});}
        return Ok(groups.Select(g=>new{id=g.Key,name=g.Value.Name,members=g.Value.Members}));
    }
    [CourseCapability("assessments")]
    [HttpPost("groups")]
    [HttpPut("groups/{id:guid}")]
    public async Task<IActionResult> SaveGroup(Guid course,GroupInput request,CancellationToken ct,Guid? id=null)
    {
        if(await Access(course,true,ct)==null)return Forbid();
        if(!request.LeaderId.HasValue || request.Members?.Contains(request.LeaderId.Value)!=true)return BadRequest(new{error="組長必須是本組成員。"});
        if(string.IsNullOrWhiteSpace(request.Name)||request.Members==null||request.Members.Count>500||request.Members.Distinct().Count()!=request.Members.Count)return BadRequest(new{error="小組名稱或成員無效。"});
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        await using(var gate=new NpgsqlCommand("SELECT id FROM courses WHERE id=$1 FOR UPDATE",conn)){gate.Parameters.AddWithValue(course);await gate.ExecuteScalarAsync(ct);}

        foreach(var user in request.Members){await using var check=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM course_members WHERE course_id=$1 AND user_id=$2 AND course_role='student' AND status='active' AND EXISTS(SELECT 1 FROM app_users u WHERE u.id=$2 AND u.status='active') AND NOT EXISTS(SELECT 1 FROM course_group_members gm JOIN course_groups g ON g.id=gm.group_id WHERE g.course_id=$1 AND gm.user_id=$2 AND gm.left_at IS NULL AND g.id<>$3))",conn);check.Parameters.AddWithValue(course);check.Parameters.AddWithValue(user);check.Parameters.AddWithValue(id??Guid.Empty);if(await check.ExecuteScalarAsync(ct) is not true)return BadRequest(new{error="只能加入本課有效學生，且每人只能屬於一個小組。"});}
        var group=id??Guid.NewGuid();
        try{
            await using(var cmd=new NpgsqlCommand(id.HasValue?"UPDATE course_groups SET name=$3 WHERE id=$1 AND course_id=$2 AND dissolved_at IS NULL":"INSERT INTO course_groups(id,course_id,name) VALUES($1,$2,$3)",conn)) {cmd.Parameters.AddWithValue(group);cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(request.Name.Trim());if(await cmd.ExecuteNonQueryAsync(ct)==0)return NotFound();}
            await using(var close=new NpgsqlCommand("UPDATE course_group_members SET left_at=now() WHERE group_id=$1 AND left_at IS NULL",conn)){close.Parameters.AddWithValue(group);await close.ExecuteNonQueryAsync(ct);}
            foreach(var user in request.Members){await using var add=new NpgsqlCommand("INSERT INTO course_group_members(group_id,user_id,is_leader) VALUES($1,$2,$3)",conn);add.Parameters.AddWithValue(group);add.Parameters.AddWithValue(user);add.Parameters.AddWithValue(user==request.LeaderId);await add.ExecuteNonQueryAsync(ct);}
            await StudioWorkflow.Audit(conn,Actor,group,"group.saved",ct);await tx.CommitAsync(ct);return Ok(new{id=group});
        }catch(PostgresException e)when(e.SqlState==PostgresErrorCodes.UniqueViolation){return Conflict(new{error="本課程已有此小組名稱，請重新載入。"});}
    }
    [CourseCapability("assessments"),HttpDelete("groups/{id:guid}")]
    public async Task<IActionResult> Dissolve(Guid course,Guid id,CancellationToken ct)
    {
        if(await Access(course,true,ct)==null)return Forbid();
        await using var conn=await source.OpenConnectionAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(ct);
        await using var cmd=new NpgsqlCommand("SELECT id FROM courses WHERE id=$1 FOR UPDATE",conn);
        cmd.Parameters.AddWithValue(course);
        await cmd.ExecuteScalarAsync(ct);
        cmd.CommandText="UPDATE course_groups SET dissolved_at=now() WHERE course_id=$1 AND id=$2 AND dissolved_at IS NULL";
        cmd.Parameters.AddWithValue(id);
        if(await cmd.ExecuteNonQueryAsync(ct)==0)return NotFound();
        cmd.Parameters.Clear();cmd.Parameters.AddWithValue(id);
        cmd.CommandText="UPDATE course_group_members SET left_at=now() WHERE group_id=$1 AND left_at IS NULL";
        await cmd.ExecuteNonQueryAsync(ct);
        await StudioWorkflow.Audit(conn,Actor,id,"group.dissolved",ct);
        await tx.CommitAsync(ct);return NoContent();
    }
}
