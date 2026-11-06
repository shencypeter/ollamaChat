using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using Npgsql;
using NpgsqlTypes;
namespace AiTeachingAssistant.Services;

public sealed partial class AssignmentWorkflow
{
    public async Task<IReadOnlyList<FormativeFeedback>> Feedback(Guid assignment,Guid question,Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);var a=await Load(conn,assignment,actor,false,ct);
        if(a.CourseRole!="student")Fail(403,"只有學生本人可以讀取私人 AI 練習回饋。");
        if(!a.Questions.Any(q=>q.Id==question))Fail(404,"找不到本作業的題目。");
        return await ReadFeedback(conn,assignment,question,actor,null,ct);
    }
    private async Task<IReadOnlyList<FormativeFeedback>> ReadFeedback(NpgsqlConnection conn,Guid assignment,Guid question,Guid actor,Guid? run,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            SELECT e.id,e.answer_revision_id,r.revision_no,e.answer_sha256,e.status,e.created_at,e.completed_at,
              e.model_name,e.model_profile,e.prompt_version,e.findings::text,
              e.answer_revision_id IS DISTINCT FROM sa.current_revision_id,e.error_message
            FROM evaluation_runs e JOIN answer_revisions r ON r.id=e.answer_revision_id
            JOIN submission_answers sa ON sa.id=r.answer_id JOIN submissions s ON s.id=sa.submission_id
            WHERE e.purpose='formative' AND e.requested_by_user_id=$3 AND s.student_user_id=$3
              AND sa.assignment_id=$1 AND sa.question_id=$2 AND ($4::uuid IS NULL OR e.id=$4) AND s.is_practice=$5
            ORDER BY e.created_at DESC,e.id DESC LIMIT 20
            """,conn);
        Add(cmd,assignment);Add(cmd,question);Add(cmd,actor);OptionalGuid(cmd,run);Add(cmd,Practice);
        await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<FormativeFeedback>();
        while(await r.ReadAsync(ct))result.Add(new(r.GetGuid(0),question,r.GetGuid(1),r.GetInt32(2),r.GetString(3),r.GetString(4),
            r.GetFieldValue<DateTimeOffset>(5),ReadDate(r,6),r.GetString(7),r.GetString(8),r.GetString(9),
            r.IsDBNull(10)?null:JsonSerializer.Deserialize<AssessmentResult>(r.GetString(10),AssessmentContract.JsonOptions),r.GetBoolean(11),r.IsDBNull(12)?null:r.GetString(12)));
        return result;
    }
    public async Task<FormativeFeedback> AskFeedback(Guid assignment,Guid question,Guid actor,RequestFormativeFeedback request,
        IOllamaClient model,OllamaOptions options,CancellationToken ct)
    {
        var run=Guid.NewGuid();IReadOnlyList<OllamaMessage> messages;bool caseAvailable;
        // Only hold the existing assignment lock while binding and recording the exact revision.
        await using(var conn=await source.OpenConnectionAsync(ct))
        await using(var tx=await conn.BeginTransactionAsync(ct))
        {
            var a=await Load(conn,assignment,actor,true,ct);
            AssignmentPolicy.RequireWritable(a.CourseRole,a.Status,a.CourseStatus,a.OpensAt,a.DueAt,clock.GetUtcNow());
            var q=a.Questions.SingleOrDefault(q=>q.Id==question)??throw new AssignmentWorkflowException(404,"找不到本作業的題目。");
            var own=await LoadSubmission(conn,a,actor,ct);var answer=own.Answers.Single(v=>v.QuestionId==question);
            FormativePrompt.RequireDraft(own,request,answer);
            messages=FormativePrompt.Build(a,q,request.AnswerRevisionId,answer.Text);caseAvailable=!string.IsNullOrWhiteSpace(a.Instructions);
            await using var insert=new NpgsqlCommand("""
                INSERT INTO evaluation_runs(id,answer_revision_id,model_name,prompt_version,model_parameters,purpose,
                  requested_by_user_id,model_profile,answer_sha256)
                VALUES($1,$2,$3,$4,$5,'formative',$6,$7,$8)
                """,conn);
            Add(insert,run);Add(insert,request.AnswerRevisionId);Add(insert,options.Model);Add(insert,FormativePrompt.Version);
            insert.Parameters.AddWithValue(NpgsqlDbType.Jsonb,JsonSerializer.Serialize(new{sampling=options.AssessmentSampling,
                options.MergeAssessmentSystemMessages,options.RepeatAssessmentRequirements,
                contextSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messages))))}));
            Add(insert,actor);Add(insert,options.Profile);Add(insert,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(answer.Text))));
            await insert.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);
        }
        AssessmentResult? assessment=null;string? failure=null;
        try
        {
            var response=await model.AssessAsync(messages,false,ct);
            assessment=AssessmentResultParser.Parse(response.Content,false,caseAvailable);
        }
        catch(OperationCanceledException){failure="AI 回饋逾時或已中止；已儲存草稿不受影響，可重試或直接提交給教師／助教。";}
        catch(Exception e) when(e is HttpRequestException or InvalidOperationException)
        {failure="AI 暫時無法提供有效回饋；已儲存草稿不受影響，可重試或直接提交給教師／助教。";}
        // Complete independently of a disconnected browser. Never store raw model responses or thinking.
        using var completion=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using(var conn=await source.OpenConnectionAsync(completion.Token))
        {
            await using var cmd=new NpgsqlCommand("UPDATE evaluation_runs SET status=$2,score=$3,findings=$4,error_message=$5,completed_at=now() WHERE id=$1 AND status='pending'",conn);
            Add(cmd,run);Add(cmd,assessment is null?"failed":"completed");
            cmd.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=NpgsqlDbType.Numeric,Value=assessment is null?DBNull.Value:assessment.Evaluations[0].Score});
            cmd.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=NpgsqlDbType.Jsonb,Value=assessment is null?DBNull.Value:JsonSerializer.Serialize(assessment,AssessmentContract.JsonOptions)});
            cmd.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=NpgsqlDbType.Text,Value=(object?)failure??DBNull.Value});
            await cmd.ExecuteNonQueryAsync(completion.Token);
            return (await ReadFeedback(conn,assignment,question,actor,run,completion.Token)).Single();
        }
    }
}
