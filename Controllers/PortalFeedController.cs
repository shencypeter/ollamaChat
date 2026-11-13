using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace AiTeachingAssistant.Controllers;

[ApiController, Authorize, Route("api/portal-feed")]
public sealed class PortalFeedController(NpgsqlDataSource source, LearnerExperience experience) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    // Always derive visibility from a current membership. A view parameter never promotes a student.
    private const string Eligible = """
        WITH eligible AS (
          SELECT c.id,c.title,m.course_role FROM courses c
          JOIN course_members m ON m.course_id=c.id JOIN app_users u ON u.id=m.user_id
          WHERE m.user_id=$1 AND m.status='active' AND u.status='active' AND c.status='active'
            AND ($2='all' OR $2='student' AND m.course_role='student' OR $2='teaching' AND m.course_role IN ('teacher','ta'))
            AND (NOT $3 OR m.course_role='student') AND ($4::uuid IS NULL OR c.id=$4)
        ), events AS (
        """;
    private NpgsqlCommand Query(string sql, string view, Guid? courseId)
    {
        var cmd=source.CreateCommand(sql);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(view);
        cmd.Parameters.AddWithValue(experience.Requested);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Uuid,Value=(object?)courseId??DBNull.Value });
        return cmd;
    }
    private static string TargetUrl(string kind, Guid course, Guid id) => kind switch {
        "announcement"=>$"/courses/{course}?section=announcements&announcement={id}",
        "material"=>$"/courses/{course}/materials/{id}",
        "assignment"=>$"/courses/{course}/assignments/{id}",
        "quiz"=>$"/courses/{course}/quizzes/{id}",
        "report"=>$"/courses/{course}/reports/{id}",
        "discussion"=>$"/courses/{course}?section=questions&thread={id}",
        "exam"=>$"/courses/{course}?section=assessments&onlineExam={id}",
        "midterm" or "final"=>$"/courses/{course}?section=assessments&exam={id}",
        _=>$"/courses/{course}?section=grades"
    };
    [HttpGet]
    public async Task<IActionResult> Feed(CancellationToken ct, [FromQuery,Range(1,100000)] int page=1,
        [FromQuery,Range(1,50)] int pageSize=20,[FromQuery] string view="all",[FromQuery] Guid? courseId=null)
    {
        if(view is not ("all" or "student" or "teaching") || page<1 || page>100000 || pageSize<1 || pageSize>50) return BadRequest();
        await using var cmd=Query(Eligible + """
            SELECT a.id,'announcement' kind,c.id course_id,c.title course_title,a.title,
              GREATEST(a.published_at,a.updated_at) occurred_at,u.display_name author_name
            FROM eligible c JOIN course_announcements a ON a.course_id=c.id JOIN app_users u ON u.id=a.author_id WHERE a.status='published'
            UNION ALL
            SELECT a.id,'material',c.id,c.title,COALESCE(NULLIF(a.display_title,''),a.file_name),a.created_at,u.display_name
            FROM eligible c JOIN course_resources a ON a.course_id=c.id JOIN app_users u ON u.id=a.uploaded_by
            UNION ALL
            SELECT a.id,CASE WHEN i.category='quiz' THEN 'quiz' ELSE 'assignment' END,c.id,c.title,a.title,a.created_at,NULL::text
            FROM eligible c JOIN assignments a ON a.course_id=c.id LEFT JOIN course_grade_items i ON i.source_assignment_id=a.id
            WHERE a.status IN ('published','closed') AND a.submission_mode='individual'
            UNION ALL
            SELECT a.id,'report',c.id,c.title,a.title,a.created_at,NULL::text FROM eligible c JOIN report_assignments a ON a.course_id=c.id
            WHERE a.status IN ('published','closed') AND (c.course_role IN ('teacher','ta') OR a.submission_mode='individual'
              OR EXISTS(SELECT 1 FROM report_group_audience WHERE report_id=a.id AND student_id=$1))
            UNION ALL
            SELECT a.id,'exam',c.id,c.title,a.title,a.created_at,NULL::text FROM eligible c JOIN exams a ON a.course_id=c.id WHERE a.status IN ('published','closed')
            UNION ALL
            SELECT i.id,'grade',c.id,c.title,i.title,s.published_at,NULL::text FROM eligible c
              JOIN course_grade_scores s ON s.course_id=c.id JOIN course_grade_items i ON i.id=s.item_id
            WHERE c.course_role='student' AND s.student_id=$1 AND s.published_score IS NOT NULL
              AND NOT EXISTS(SELECT 1 FROM assignments a WHERE a.id=i.source_assignment_id AND (a.status='draft' OR a.submission_mode<>'individual'))
              AND NOT EXISTS(SELECT 1 FROM report_assignments a WHERE a.id=i.source_report_id AND (a.status='draft' OR a.submission_mode='group'
                AND NOT EXISTS(SELECT 1 FROM report_group_audience WHERE report_id=a.id AND student_id=$1)))
            UNION ALL
            SELECT q.id,'discussion',c.id,c.title,COALESCE(NULLIF(q.title,''),left(btrim(q.question),200)),
              GREATEST(q.created_at,COALESCE(q.answered_at,q.created_at),COALESCE((SELECT max(r.created_at) FROM course_question_replies r WHERE r.question_id=q.id),q.created_at)),u.display_name
            FROM eligible c JOIN course_questions q ON q.course_id=c.id JOIN app_users u ON u.id=q.author_id WHERE c.course_role IN ('teacher','ta')
            ) SELECT id,kind,course_id,course_title,title,occurred_at,author_name FROM events
            ORDER BY occurred_at DESC,course_id,kind,id LIMIT $5 OFFSET $6
            """,view,courseId);
        cmd.Parameters.AddWithValue(pageSize+1);cmd.Parameters.AddWithValue((page-1)*pageSize);
        var items=new List<object>();await using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))items.Add(new{id=r.GetGuid(0),kind=r.GetString(1),courseId=r.GetGuid(2),courseTitle=r.GetString(3),title=r.GetString(4),occurredAt=r.GetFieldValue<DateTimeOffset>(5),authorName=r.IsDBNull(6)?null:r.GetString(6),url=TargetUrl(r.GetString(1),r.GetGuid(2),r.GetGuid(0))});
        var hasMore=items.Count>pageSize;if(hasMore)items.RemoveAt(items.Count-1);
        return Ok(new{items,page,pageSize,hasMore});
    }
    [HttpGet("agenda")]
    public async Task<IActionResult> Agenda(CancellationToken ct,[FromQuery] string view="all",[FromQuery] Guid? courseId=null)
    {
        if(view is not ("all" or "student" or "teaching"))return BadRequest();
        await using var cmd=Query(Eligible + """
            SELECT a.id,CASE WHEN i.category='quiz' THEN 'quiz' ELSE 'assignment' END kind,c.id course_id,c.title course_title,a.title,a.due_at,a.opens_at
            FROM eligible c JOIN assignments a ON a.course_id=c.id LEFT JOIN course_grade_items i ON i.source_assignment_id=a.id
            WHERE a.status='published' AND a.submission_mode='individual'
            UNION ALL
            SELECT a.id,'report',c.id,c.title,a.title,a.due_at,a.opens_at FROM eligible c JOIN report_assignments a ON a.course_id=c.id
            WHERE a.status='published' AND (c.course_role IN ('teacher','ta') OR a.submission_mode='individual'
              OR EXISTS(SELECT 1 FROM report_group_audience WHERE report_id=a.id AND student_id=$1))
            UNION ALL
            SELECT a.id,'exam',c.id,c.title,a.title,a.due_at,a.opens_at FROM eligible c JOIN exams a ON a.course_id=c.id WHERE a.status='published'
            UNION ALL
            SELECT a.id,a.category,c.id,c.title,a.title,a.scheduled_at,NULL::timestamptz FROM eligible c JOIN course_grade_items a ON a.course_id=c.id
            WHERE a.category IN ('midterm','final') AND a.source_assignment_id IS NULL AND a.source_report_id IS NULL
            ) SELECT id,kind,course_id,course_title,title,due_at,opens_at FROM events
            WHERE COALESCE(due_at,opens_at) >= date_trunc('day',now() AT TIME ZONE 'Asia/Taipei') AT TIME ZONE 'Asia/Taipei'
            ORDER BY COALESCE(due_at,opens_at),course_id,kind,id LIMIT 101
            """,view,courseId);
        var items=new List<object>();await using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))items.Add(new{id=r.GetGuid(0),kind=r.GetString(1),courseId=r.GetGuid(2),courseTitle=r.GetString(3),title=r.GetString(4),dueAt=r.IsDBNull(5)?(DateTimeOffset?)null:r.GetFieldValue<DateTimeOffset>(5),opensAt=r.IsDBNull(6)?(DateTimeOffset?)null:r.GetFieldValue<DateTimeOffset>(6),url=TargetUrl(r.GetString(1),r.GetGuid(2),r.GetGuid(0))});
        var hasMore=items.Count>100;if(hasMore)items.RemoveAt(items.Count-1);
        return Ok(new{items,hasMore});
    }
}
