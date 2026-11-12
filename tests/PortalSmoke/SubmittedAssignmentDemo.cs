using System.Text.Json;
using AiTeachingAssistant.Services;
using Npgsql;

internal static class SubmittedAssignmentDemo
{
    private sealed record Target(Guid AssignmentId, Guid CourseId, string CourseTitle, string AssignmentTitle, decimal MaxScore, Guid GraderId);
    private sealed record Question(Guid Id, string Prompt, string Type, string StandardAnswer, decimal? Points);

    private static NpgsqlDataSource Source()
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
            throw new InvalidOperationException("The submitted assignment demo requires Development.");
        var connection=Environment.GetEnvironmentVariable("ConnectionStrings__Portal");
        if(string.IsNullOrWhiteSpace(connection))throw new InvalidOperationException("Specify ConnectionStrings__Portal.");
        return NpgsqlDataSource.Create(connection);
    }

    private static int Variation(Guid student, Guid assignment)
        => student.ToByteArray().Concat(assignment.ToByteArray()).Sum(x=>x)%5;

    public static async Task Seed()
    {
        await using var source=Source();await using var conn=await source.OpenConnectionAsync();await using var tx=await conn.BeginTransactionAsync();
        await using(var advisory=new NpgsqlCommand("SELECT pg_advisory_xact_lock(741026112)",conn,tx))await advisory.ExecuteNonQueryAsync();
        var targets=new List<Target>();
        await using(var cmd=new NpgsqlCommand("""
            SELECT a.id,c.id,c.title,a.title,a.max_score,
              COALESCE((SELECT m.user_id FROM course_members m WHERE m.course_id=c.id AND m.course_role='teacher' AND m.status='active' ORDER BY (m.user_id=c.owner_user_id) DESC,m.joined_at,m.user_id LIMIT 1),c.owner_user_id)
            FROM assignments a JOIN courses c ON c.id=a.course_id
            WHERE c.status='active' AND a.status IN ('published','closed') AND a.submission_mode='individual'
              AND EXISTS(SELECT 1 FROM assignment_questions q WHERE q.assignment_id=a.id)
              AND (c.code LIKE 'DEMO-%' OR EXISTS(SELECT 1 FROM audit_events e WHERE e.entity_id=c.id AND e.action LIKE 'demo.%'))
            ORDER BY c.created_at,c.id,a.created_at,a.id
            """,conn,tx))
        await using(var reader=await cmd.ExecuteReaderAsync())while(await reader.ReadAsync())targets.Add(new(reader.GetGuid(0),reader.GetGuid(1),reader.GetString(2),reader.GetString(3),reader.GetDecimal(4),reader.GetGuid(5)));

        var created=0;var assessed=0;
        foreach(var target in targets)
        {
            var questions=new List<Question>();
            await using(var cmd=new NpgsqlCommand("SELECT id,prompt_text,question_type,standard_answer::text,max_score FROM assignment_questions WHERE assignment_id=$1 ORDER BY sort_order,id",conn,tx))
            {cmd.Parameters.AddWithValue(target.AssignmentId);await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync())questions.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.IsDBNull(4)?null:reader.GetDecimal(4)));}
            var quiz=await Scalar<bool>(conn,tx,"SELECT EXISTS(SELECT 1 FROM course_grade_items WHERE source_assignment_id=$1)",target.AssignmentId);
            var students=new List<(Guid Id,string Name)>();
            await using(var cmd=new NpgsqlCommand("SELECT u.id,u.display_name FROM course_members m JOIN app_users u ON u.id=m.user_id AND u.status='active' WHERE m.course_id=$1 AND m.course_role='student' AND m.status='active' ORDER BY u.student_number NULLS LAST,u.display_name,u.id",conn,tx))
            {cmd.Parameters.AddWithValue(target.CourseId);await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync())students.Add((reader.GetGuid(0),reader.GetString(1)));}

            foreach(var student in students)
            {
                var submission=await Scalar<Guid?>(conn,tx,"SELECT id FROM submissions WHERE assignment_id=$1 AND student_user_id=$2 AND attempt_no=1 AND NOT is_practice",target.AssignmentId,student.Id);
                Guid? snapshot=null;
                if(!submission.HasValue)
                {
                    submission=Guid.NewGuid();var submittedAt=DateTimeOffset.UtcNow.AddMinutes(-(15+Variation(student.Id,target.AssignmentId)*7));
                    await Execute(conn,tx,"INSERT INTO submissions(id,assignment_id,student_user_id,attempt_no,status,submitted_at) VALUES($1,$2,$3,1,'submitted',$4)",submission.Value,target.AssignmentId,student.Id,submittedAt);
                    var rows=new List<object>();
                    foreach(var question in questions)
                    {
                        var answer=Answer(target,student,question);var answerId=Guid.NewGuid();var revision=Guid.NewGuid();
                        await Execute(conn,tx,"INSERT INTO submission_answers(id,submission_id,assignment_id,question_id) VALUES($1,$2,$3,$4)",answerId,submission.Value,target.AssignmentId,question.Id);
                        await Execute(conn,tx,"INSERT INTO answer_revisions(id,answer_id,revision_no,answer_text,created_by_user_id,is_submitted,created_at) VALUES($1,$2,1,$3,$4,true,$5)",revision,answerId,answer,student.Id,submittedAt);
                        await Execute(conn,tx,"UPDATE submission_answers SET current_revision_id=$2 WHERE id=$1",answerId,revision);
                        rows.Add(new{questionId=question.Id,revisionId=revision,text=answer});
                    }
                    snapshot=Guid.NewGuid();
                    await Execute(conn,tx,"INSERT INTO submission_revisions(id,submission_id,revision_no,author_user_id,answers,submitted_at) VALUES($1,$2,1,$3,$4::jsonb,$5)",snapshot.Value,submission.Value,student.Id,JsonSerializer.Serialize(rows),submittedAt);
                    if(quiz)await QuizGrading.Capture(conn,target.AssignmentId,snapshot.Value,false,default);
                    created++;
                }
                else snapshot=await Scalar<Guid?>(conn,tx,"SELECT id FROM submission_revisions WHERE submission_id=$1 ORDER BY revision_no DESC,id DESC LIMIT 1",submission.Value);

                if(snapshot.HasValue && !await Scalar<bool>(conn,tx,"SELECT EXISTS(SELECT 1 FROM grades WHERE submission_revision_id=$1)",snapshot.Value))
                {
                    var (score,essayScores)=await DraftScore(conn,tx,target,snapshot.Value,questions,Variation(student.Id,target.AssignmentId));
                    await Execute(conn,tx,"INSERT INTO grades(submission_revision_id,grader_user_id,score,feedback,essay_scores) VALUES($1,$2,$3,$4,$5::jsonb)",snapshot.Value,target.GraderId,score,$"Codex 示範批改：已回應「{target.AssignmentTitle}」的核心概念並提出具體依據；可再補充限制與後續驗證。",JsonSerializer.Serialize(essayScores));
                    assessed++;
                }
            }
            await Execute(conn,tx,"INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action) SELECT $1,'assignment',$2,'demo.submitted_assignment.20261112' WHERE NOT EXISTS(SELECT 1 FROM audit_events WHERE entity_id=$2 AND action='demo.submitted_assignment.20261112')",target.GraderId,target.AssignmentId);
        }
        await tx.CommitAsync();
        Console.WriteLine($"Submitted assignment demo ready: {targets.Count} assignments, {created} new submissions, {assessed} new Codex draft assessments.");
    }

    private static string Answer(Target target,(Guid Id,string Name) student,Question question)
    {
        if(question.Type is "single" or "multiple" or "true_false")return question.StandardAnswer;
        return $"我在「{target.CourseTitle}」的「{target.AssignmentTitle}」中，先依題目整理可觀察的證據，再說明推論。針對「{question.Prompt}」，我的結論是資料支持目前的解釋，但仍需注意樣本、情境與替代原因；後續可用新的觀察或比較資料驗證。（{student.Name} 示範交卷）";
    }

    private static async Task<(decimal Score,Dictionary<Guid,decimal> Essays)> DraftScore(NpgsqlConnection conn,NpgsqlTransaction tx,Target target,Guid snapshot,List<Question> questions,int variation)
    {
        var essays=new Dictionary<Guid,decimal>();decimal objective=0;
        var findings=await Scalar<string?>(conn,tx,"SELECT findings::text FROM quiz_grading WHERE snapshot_id=$1",snapshot);
        if(findings is not null)
        {
            using var json=JsonDocument.Parse(findings);
            foreach(var mark in json.RootElement.EnumerateArray())
                if(mark.GetProperty("questionType").GetString()=="essay")
                {
                    var id=mark.GetProperty("questionId").GetGuid();var points=mark.GetProperty("points").GetDecimal();essays[id]=decimal.Round(points*(0.78m+variation*0.03m),2);
                }
                else if(mark.TryGetProperty("autoScore",out var autoScore)&&autoScore.ValueKind==JsonValueKind.Number)objective+=autoScore.GetDecimal();
        }
        else foreach(var question in questions.Where(x=>x.Type=="essay"&&x.Points.HasValue))essays[question.Id]=decimal.Round(question.Points!.Value*(0.78m+variation*0.03m),2);
        var score=findings is null&&essays.Count==0?decimal.Round(target.MaxScore*(0.78m+variation*0.03m),2):objective+essays.Values.Sum();
        return (Math.Min(target.MaxScore,score),essays);
    }

    public static async Task Audit()
    {
        await using var source=Source();await using var conn=await source.OpenConnectionAsync();
        const string scope="""
            FROM assignments a JOIN courses c ON c.id=a.course_id
            JOIN course_members m ON m.course_id=c.id AND m.course_role='student' AND m.status='active'
            JOIN app_users u ON u.id=m.user_id AND u.status='active'
            WHERE c.status='active' AND a.status IN ('published','closed') AND a.submission_mode='individual'
              AND EXISTS(SELECT 1 FROM assignment_questions q WHERE q.assignment_id=a.id)
              AND (c.code LIKE 'DEMO-%' OR EXISTS(SELECT 1 FROM audit_events e WHERE e.entity_id=c.id AND e.action LIKE 'demo.%'))
            """;
        var expected=await Scalar<long>(conn,null,"SELECT count(*) "+scope);
        var submitted=await Scalar<long>(conn,null,"SELECT count(*) "+scope+" AND EXISTS(SELECT 1 FROM submissions s WHERE s.assignment_id=a.id AND s.student_user_id=m.user_id AND s.status='submitted' AND NOT s.is_practice)");
        var drafts=await Scalar<long>(conn,null,"SELECT count(*) "+scope+" AND EXISTS(SELECT 1 FROM submissions s JOIN submission_revisions r ON r.submission_id=s.id JOIN grades g ON g.submission_revision_id=r.id WHERE s.assignment_id=a.id AND s.student_user_id=m.user_id AND s.status='submitted' AND NOT s.is_practice AND g.released_at IS NULL)");
        if(expected==0||submitted!=expected||drafts!=expected)throw new InvalidOperationException($"Submitted assignment demo audit failed: expected={expected}, submitted={submitted}, Codex drafts={drafts}.");
        Console.WriteLine($"Submitted assignment demo audit passed: {expected} student-assignment papers submitted and assessed as unpublished drafts.");
    }

    private static async Task Execute(NpgsqlConnection conn,NpgsqlTransaction tx,string sql,params object[] values)
    {await using var cmd=new NpgsqlCommand(sql,conn,tx);foreach(var value in values)cmd.Parameters.AddWithValue(value);await cmd.ExecuteNonQueryAsync();}
    private static async Task<T> Scalar<T>(NpgsqlConnection conn,NpgsqlTransaction? tx,string sql,params object[] values)
    {await using var cmd=new NpgsqlCommand(sql,conn,tx);foreach(var value in values)cmd.Parameters.AddWithValue(value);var result=await cmd.ExecuteScalarAsync();if(result is null||result is DBNull)return default!;return (T)result;}
}
