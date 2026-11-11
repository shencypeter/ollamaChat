using Npgsql;
namespace AiTeachingAssistant.Services;
public static class AssessmentGradeSync
{
    public static async Task SetScore(NpgsqlConnection conn,Guid item,Guid course,Guid student,decimal score,Guid actor,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("INSERT INTO course_grade_scores(item_id,course_id,student_id,score,updated_by,published_score) VALUES($1,$2,$3,$4,$5,$4) ON CONFLICT(item_id,student_id) DO UPDATE SET score=EXCLUDED.score,published_score=EXCLUDED.score,version=course_grade_scores.version+1,updated_by=EXCLUDED.updated_by,updated_at=now()",conn);
        cmd.Parameters.AddWithValue(item);cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(student);cmd.Parameters.AddWithValue(score);cmd.Parameters.AddWithValue(actor);await cmd.ExecuteNonQueryAsync(ct);
    }
    public static async Task ReleaseQuiz(NpgsqlConnection conn,Guid assignment,Guid snapshot,decimal score,Guid actor,CancellationToken ct)
    {
        Guid item,course,student;
        await using(var cmd=new NpgsqlCommand("SELECT i.id,i.course_id,s.student_user_id FROM course_grade_items i JOIN submission_revisions r ON r.id=$2 JOIN submissions s ON s.id=r.submission_id AND s.assignment_id=i.source_assignment_id WHERE i.source_assignment_id=$1 AND NOT s.is_practice AND r.id=(SELECT id FROM submission_revisions WHERE submission_id=s.id ORDER BY revision_no DESC LIMIT 1) FOR UPDATE OF i",conn)){
            cmd.Parameters.AddWithValue(assignment);cmd.Parameters.AddWithValue(snapshot);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return;item=r.GetGuid(0);course=r.GetGuid(1);student=r.GetGuid(2);
        }
        await SetScore(conn,item,course,student,score,actor,ct);
    }
}
