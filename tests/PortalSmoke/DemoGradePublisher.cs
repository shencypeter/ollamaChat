using AiTeachingAssistant.Services;
using Npgsql;

internal static class DemoGradePublisher
{
    private static NpgsqlDataSource Source()
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development")throw new InvalidOperationException("Demo grade publication requires Development.");
        var connection=Environment.GetEnvironmentVariable("ConnectionStrings__Portal");
        if(string.IsNullOrWhiteSpace(connection))throw new InvalidOperationException("ConnectionStrings__Portal is required.");
        return NpgsqlDataSource.Create(connection);
    }

    internal static async Task Publish()
    {
        await using var source=Source();await using var conn=await source.OpenConnectionAsync();await using var tx=await conn.BeginTransactionAsync();
        await Execute(conn,tx,"SELECT pg_advisory_xact_lock(741026113)");
        var onlineAssignments=new List<(Guid Id,Guid Actor,string Title)>();
        await using(var cmd=new NpgsqlCommand("""
            SELECT a.id,COALESCE((SELECT m.user_id FROM course_members m WHERE m.course_id=c.id AND m.course_role='teacher' AND m.status='active' ORDER BY (m.user_id=c.owner_user_id) DESC,m.joined_at,m.user_id LIMIT 1),c.owner_user_id),c.title||' / '||a.title
            FROM assignments a JOIN courses c ON c.id=a.course_id
            WHERE c.status='active' AND a.status IN ('published','closed') AND a.submission_mode='individual'
              AND EXISTS(SELECT 1 FROM assignment_questions q WHERE q.assignment_id=a.id)
              AND (c.code LIKE 'DEMO-%' OR EXISTS(SELECT 1 FROM audit_events e WHERE e.entity_id=c.id AND e.action LIKE 'demo.%'))
            ORDER BY c.id,a.id
            """,conn,tx))
        await using(var reader=await cmd.ExecuteReaderAsync())while(await reader.ReadAsync())onlineAssignments.Add((reader.GetGuid(0),reader.GetGuid(1),reader.GetString(2)));

        var onlineReleased=0;
        foreach(var assignment in onlineAssignments)
        {
            var expected=await Scalar<long>(conn,tx,"SELECT count(*) FROM assignments a JOIN course_members m ON m.course_id=a.course_id AND m.course_role='student' AND m.status='active' JOIN app_users u ON u.id=m.user_id AND u.status='active' WHERE a.id=$1",assignment.Id);
            var rows=new List<(Guid Snapshot,Guid Grade,decimal Score)>();
            await using(var cmd=new NpgsqlCommand("""
                SELECT r.id,g.id,g.score
                FROM assignments a JOIN course_members m ON m.course_id=a.course_id AND m.course_role='student' AND m.status='active'
                JOIN app_users u ON u.id=m.user_id AND u.status='active'
                JOIN submissions s ON s.assignment_id=a.id AND s.student_user_id=m.user_id AND s.status='submitted' AND NOT s.is_practice AND s.attempt_no=1
                JOIN LATERAL (SELECT id FROM submission_revisions WHERE submission_id=s.id ORDER BY revision_no DESC,id DESC LIMIT 1) r ON true
                JOIN LATERAL (SELECT id,score FROM grades WHERE submission_revision_id=r.id ORDER BY graded_at DESC,id DESC LIMIT 1) g ON true
                WHERE a.id=$1 ORDER BY m.user_id
                """,conn,tx)){cmd.Parameters.AddWithValue(assignment.Id);await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync())rows.Add((reader.GetGuid(0),reader.GetGuid(1),reader.GetDecimal(2)));}
            if(expected==0||rows.Count!=expected)throw new InvalidOperationException($"Cannot publish incomplete online assignment {assignment.Title}: graded={rows.Count}, expected={expected}.");
            foreach(var row in rows)
            {
                await Execute(conn,tx,"UPDATE grades SET released_at=COALESCE(released_at,now()) WHERE id=$1",row.Grade);
                await AssessmentGradeSync.ReleaseQuiz(conn,assignment.Id,row.Snapshot,row.Score,assignment.Actor,default);
                onlineReleased++;
            }
            await Execute(conn,tx,"INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata) VALUES($1,'assignment',$2,'demo.grade_batch_released.20261112',jsonb_build_object('students',$3))",assignment.Actor,assignment.Id,rows.Count);
        }

        var reports=new List<(Guid Id,Guid Course,Guid Item,Guid Actor,string Mode,decimal Max,string Title)>();
        await using(var cmd=new NpgsqlCommand("""
            SELECT a.id,a.course_id,i.id,a.created_by,a.submission_mode,a.max_score,c.title||' / '||a.title
            FROM report_assignments a JOIN courses c ON c.id=a.course_id JOIN course_grade_items i ON i.source_report_id=a.id
            WHERE a.status='published' AND EXISTS(SELECT 1 FROM audit_events e WHERE e.entity_id=c.id AND e.action='demo.course_workshop.20261111')
            ORDER BY c.id,a.id
            """,conn,tx))
        await using(var reader=await cmd.ExecuteReaderAsync())while(await reader.ReadAsync())reports.Add((reader.GetGuid(0),reader.GetGuid(1),reader.GetGuid(2),reader.GetGuid(3),reader.GetString(4),reader.GetDecimal(5),reader.GetString(6)));

        var reportReleased=0;var memberReleased=0;
        foreach(var report in reports)
        {
            if(report.Mode=="individual")
            {
                var expected=await Scalar<long>(conn,tx,"SELECT count(*) FROM course_members m JOIN app_users u ON u.id=m.user_id AND u.status='active' WHERE m.course_id=$1 AND m.course_role='student' AND m.status='active'",report.Course);
                var rows=new List<(Guid Submission,Guid Student,decimal Score,string Feedback)>();
                await using(var cmd=new NpgsqlCommand("SELECT s.id,s.student_id,s.score,s.feedback FROM report_submissions s WHERE s.report_id=$1 AND NOT s.is_practice AND NOT s.returned AND s.group_id IS NULL AND s.score IS NOT NULL ORDER BY s.student_id",conn,tx)){cmd.Parameters.AddWithValue(report.Id);await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync())rows.Add((reader.GetGuid(0),reader.GetGuid(1),reader.GetDecimal(2),reader.GetString(3)));}
                if(expected==0||rows.Count!=expected)throw new InvalidOperationException($"Cannot publish incomplete report {report.Title}: graded={rows.Count}, expected={expected}.");
                foreach(var row in rows)
                {
                    GradebookPolicy.ValidateScore(row.Score,report.Max);
                    await Execute(conn,tx,"UPDATE report_submissions SET published_score=score,published_feedback=feedback WHERE id=$1",row.Submission);
                    await AssessmentGradeSync.SetScore(conn,report.Item,report.Course,row.Student,row.Score,report.Actor,default);
                    await Event(conn,tx,row.Submission,report.Actor,row.Score,rows.Count);
                    reportReleased++;
                }
            }
            else
            {
                var expectedGroups=await Scalar<long>(conn,tx,"SELECT count(*) FROM report_groups WHERE report_id=$1",report.Id);
                var submissions=new List<(Guid Id,decimal Score)>();
                await using(var cmd=new NpgsqlCommand("SELECT id,score FROM report_submissions WHERE report_id=$1 AND NOT is_practice AND NOT returned AND group_id IS NOT NULL AND score IS NOT NULL ORDER BY group_id",conn,tx)){cmd.Parameters.AddWithValue(report.Id);await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync())submissions.Add((reader.GetGuid(0),reader.GetDecimal(1)));}
                if(expectedGroups==0||submissions.Count!=expectedGroups)throw new InvalidOperationException($"Cannot publish incomplete group report {report.Title}: graded groups={submissions.Count}, expected={expectedGroups}.");
                foreach(var submission in submissions)
                {
                    GradebookPolicy.ValidateScore(submission.Score,report.Max);
                    await Execute(conn,tx,"UPDATE report_submissions SET published_score=score,published_feedback=feedback WHERE id=$1",submission.Id);
                    var members=new List<(Guid Student,decimal Score)>();
                    await using(var cmd=new NpgsqlCommand("UPDATE report_snapshot SET published_score=COALESCE(override_score,$2) WHERE submission_id=$1 RETURNING student_id,published_score",conn,tx)){cmd.Parameters.AddWithValue(submission.Id);cmd.Parameters.AddWithValue(submission.Score);await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync())members.Add((reader.GetGuid(0),reader.GetDecimal(1)));}
                    if(members.Count==0)throw new InvalidOperationException($"Cannot publish group report {report.Title}: submission {submission.Id} has no member snapshot.");
                    foreach(var member in members){GradebookPolicy.ValidateScore(member.Score,report.Max);await AssessmentGradeSync.SetScore(conn,report.Item,report.Course,member.Student,member.Score,report.Actor,default);memberReleased++;}
                    await Event(conn,tx,submission.Id,report.Actor,submission.Score,members.Count);reportReleased++;
                }
            }
            await Execute(conn,tx,"INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata) VALUES($1,'report',$2,'demo.grade_batch_released.20261112',jsonb_build_object('mode',$3))",report.Actor,report.Id,report.Mode);
        }
        await tx.CommitAsync();
        Console.WriteLine($"Published all complete demo grades: {onlineAssignments.Count} online assignments / {onlineReleased} students; {reports.Count} PDF assignments / {reportReleased} submissions / {memberReleased} group members.");
    }

    private static Task Event(NpgsqlConnection conn,NpgsqlTransaction tx,Guid submission,Guid actor,decimal score,int count)
        => Execute(conn,tx,"INSERT INTO report_grade_events(submission_id,actor,details) VALUES($1,$2,jsonb_build_object('batchRelease',true,'published',true,'score',$3,'audience',$4))",submission,actor,score,count);

    internal static async Task Audit()
    {
        await using var source=Source();await using var conn=await source.OpenConnectionAsync();
        const string onlineScope="""
            FROM assignments a JOIN courses c ON c.id=a.course_id
            JOIN course_members m ON m.course_id=c.id AND m.course_role='student' AND m.status='active'
            JOIN app_users u ON u.id=m.user_id AND u.status='active'
            WHERE c.status='active' AND a.status IN ('published','closed') AND a.submission_mode='individual'
              AND EXISTS(SELECT 1 FROM assignment_questions q WHERE q.assignment_id=a.id)
              AND (c.code LIKE 'DEMO-%' OR EXISTS(SELECT 1 FROM audit_events e WHERE e.entity_id=c.id AND e.action LIKE 'demo.%'))
            """;
        var onlineExpected=await Scalar<long>(conn,null,"SELECT count(*) "+onlineScope);
        var onlineReleased=await Scalar<long>(conn,null,"SELECT count(*) "+onlineScope+" AND EXISTS(SELECT 1 FROM submissions s JOIN LATERAL(SELECT id FROM submission_revisions WHERE submission_id=s.id ORDER BY revision_no DESC,id DESC LIMIT 1) r ON true JOIN LATERAL(SELECT released_at FROM grades WHERE submission_revision_id=r.id ORDER BY graded_at DESC,id DESC LIMIT 1) g ON true WHERE s.assignment_id=a.id AND s.student_user_id=m.user_id AND s.status='submitted' AND NOT s.is_practice AND g.released_at IS NOT NULL)");
        var onlineBook=await Scalar<long>(conn,null,"SELECT count(*) "+onlineScope+" AND (NOT EXISTS(SELECT 1 FROM course_grade_items i WHERE i.source_assignment_id=a.id) OR EXISTS(SELECT 1 FROM course_grade_items i JOIN course_grade_scores s ON s.item_id=i.id AND s.student_id=m.user_id WHERE i.source_assignment_id=a.id AND s.published_score IS NOT NULL))");
        var reports=await Scalar<long>(conn,null,"SELECT count(*) FROM report_submissions s JOIN report_assignments a ON a.id=s.report_id JOIN courses c ON c.id=a.course_id WHERE NOT s.is_practice AND s.score IS NOT NULL AND s.published_score=s.score AND EXISTS(SELECT 1 FROM audit_events e WHERE e.entity_id=c.id AND e.action='demo.course_workshop.20261111')");
        var reportExpected=await Scalar<long>(conn,null,"SELECT count(*) FROM report_submissions s JOIN report_assignments a ON a.id=s.report_id JOIN courses c ON c.id=a.course_id WHERE NOT s.is_practice AND s.score IS NOT NULL AND EXISTS(SELECT 1 FROM audit_events e WHERE e.entity_id=c.id AND e.action='demo.course_workshop.20261111')");
        var groupMissing=await Scalar<long>(conn,null,"SELECT count(*) FROM report_snapshot m JOIN report_submissions s ON s.id=m.submission_id JOIN report_assignments a ON a.id=s.report_id JOIN courses c ON c.id=a.course_id WHERE m.published_score IS NULL AND EXISTS(SELECT 1 FROM audit_events e WHERE e.entity_id=c.id AND e.action='demo.course_workshop.20261111')");
        if(onlineExpected==0||onlineReleased!=onlineExpected||onlineBook!=onlineExpected||reports!=reportExpected||groupMissing!=0)throw new InvalidOperationException($"Demo grade publication audit failed: online={onlineReleased}/{onlineExpected}, gradebook={onlineBook}/{onlineExpected}, PDF={reports}/{reportExpected}, unpublished group members={groupMissing}.");
        Console.WriteLine($"Demo grade publication audit passed: online {onlineReleased}/{onlineExpected}, gradebook {onlineBook}/{onlineExpected}, PDF {reports}/{reportExpected}, unpublished group members {groupMissing}.");
    }

    private static async Task Execute(NpgsqlConnection conn,NpgsqlTransaction tx,string sql,params object[] values){await using var cmd=new NpgsqlCommand(sql,conn,tx);foreach(var value in values)cmd.Parameters.AddWithValue(value);await cmd.ExecuteNonQueryAsync();}
    private static async Task<T> Scalar<T>(NpgsqlConnection conn,NpgsqlTransaction? tx,string sql,params object[] values){await using var cmd=new NpgsqlCommand(sql,conn,tx);foreach(var value in values)cmd.Parameters.AddWithValue(value);var result=await cmd.ExecuteScalarAsync();if(result is null||result is DBNull)return default!;return (T)result;}
}
