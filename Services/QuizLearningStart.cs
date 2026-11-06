using System.Text.Json;
using Npgsql;
namespace AiTeachingAssistant.Services;

public sealed partial class AssignmentWorkflow
{
    public async Task StartLearningQuiz(Guid id,Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        var a=await Load(conn,id,actor,true,ct);
        AssignmentPolicy.RequireWritable(a.CourseRole,a.Status,a.CourseStatus,a.OpensAt,a.DueAt,clock.GetUtcNow());
        if(!a.IsQuiz)Fail(400,"此項目不是線上測驗。");
        var gate=await LearningTime.Gate(conn,id,actor,Practice,ct);
        if(!gate.Met)Fail(409,"教材有效閱讀／觀看時間尚未達標，請先完成教材學習。");
        await using(var cmd=new NpgsqlCommand("INSERT INTO quiz_learning_starts(assignment_id,user_id,is_practice,started_at,evidence) VALUES($1,$2,$3,$4,$5::jsonb) ON CONFLICT DO NOTHING",conn))
        {Add(cmd,id);Add(cmd,actor);Add(cmd,Practice);Add(cmd,clock.GetUtcNow());Add(cmd,JsonSerializer.Serialize(gate.Requirements));var inserted=await cmd.ExecuteNonQueryAsync(ct);if(inserted>0)await Audit(conn,actor,id,"assignments","quiz.learning_started",ct);}
        await tx.CommitAsync(ct);
    }
}
