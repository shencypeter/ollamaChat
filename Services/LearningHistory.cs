using AiTeachingAssistant.Models;
using Npgsql;
using NpgsqlTypes;
namespace AiTeachingAssistant.Services;

public sealed record LearningCourse(Guid Id,string Code,string Title,string Term,string Status,DateTimeOffset JoinedAt,
    int Required,int Submitted,int Pending,decimal? Average);
public sealed record LearningActivity(Guid Id,Guid CourseId,string Kind,string Title,string State,DateTimeOffset? OpensAt,
    DateTimeOffset? DueAt,DateTimeOffset? OccurredAt,decimal? Score,decimal? MaxScore,string Feedback,string Url,int? Revision,long? EffectiveSeconds);
public sealed record LearningPage(IReadOnlyList<LearningActivity> Items,long Total,int Page,int PageSize);

// All branches derive ownership from the authenticated user and the real course role.
// This projection never creates drafts and never reads answer bodies or attachment bytes.
public sealed class LearningHistory(NpgsqlDataSource source,TimeProvider clock)
{
    private const string CoursesSql="""
        SELECT c.id,c.code,c.title,c.term_code,c.status,m.joined_at
        FROM courses c JOIN course_members m ON m.course_id=c.id AND c.status<>'draft'
        WHERE m.user_id=$1 AND m.course_role='student' AND m.status='active'
        """;
    private const string ActivitiesSql="""
        WITH mine AS (
          SELECT c.id FROM courses c JOIN course_members m ON m.course_id=c.id AND c.status<>'draft'
          WHERE m.user_id=$1 AND m.course_role='student' AND m.status='active'
        ), activities AS (
          SELECT a.id,a.course_id,'quiz'::text kind,a.title,
            CASE WHEN s.status='submitted' THEN CASE WHEN g.id IS NOT NULL THEN 'graded' ELSE 'pending' END
                 WHEN a.opens_at>$2 THEN 'upcoming' WHEN a.status='closed' THEN 'closed'
                 WHEN a.due_at<=$2 THEN 'overdue' ELSE 'todo' END state,
            a.opens_at,a.due_at,COALESCE(g.released_at,s.submitted_at) occurred_at,
            g.score,a.max_score,COALESCE(g.feedback,'') feedback,'/quizzes/'||a.id::text url,sr.revision_no revision,NULL::bigint effective_seconds
          FROM assignments a JOIN mine ON mine.id=a.course_id
          JOIN course_grade_items gi ON gi.source_assignment_id=a.id
          LEFT JOIN submissions s ON s.assignment_id=a.id AND s.student_user_id=$1 AND NOT s.is_practice AND s.attempt_no=1
          LEFT JOIN LATERAL (SELECT id,revision_no FROM submission_revisions WHERE submission_id=s.id ORDER BY revision_no DESC LIMIT 1) sr ON true
          LEFT JOIN LATERAL (SELECT id,score,feedback,released_at FROM grades WHERE submission_revision_id=sr.id AND released_at IS NOT NULL ORDER BY graded_at DESC,id DESC LIMIT 1) g ON true
          WHERE a.status<>'draft'
          UNION ALL
          SELECT a.id,a.course_id,'report',a.title,
            CASE WHEN s.id IS NOT NULL THEN CASE WHEN COALESCE(own.published_score,CASE WHEN s.group_id IS NULL THEN s.published_score END) IS NOT NULL THEN 'graded' ELSE 'pending' END
                 WHEN a.opens_at>$2 THEN 'upcoming' WHEN a.status='closed' THEN 'closed'
                 WHEN a.due_at<=$2 THEN 'overdue' ELSE 'todo' END,
            a.opens_at,a.due_at,COALESCE(s.graded_at,s.submitted_at),COALESCE(own.published_score,CASE WHEN s.group_id IS NULL THEN s.published_score END),a.max_score,COALESCE(s.published_feedback,''),'/reports/'||a.id::text,NULL::int,NULL::bigint
          FROM report_assignments a JOIN mine ON mine.id=a.course_id
          LEFT JOIN report_snapshot own ON own.report_id=a.id AND own.student_id=$1
          LEFT JOIN report_submissions s ON s.report_id=a.id AND (s.id=own.submission_id OR s.group_id IS NULL AND s.student_id=$1) AND NOT s.is_practice
          WHERE a.status<>'draft' AND (a.submission_mode='individual' OR EXISTS(SELECT 1 FROM report_group_audience WHERE report_id=a.id AND student_id=$1))
          UNION ALL
          SELECT a.id,c.course_id,'case',v.title,'submitted',NULL::timestamptz,NULL::timestamptz,a.submitted_at,
            NULL::numeric,NULL::numeric,''::text,'/student/cases/'||c.id::text,NULL::int,NULL::bigint
          FROM studio_attempts a JOIN studio_case_versions v ON v.id=a.case_version_id
          JOIN studio_cases c ON c.id=v.case_id JOIN mine ON mine.id=c.course_id
          WHERE a.student_id=$1 AND NOT a.is_practice AND a.submitted_at IS NOT NULL
          UNION ALL
          SELECT i.id,i.course_id,'offline',i.title,'graded',NULL::timestamptz,NULL::timestamptz,s.updated_at,
            s.published_score,i.max_score,''::text,'/assessment-grades?course='||i.course_id::text,NULL::int,NULL::bigint
          FROM course_grade_items i JOIN mine ON mine.id=i.course_id
          JOIN course_grade_scores s ON s.item_id=i.id AND s.student_id=$1
          WHERE i.source_assignment_id IS NULL AND i.source_report_id IS NULL AND s.published_score IS NOT NULL
          UNION ALL
          SELECT r.id,r.course_id,'material',COALESCE(NULLIF(r.display_title,''),r.file_name),'studied',NULL::timestamptz,NULL::timestamptz,max(s.last_seen_at),
            NULL::numeric,NULL::numeric,''::text,'/courses/'||r.course_id::text||'/materials/'||r.id::text,NULL::int,(floor(sum(s.credited_milliseconds)/1000.0))::bigint
          FROM course_resources r JOIN mine ON mine.id=r.course_id
          JOIN learning_time_sessions s ON s.resource_id=r.id AND s.user_id=$1 AND NOT s.is_practice
          GROUP BY r.id HAVING sum(s.credited_milliseconds)>0
        )
        """;
    private NpgsqlCommand Query(NpgsqlConnection conn,string suffix,Guid actor)
    {
        var cmd=new NpgsqlCommand(ActivitiesSql+suffix,conn);cmd.Parameters.AddWithValue(actor);cmd.Parameters.AddWithValue(clock.GetUtcNow());return cmd;
    }
    private static DateTimeOffset? Date(NpgsqlDataReader r,int i)=>r.IsDBNull(i)?null:r.GetFieldValue<DateTimeOffset>(i);
    public async Task<IReadOnlyList<LearningCourse>> Courses(Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        var stats=new Dictionary<Guid,(int Required,int Submitted,int Pending)>();
        await using(var cmd=Query(conn,"""
             SELECT course_id,count(*) FILTER(WHERE kind IN ('quiz','report') AND state<>'upcoming')::int,
               count(*) FILTER(WHERE kind IN ('quiz','report') AND state IN ('pending','graded'))::int,
               count(*) FILTER(WHERE kind IN ('quiz','report') AND state='pending')::int
             FROM activities GROUP BY course_id
            """,actor))
        {await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))stats[r.GetGuid(0)]=(r.GetInt32(1),r.GetInt32(2),r.GetInt32(3));}
        var averages=new Dictionary<Guid,decimal>();
        await using(var cmd=new NpgsqlCommand("""
            SELECT f.course_id,f.published_total FROM course_final_grades f WHERE f.student_id=$1 AND f.published_total IS NOT NULL
            """,conn))
        {cmd.Parameters.AddWithValue(actor);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))averages[r.GetGuid(0)]=r.GetDecimal(1);}
        var result=new List<LearningCourse>();
        await using(var cmd=new NpgsqlCommand(CoursesSql+" ORDER BY c.term_code DESC,c.code,c.id",conn))
        {cmd.Parameters.AddWithValue(actor);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct)){
            var id=r.GetGuid(0);var s=stats.GetValueOrDefault(id);
            result.Add(new(id,r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetFieldValue<DateTimeOffset>(5),s.Required,s.Submitted,s.Pending,averages.TryGetValue(id,out var avg)?avg:null));}}
        await tx.CommitAsync(ct);return result;
    }
    public async Task<LearningPage> Activities(Guid actor,Guid? course,string? term,string? kind,DateTimeOffset? from,DateTimeOffset? until,int page,int pageSize,bool todo,CancellationToken ct)
    {
        if(page<1||page>100000||pageSize is <1 or >100||term?.Length>80||kind is not (null or "" or "quiz" or "report" or "case" or "offline" or "material")||from>until)
            throw new AssignmentWorkflowException(400,"歷程篩選或分頁參數無效。");
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        var filter="""
             FROM activities a JOIN courses c ON c.id=a.course_id
             WHERE ($3::uuid IS NULL OR a.course_id=$3) AND ($4='' OR c.term_code=$4) AND ($5='' OR a.kind=$5)
               AND ($6::timestamptz IS NULL OR a.occurred_at>=$6) AND ($7::timestamptz IS NULL OR a.occurred_at<$7)
               AND (NOT $8 OR (a.kind IN ('quiz','report') AND a.state IN ('todo','overdue','upcoming') AND c.status<>'archived'))
            """;
        NpgsqlCommand Make(string select){var cmd=Query(conn,select,actor);
            cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlDbType.Uuid,Value=(object?)course??DBNull.Value});cmd.Parameters.AddWithValue(term??"");cmd.Parameters.AddWithValue(kind??"");
            cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlDbType.TimestampTz,Value=(object?)from?.ToUniversalTime()??DBNull.Value});cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlDbType.TimestampTz,Value=(object?)until?.ToUniversalTime()??DBNull.Value});cmd.Parameters.AddWithValue(todo);return cmd;}
        long total;await using(var cmd=Make(" SELECT count(*)"+filter))total=(long)(await cmd.ExecuteScalarAsync(ct))!;
        var items=new List<LearningActivity>();
        await using(var cmd=Make(" SELECT a.*"+filter+(todo?" ORDER BY a.due_at ASC NULLS LAST,a.id":" ORDER BY a.occurred_at DESC NULLS LAST,a.due_at ASC NULLS LAST,a.id")+" LIMIT $9 OFFSET $10"))
        {cmd.Parameters.AddWithValue(pageSize);cmd.Parameters.AddWithValue((page-1)*pageSize);await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))items.Add(new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetString(4),Date(r,5),Date(r,6),Date(r,7),r.IsDBNull(8)?null:r.GetDecimal(8),r.IsDBNull(9)?null:r.GetDecimal(9),r.GetString(10),r.GetString(11),r.IsDBNull(12)?null:r.GetInt32(12),r.IsDBNull(13)?null:r.GetInt64(13)));}
        await tx.CommitAsync(ct);return new(items,total,page,pageSize);
    }
}
