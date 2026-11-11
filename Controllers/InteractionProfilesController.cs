using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AiTeachingAssistant.Controllers;

[ApiController,Authorize,Route("api/interaction-profile")]
public sealed class InteractionProfilesController(NpgsqlDataSource source,LearnerExperience experience):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct,[FromQuery]Guid? student=null,[FromQuery]Guid? course=null,[FromQuery]string? kind=null,[FromQuery,Range(1,100000)]int page=1)
    {
        var actor=Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var target=student??actor;
        if(experience.IsPractice)return Forbid();
        if(kind is not (null or "" or "learning" or "like" or "comment"))return BadRequest(new{error="互動類型無效。"});
        await using var db=await source.OpenConnectionAsync(ct);
        await using var tx=await db.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        if(target!=actor)
        {
            if(course==null)return Forbid();
            await using var access=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM course_members t JOIN course_members s ON s.course_id=t.course_id WHERE t.course_id=$1 AND t.user_id=$2 AND t.status='active' AND t.course_role IN ('teacher','ta') AND s.user_id=$3 AND s.status='active' AND s.course_role='student')",db);
            access.Parameters.AddWithValue(course.Value);access.Parameters.AddWithValue(actor);access.Parameters.AddWithValue(target);
            if(await access.ExecuteScalarAsync(ct) is not true)return Forbid();
        }
        string name;
        await using(var user=new NpgsqlCommand("SELECT display_name FROM app_users WHERE id=$1 AND status='active'",db)){user.Parameters.AddWithValue(target);if(await user.ExecuteScalarAsync(ct) is not string found)return NotFound();name=found;}
        const string projection="""
            WITH allowed AS (
             SELECT c.id,c.title FROM courses c JOIN course_members m ON m.course_id=c.id
             WHERE m.user_id=$1 AND m.status='active' AND c.status<>'draft' AND m.course_role='student' AND ($2::uuid IS NULL OR c.id=$2)
            ), events AS (
             SELECT resource_id,'like' AS kind,created_at AS at,0::bigint AS ms,0::bigint AS content FROM resource_likes WHERE user_id=$1
             UNION ALL SELECT resource_id,'comment',max(created_at),0::bigint,0::bigint FROM resource_comments WHERE author_id=$1 GROUP BY resource_id
             UNION ALL SELECT resource_id,'learning',max(last_seen_at),sum(credited_milliseconds)::bigint,sum(content_milliseconds)::bigint FROM learning_time_sessions WHERE user_id=$1 AND NOT is_practice AND credited_milliseconds>0 GROUP BY resource_id
            ), filtered AS (
             SELECT r.id,r.course_id,c.title AS course_title,COALESCE(NULLIF(r.display_title,''),r.file_name) AS title,e.kind,e.at,e.ms,e.content
             FROM events e JOIN course_resources r ON r.id=e.resource_id JOIN allowed c ON c.id=r.course_id WHERE ($3::text='' OR e.kind=$3)
            )
            """;
        NpgsqlCommand Query(string sql)
        {
            var cmd=new NpgsqlCommand(projection+sql,db);cmd.Parameters.AddWithValue(target);
            cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Uuid,Value=(object?)course??DBNull.Value});cmd.Parameters.AddWithValue(kind??"");return cmd;
        }
        long total;
        await using(var count=Query("SELECT count(*) FROM filtered"))total=(long)(await count.ExecuteScalarAsync(ct))!;
        var items=new List<object>();
        await using(var cmd=Query("SELECT * FROM filtered ORDER BY at DESC,id,kind LIMIT 20 OFFSET $4"))
        {
            cmd.Parameters.AddWithValue((page-1)*20);
            await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))items.Add(new{resourceId=r.GetGuid(0),courseId=r.GetGuid(1),courseTitle=r.GetString(2),title=r.GetString(3),kind=r.GetString(4),lastAt=r.GetFieldValue<DateTimeOffset>(5),seconds=r.GetInt64(6)/1000,contentSeconds=r.GetInt64(7)/1000});
        }
        var courses=new List<object>();
        await using(var cmd=new NpgsqlCommand("SELECT c.id,c.title FROM courses c JOIN course_members m ON m.course_id=c.id WHERE m.user_id=$1 AND m.status='active' AND c.status<>'draft' AND m.course_role='student' AND ($2::uuid IS NULL OR c.id=$2) ORDER BY c.title,c.id",db))
        {
            cmd.Parameters.AddWithValue(target);cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Uuid,Value=(object?)(target==actor?null:course)??DBNull.Value});
            await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))courses.Add(new{id=r.GetGuid(0),title=r.GetString(1)});
        }
        await tx.CommitAsync(ct);
        return Ok(new{name,total,page,pageSize=20,items,courses});
    }
}
