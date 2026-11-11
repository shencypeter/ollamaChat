using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Serialization;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AiTeachingAssistant.Controllers;

[ApiController,Authorize,Route("api/notifications")]
public sealed class NotificationsController(NpgsqlDataSource source,LearnerExperience experience):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    // Project only content the current course role can actually list. Drafts never notify.
    private const string Eligible="""
        WITH memberships AS (
         SELECT c.id,m.course_role FROM courses c JOIN course_members m ON m.course_id=c.id
         JOIN app_users u ON u.id=m.user_id
         WHERE m.user_id=$1 AND m.status='active' AND u.status='active' AND c.status<>'archived' AND (c.status<>'draft' OR m.course_role<>'student')
        ), eligible AS (
         SELECT a.id,a.course_id,'quiz' AS kind FROM assignments a JOIN memberships m ON m.id=a.course_id
          WHERE m.course_role='student' AND a.status='published' AND a.notify_students AND EXISTS(SELECT 1 FROM course_grade_items i WHERE i.source_assignment_id=a.id)
         UNION ALL SELECT a.id,a.course_id,'report' FROM report_assignments a JOIN memberships m ON m.id=a.course_id
          WHERE m.course_role='student' AND a.status='published' AND a.notify_students AND (a.submission_mode='individual' OR EXISTS(SELECT 1 FROM report_group_audience WHERE report_id=a.id AND student_id=$1)) AND EXISTS(SELECT 1 FROM course_grade_items i WHERE i.source_report_id=a.id)
         UNION ALL SELECT r.id,r.course_id,'material' FROM course_resources r JOIN memberships m ON m.id=r.course_id WHERE m.course_role='student' AND r.notify_students
         UNION ALL SELECT q.id,q.course_id,'question' FROM course_questions q JOIN memberships m ON m.id=q.course_id
          WHERE m.course_role IN ('teacher','ta') AND q.author_id<>$1
        )
        """;
    [HttpGet]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        if(experience.Requested)return Ok(new{materials=0L,assignments=0L,questions=0L,total=0L,courses=Array.Empty<object>()});
        await using var cmd=source.CreateCommand(Eligible+"SELECT m.id,count(e.id) FILTER(WHERE e.kind IN ('quiz','report')),count(e.id) FILTER(WHERE e.kind='question'),count(e.id) FILTER(WHERE e.kind='material') FROM memberships m LEFT JOIN eligible e ON e.course_id=m.id AND NOT EXISTS(SELECT 1 FROM notification_reads r WHERE r.user_id=$1 AND r.kind=e.kind AND r.item_id=e.id) GROUP BY m.id ORDER BY m.id");
        cmd.Parameters.AddWithValue(Actor);
        var courses=new List<object>();long materials=0,assignments=0,questions=0;
        await using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct)){var a=r.GetInt64(1);var q=r.GetInt64(2);var m=r.GetInt64(3);materials+=m;assignments+=a;questions+=q;courses.Add(new{courseId=r.GetGuid(0),materials=m,assignments=a,questions=q,total=a+q+m});}
        return Ok(new{materials,assignments,questions,total=materials+assignments+questions,courses});
    }
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed class ReadInput
    {
        [Required,RegularExpression("^(quiz|report|question|material)$")] public string Kind{get;set;}="";
        [Required,MinLength(1),MaxLength(200)] public Guid[] Ids{get;set;}=[];
    }
    [HttpPost("read")]
    public async Task<IActionResult> Read(ReadInput input,CancellationToken ct)
    {
        if(experience.Requested)return Forbid();
        if(input.Kind is not ("quiz" or "report" or "question" or "material") || input.Ids.Length is <1 or >200 || input.Ids.Contains(Guid.Empty))return BadRequest();
        var ids=input.Ids.Distinct().ToArray();
        await using var db=await source.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await using(var gate=new NpgsqlCommand("SELECT m.course_id FROM course_members m JOIN courses c ON c.id=m.course_id JOIN app_users u ON u.id=m.user_id WHERE m.user_id=$1 AND m.status='active' AND u.status='active' FOR SHARE OF m,c,u",db))
        {gate.Parameters.AddWithValue(Actor);await using var reader=await gate.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct)){} }
        await using(var check=new NpgsqlCommand(Eligible+"SELECT count(*) FROM eligible WHERE kind=$2 AND id=ANY($3)",db))
        {
            check.Parameters.AddWithValue(Actor);check.Parameters.AddWithValue(input.Kind);check.Parameters.AddWithValue(ids);
            if((long)(await check.ExecuteScalarAsync(ct))! != ids.Length)return Forbid();
        }
        await using(var cmd=new NpgsqlCommand("INSERT INTO notification_reads(user_id,kind,item_id) SELECT $1,$2,unnest($3::uuid[]) ON CONFLICT DO NOTHING",db))
        {cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(input.Kind);cmd.Parameters.AddWithValue(ids);await cmd.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct);return NoContent();
    }
}
