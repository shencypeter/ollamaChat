using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AiTeachingAssistant.Controllers;

[ApiController, Authorize, Route("api/courses/{course:guid}/announcements")]
public sealed class CourseAnnouncementsController(NpgsqlDataSource source, LearnerExperience experience) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public sealed class AnnouncementInput
    {
        [Required, StringLength(200)] public string Title { get; set; } = "";
        [Required, StringLength(16000)] public string Body { get; set; } = "";
        [Required] public string Status { get; set; } = "draft";
        public int ExpectedVersion { get; set; }
    }
    private static NpgsqlCommand Command(NpgsqlConnection db, string sql, params object[] args)
    {
        var cmd = new NpgsqlCommand(sql, db);
        foreach (var arg in args) cmd.Parameters.AddWithValue(arg);
        return cmd;
    }
    private async Task<(bool Staff, bool Manage)?> Access(NpgsqlConnection db, Guid course, CancellationToken ct)
    {
        await using var cmd = Command(db, """
            SELECT m.course_role,c.status,m.ta_restrictions FROM course_members m
            JOIN courses c ON c.id=m.course_id JOIN app_users u ON u.id=m.user_id
            WHERE m.course_id=$1 AND m.user_id=$2 AND m.status='active' AND u.status='active'
            AND (c.status='active' OR m.course_role IN ('teacher','ta')) FOR SHARE OF c,m,u
            """, course, Actor);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        var role = r.GetString(0);
        var staff = !experience.Requested && AssignmentPolicy.IsStaff(role);
        var manage = staff && r.GetString(1) != "archived" && (role == "teacher" || !r.GetFieldValue<string[]>(2).Contains("materials"));
        return (staff, manage);
    }
    [HttpGet]
    public async Task<IActionResult> List(Guid course, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var access = await Access(db, course, ct); if (access is null) return NotFound();
        await using var cmd = Command(db, """
            SELECT a.id,a.title,a.body,a.status,a.version,a.created_at,a.updated_at,a.published_at,u.display_name
            FROM course_announcements a JOIN app_users u ON u.id=a.author_id
            WHERE a.course_id=$1 AND ($2 OR a.status='published')
            ORDER BY COALESCE(a.published_at,a.created_at) DESC,a.id DESC
            """, course, access.Value.Staff);
        var items = new List<object>();
        await using (var r = await cmd.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) items.Add(new {
            id=r.GetGuid(0),title=r.GetString(1),body=r.GetString(2),status=r.GetString(3),version=r.GetInt32(4),
            createdAt=r.GetFieldValue<DateTimeOffset>(5),updatedAt=r.GetFieldValue<DateTimeOffset>(6),
            publishedAt=r.IsDBNull(7)?(DateTimeOffset?)null:r.GetFieldValue<DateTimeOffset>(7),authorName=r.GetString(8)
        });
        await tx.CommitAsync(ct);
        return Ok(new { items, canManage=access.Value.Manage });
    }
    [CourseCapability("materials"), HttpPost, HttpPut("{id:guid}")]
    public async Task<IActionResult> Save(Guid course, AnnouncementInput input, CancellationToken ct, Guid? id=null)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Body) || input.Title.Trim().Length>200 || input.Body.Trim().Length>16000 || input.Status is not ("draft" or "published"))
            return BadRequest(new { error="請填寫公告標題、內容與有效發布狀態。" });
        await using var db = await source.OpenConnectionAsync(ct); await using var tx = await db.BeginTransactionAsync(ct);
        var access = await Access(db, course, ct); if (access is null || !access.Value.Manage) return Forbid();
        var entity = id ?? Guid.NewGuid();
        await using var cmd = Command(db, id.HasValue ? """
            UPDATE course_announcements SET title=$3,body=$4,status=$5,version=version+1,updated_at=now(),
              published_at=CASE WHEN $5='published' THEN COALESCE(published_at,now()) ELSE NULL END
            WHERE id=$1 AND course_id=$2 AND version=$6 RETURNING version
            """ : """
            INSERT INTO course_announcements(id,course_id,title,body,status,author_id,published_at)
            VALUES($1,$2,$3,$4,$5,$6,CASE WHEN $5='published' THEN now() ELSE NULL END) RETURNING version
            """, entity, course, input.Title.Trim(), input.Body.Trim(), input.Status);
        if (id.HasValue) cmd.Parameters.AddWithValue(input.ExpectedVersion); else cmd.Parameters.AddWithValue(Actor);
        if (await cmd.ExecuteScalarAsync(ct) is not int version) return Conflict(new { error="公告不存在或已被修改，請重新載入。" });
        await using var audit=Command(db,"INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action) VALUES($1,'course_announcements',$2,$3)",Actor,entity,input.Status=="published"?"course.announcement.published":"course.announcement.saved");
        await audit.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct);
        return Ok(new { id=entity, version });
    }
}
