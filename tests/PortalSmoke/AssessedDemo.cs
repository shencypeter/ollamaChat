using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;

internal static class AssessedDemo
{
    private sealed record DemoRow(
        Guid Report, [property:JsonPropertyName("course_id")]Guid CourseId, string Topic, string Mode, Guid Owner,
        [property:JsonPropertyName("add_member")]bool AddMember,
        int N, Guid? Existing, string Path, decimal Score, string Feedback);

    internal static async Task Seed(string root)
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development")
            throw new InvalidOperationException("The assessed demo requires Development.");
        var connection=Environment.GetEnvironmentVariable("ConnectionStrings__Portal");
        if(string.IsNullOrWhiteSpace(connection))throw new InvalidOperationException("ConnectionStrings__Portal is required.");
        var manifestPath=Path.Combine(root,".codex-build","demo-grading-manifest.json");
        var rows=JsonSerializer.Deserialize<DemoRow[]>(await File.ReadAllTextAsync(manifestPath),new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("The demo grading manifest is empty.");

        await using var database=NpgsqlDataSource.Create(connection);
        await using var db=await database.OpenConnectionAsync();
        await using var tx=await db.BeginTransactionAsync();
        async Task<object?> Sql(string sql,params object[] values)
        {
            await using var command=new NpgsqlCommand(sql,db,tx);
            foreach(var value in values)command.Parameters.AddWithValue(value);
            return await command.ExecuteScalarAsync();
        }

        await Sql("SELECT pg_advisory_xact_lock(741026112)");
        foreach(var row in rows)
        {
            if(await Sql("""
                SELECT a.id FROM report_assignments a JOIN audit_events e ON e.entity_id=a.course_id
                WHERE a.id=$1 AND a.course_id=$2 AND a.status='published'
                  AND e.action='demo.course_workshop.20261111'
                  AND a.submission_mode=$3
                """,row.Report,row.CourseId,row.Mode) is not Guid report)
                throw new InvalidOperationException("Manifest report is outside the demo scope.");
            if(row.AddMember)
            {
                await Sql("INSERT INTO app_users(id,display_name) VALUES($1,$2) ON CONFLICT(id) DO NOTHING",row.Owner,"教學展示補充分組學員");
                await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,'student') ON CONFLICT(course_id,user_id) DO UPDATE SET status='active',course_role='student'",row.CourseId,row.Owner);
                if(row.Mode=="group")await Sql("INSERT INTO course_group_members(group_id,user_id,is_leader) VALUES($1,$2,true) ON CONFLICT DO NOTHING",row.Owner,row.Owner);
            }
            if(row.Mode=="group")
            {
                await Sql("""
                    UPDATE course_group_members gm SET is_leader=(gm.user_id=(SELECT x.user_id FROM course_group_members x JOIN course_members m ON m.course_id=$2 AND m.user_id=x.user_id AND m.status='active' AND m.course_role='student' WHERE x.group_id=$1 AND x.left_at IS NULL ORDER BY x.joined_at,x.user_id LIMIT 1))
                    WHERE gm.group_id=$1 AND gm.left_at IS NULL AND NOT EXISTS(SELECT 1 FROM course_group_members l WHERE l.group_id=$1 AND l.left_at IS NULL AND l.is_leader)
                    """,row.Owner,row.CourseId);
            }
            var bytes=await File.ReadAllBytesAsync(row.Path);
            Guid submission;
            var stored=row.Existing ?? (await Sql(
                row.Mode=="individual"
                    ? "SELECT id FROM report_submissions WHERE report_id=$1 AND student_id=$2 AND NOT is_practice"
                    : "SELECT id FROM report_submissions WHERE report_id=$1 AND group_id=$2 AND NOT is_practice",
                row.Report,row.Owner) as Guid?);
            if(stored.HasValue)
            {
                submission=stored.Value;
                if(await Sql("""
                    UPDATE report_submissions SET file_name=$3,content=$4,score=$5,feedback=$6,graded_by=a.created_by,graded_at=now(),version=report_submissions.version+1
                    FROM report_assignments a WHERE report_submissions.id=$1 AND report_submissions.report_id=$2 AND a.id=report_submissions.report_id
                    RETURNING report_submissions.id
                    """,submission,row.Report,"codex-demo-submission.pdf",bytes,row.Score,row.Feedback) is not Guid)
                    throw new InvalidOperationException("Existing demo submission is missing.");
            }
            else if(row.Mode=="individual")
            {
                submission=(Guid)(await Sql("""
                    INSERT INTO report_submissions(report_id,student_id,file_name,content,score,feedback,graded_by,graded_at)
                    SELECT $1,$2,'codex-demo-submission.pdf',$3,$4,$5,a.created_by,now() FROM report_assignments a
                    JOIN course_members m ON m.course_id=a.course_id AND m.user_id=$2 AND m.status='active' AND m.course_role='student'
                    WHERE a.id=$1 RETURNING id
                    """,row.Report,row.Owner,bytes,row.Score,row.Feedback))!;
            }
            else
            {
                submission=(Guid)(await Sql("""
                    INSERT INTO report_submissions(report_id,student_id,file_name,content,group_id,group_name,score,feedback,graded_by,graded_at)
                    SELECT $1,l.user_id,'codex-demo-submission.pdf',$3,g.id,g.name,$4,$5,a.created_by,now()
                    FROM report_groups rg JOIN report_assignments a ON a.id=rg.report_id
                    JOIN course_groups g ON g.id=rg.group_id
                    JOIN course_group_members l ON l.group_id=g.id AND l.left_at IS NULL AND l.is_leader
                    WHERE rg.report_id=$1 AND rg.group_id=$2 RETURNING id
                    """,row.Report,row.Owner,bytes,row.Score,row.Feedback))!;
            }
            if(row.Mode=="group")
            {
                var snapshotCount=(long)(await Sql("""
                    WITH inserted AS (
                      INSERT INTO report_snapshot(submission_id,report_id,student_id,display_name,is_leader)
                      SELECT $1,$2,gm.user_id,u.display_name,gm.is_leader FROM course_group_members gm
                      JOIN course_members m ON m.course_id=$4 AND m.user_id=gm.user_id AND m.status='active' AND m.course_role='student'
                      JOIN app_users u ON u.id=gm.user_id AND u.status='active'
                      WHERE gm.group_id=$3 AND gm.left_at IS NULL
                      ON CONFLICT(submission_id,student_id) DO UPDATE SET display_name=EXCLUDED.display_name,is_leader=EXCLUDED.is_leader
                      RETURNING 1)
                    SELECT count(*) FROM inserted
                    """,submission,row.Report,row.Owner,row.CourseId))!;
                if(snapshotCount==0)throw new InvalidOperationException("A demo group has no active members.");
                await Sql("""
                    INSERT INTO report_peer_reviews(submission_id,reviewer,subject,ratings,comment)
                    SELECT $1,a.student_id,b.student_id,ARRAY[4+(abs(hashtext(a.student_id::text||b.student_id::text))%2),4,4,4],
                      '示範互評：能完成分工並回應組內修訂。'
                    FROM report_snapshot a JOIN report_snapshot b ON b.submission_id=a.submission_id AND b.student_id<>a.student_id
                    WHERE a.submission_id=$1 ON CONFLICT(submission_id,reviewer,subject) DO NOTHING
                    """,submission);
                await Sql("""
                    INSERT INTO report_votes(submission_id,voter,choice,explanation)
                    SELECT $1,s.student_id,
                      CASE WHEN $2%3=1 THEN 'agree' WHEN $2%3=2 AND row_number() OVER(ORDER BY s.student_id)=1 THEN 'review' WHEN $2%3=0 AND row_number() OVER(ORDER BY s.student_id)=1 THEN 'pending' ELSE 'agree' END,
                      CASE WHEN $2%3=2 AND row_number() OVER(ORDER BY s.student_id)=1 THEN '示範：希望教師參考實際分工。' ELSE '' END
                    FROM report_snapshot s WHERE s.submission_id=$1
                    ON CONFLICT(submission_id,voter) DO UPDATE SET choice=EXCLUDED.choice,explanation=EXCLUDED.explanation
                    """,submission,row.N);
            }
            await Sql("""
                INSERT INTO report_grade_events(submission_id,actor,details)
                SELECT $1,a.created_by,jsonb_build_object('codexDemo',true,'draft',true,'score',$2,'feedback',$3)
                FROM report_submissions s JOIN report_assignments a ON a.id=s.report_id WHERE s.id=$1
                  AND NOT EXISTS(SELECT 1 FROM report_grade_events e WHERE e.submission_id=$1 AND e.details->>'codexDemo'='true' AND e.details->>'draft'='true')
                """,submission,row.Score,row.Feedback);
        }
        foreach(var courseRows in rows.GroupBy(x=>x.CourseId))
        {
            var template=courseRows.First(x=>x.Mode=="individual");
            var bytes=await File.ReadAllBytesAsync(template.Path);
            await Sql("""
                WITH target AS (
                  SELECT a.id,a.created_by FROM report_assignments a
                  WHERE a.id=$1 AND a.course_id=$2 AND a.submission_mode='individual'
                ), inserted AS (
                  INSERT INTO report_submissions(report_id,student_id,file_name,content,score,feedback,graded_by,graded_at)
                  SELECT t.id,m.user_id,'codex-demo-submission.pdf',$3,$4,$5,t.created_by,now()
                  FROM target t JOIN course_members m ON m.course_id=$2 AND m.course_role='student' AND m.status='active'
                  JOIN app_users u ON u.id=m.user_id AND u.status='active'
                  WHERE NOT EXISTS(SELECT 1 FROM report_submissions s WHERE s.report_id=t.id AND s.student_id=m.user_id AND NOT s.is_practice)
                  RETURNING id,report_id
                ), events AS (
                  INSERT INTO report_grade_events(submission_id,actor,details)
                  SELECT i.id,a.created_by,jsonb_build_object('codexDemo',true,'draft',true,'score',$4,'feedback',$5)
                  FROM inserted i JOIN report_assignments a ON a.id=i.report_id RETURNING 1)
                SELECT count(*) FROM inserted
                """,template.Report,template.CourseId,bytes,template.Score,template.Feedback);
        }
        foreach(var course in rows.Select(x=>x.CourseId).Distinct())
            await Sql("INSERT INTO audit_events(entity_type,entity_id,action) SELECT 'course',$1,'demo.codex_assessed.20261111' WHERE NOT EXISTS(SELECT 1 FROM audit_events WHERE entity_id=$1 AND action='demo.codex_assessed.20261111')",course);
        await tx.CommitAsync();
        Console.WriteLine($"Stored {rows.Length} simulated submissions with Codex draft assessments.");
    }

    internal static async Task Audit()
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development")
            throw new InvalidOperationException("The assessed demo audit requires Development.");
        var connection=Environment.GetEnvironmentVariable("ConnectionStrings__Portal");
        if(string.IsNullOrWhiteSpace(connection))throw new InvalidOperationException("ConnectionStrings__Portal is required.");
        await using var database=NpgsqlDataSource.Create(connection);
        await using var db=await database.OpenConnectionAsync();
        const string sql="""
            SELECT c.title,a.title,a.submission_mode,
                   count(s.id),
                   count(s.id) FILTER (WHERE s.score IS NOT NULL AND s.graded_by IS NOT NULL AND s.graded_at IS NOT NULL AND length(trim(s.feedback))>0),
                   count(s.id) FILTER (WHERE EXISTS(SELECT 1 FROM report_grade_events e WHERE e.submission_id=s.id AND e.details->>'codexDemo'='true' AND e.details->>'draft'='true')),
                   count(s.id) FILTER (WHERE s.published_score IS NOT NULL)
            FROM courses c
            JOIN report_assignments a ON a.course_id=c.id AND a.status='published'
            LEFT JOIN report_submissions s ON s.report_id=a.id AND NOT s.is_practice
            WHERE EXISTS(SELECT 1 FROM audit_events e WHERE e.entity_id=c.id AND e.action='demo.course_workshop.20261111')
            GROUP BY c.created_at,c.id,c.title,a.created_at,a.id,a.title,a.submission_mode
            ORDER BY c.created_at,c.id,a.created_at,a.id
            """;
        await using var command=new NpgsqlCommand(sql,db);
        await using var reader=await command.ExecuteReaderAsync();
        var assignments=0;long submissions=0;long published=0;
        while(await reader.ReadAsync())
        {
            var course=reader.GetString(0);var assignment=reader.GetString(1);var mode=reader.GetString(2);
            var submitted=reader.GetInt64(3);var assessed=reader.GetInt64(4);var codex=reader.GetInt64(5);var released=reader.GetInt64(6);
            if(submitted==0 || assessed!=submitted || codex!=submitted)
                throw new InvalidOperationException($"Assessed demo audit failed for {course} / {assignment}: submitted={submitted}, assessed={assessed}, codexDrafts={codex}.");
            Console.WriteLine($"OK {course} / {assignment} ({mode}): {submitted} submitted, {assessed} Codex-assessed, {released} published.");
            assignments++;submissions+=submitted;published+=released;
        }
        if(assignments==0)throw new InvalidOperationException("No demo assignments were found.");
        Console.WriteLine($"Assessed demo audit passed: {assignments} assignments, {submissions} assessed submissions, {published} published scores.");
    }
}
