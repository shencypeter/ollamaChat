using AiTeachingAssistant.Models;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;

namespace AiTeachingAssistant.Services;

public sealed partial class CaseAnalysisChat
{
    private static bool CanReadFormal(CaseChatAccess access)=>access.Role=="teacher"||access.Role=="ta"&&!access.Restrictions.Contains("grading");
    public async Task<CaseFormalEnvelope> FormalSubmissions(Guid course,Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        var access=await Access(conn,course,actor,false,true,ct);
        if(access.Practice&&!CanReadFormal(access))throw new CaseChatException(403,"你沒有本課正式作業的審閱權限。");
        var result=new List<CaseFormalSubmission>();
        await using(var cmd=new NpgsqlCommand("""
            SELECT f.id,f.student_id,u.display_name,f.stage,f.version,f.status,f.submitted_at,f.returned_at,f.return_reason,
                   f.assignment_id,f.analysis_message_id,f.answer_text,f.feedback_text,f.annotations,f.model_name,f.prompt_version,f.rule_version,f.rule_snapshot
            FROM case_formal_submissions f JOIN app_users u ON u.id=f.student_id
            WHERE f.course_id=$1 AND ($2 OR f.student_id=$3)
            ORDER BY f.submitted_at DESC,f.version DESC,f.id
            """,conn))
        {
            Add(cmd,course,CanReadFormal(access),actor);await using var row=await cmd.ExecuteReaderAsync(ct);
            while(await row.ReadAsync(ct))result.Add(new(row.GetGuid(0),course,row.GetGuid(1),row.GetString(2),row.GetString(3),row.GetInt32(4),row.GetString(5),row.GetFieldValue<DateTimeOffset>(6),row.IsDBNull(7)?null:row.GetFieldValue<DateTimeOffset>(7),row.IsDBNull(8)?null:row.GetString(8),row.IsDBNull(9)?null:row.GetGuid(9),row.GetGuid(10),row.GetString(11),row.GetString(12),JsonSerializer.Deserialize<CaseTextAnnotation[]>(row.GetString(13),PerformanceJson)??[],row.GetString(14),row.GetString(15),row.IsDBNull(16)?null:row.GetInt32(16),row.IsDBNull(17)?null:JsonSerializer.Deserialize<JsonElement>(row.GetString(17))));
        }
        var assignments=new List<CaseFormalAssignment>();
        await using(var cmd=new NpgsqlCommand("SELECT id,title FROM assignments WHERE course_id=$1 AND submission_mode='individual' AND status='published' AND (opens_at IS NULL OR opens_at<=$2) AND (due_at IS NULL OR due_at>=$2) ORDER BY title,id",conn))
        {Add(cmd,course,clock.GetUtcNow());await using var row=await cmd.ExecuteReaderAsync(ct);while(await row.ReadAsync(ct))assignments.Add(new(row.GetGuid(0),row.GetString(1)));}
        await tx.CommitAsync(ct);return new(result,assignments);
    }

    public async Task<CaseFormalEnvelope> SubmitFormal(Guid course,Guid actor,CaseFormalSubmissionInput input,CancellationToken ct)
    {
        if(input.RequestId==Guid.Empty||input.MessageId==Guid.Empty||input.AssignmentId==Guid.Empty||input.ExpectedPreviousId==Guid.Empty)throw new CaseChatException(400,"正式提交識別碼無效。");
        await using(var conn=await source.OpenConnectionAsync(ct))await using(var tx=await conn.BeginTransactionAsync(ct))
        {
            var access=await Access(conn,course,actor,true,true,ct);
            if(access.Role!="student")throw new CaseChatException(403,"教師教學練習不屬於學生正式作業。");
            string stage;Guid session;
            await using(var find=new NpgsqlCommand("SELECT s.stage,s.id FROM case_analysis_messages m JOIN case_analysis_sessions s ON s.id=m.session_id WHERE m.id=$1 AND m.role='user' AND s.course_id=$2 AND s.student_id=$3 AND NOT s.is_practice",conn))
            {Add(find,input.MessageId,course,actor);await using var row=await find.ExecuteReaderAsync(ct);if(!await row.ReadAsync(ct))throw new CaseChatException(404,"找不到可正式提交的本人作答。");stage=row.GetString(0);session=row.GetGuid(1);}
            await using(var requestLock=new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,741026041))",conn)){Add(requestLock,"case-formal:"+input.RequestId);await requestLock.ExecuteNonQueryAsync(ct);}
            await Lock(conn,course,actor,stage,false,ct);
            // Request ID replays stay bound to their original owner, course and source.
            await using(var replay=new NpgsqlCommand("SELECT course_id,student_id,analysis_message_id,assignment_id FROM case_formal_submissions WHERE id=$1",conn))
            {
                Add(replay,input.RequestId);await using var row=await replay.ExecuteReaderAsync(ct);
                if(await row.ReadAsync(ct))
                {
                    if(row.GetGuid(0)!=course||row.GetGuid(1)!=actor||row.GetGuid(2)!=input.MessageId||(row.IsDBNull(3)?null:(Guid?)row.GetGuid(3))!=input.AssignmentId)throw new CaseChatException(409,"此正式提交識別碼已用於其他作答。");
                    await row.DisposeAsync();await tx.CommitAsync(ct);
                    // Release this pooled connection before reading the response envelope.
                    await conn.DisposeAsync();return await FormalSubmissions(course,actor,ct);
                }
            }
            Guid? previous=null;int version=1;string? previousStatus=null;
            await using(var read=new NpgsqlCommand("SELECT id,version,status FROM case_formal_submissions WHERE course_id=$1 AND student_id=$2 AND stage=$3 ORDER BY version DESC LIMIT 1 FOR UPDATE",conn))
            {Add(read,course,actor,stage);await using var row=await read.ExecuteReaderAsync(ct);if(await row.ReadAsync(ct)){previous=row.GetGuid(0);version=row.GetInt32(1)+1;previousStatus=row.GetString(2);}}
            if(previous!=input.ExpectedPreviousId)throw new CaseChatException(409,"正式提交版本已變更，請重新載入後確認。");
            if(previousStatus=="submitted")throw new CaseChatException(409,"本階段已正式提交；教師退回後才能重新提交。");
            if(input.AssignmentId is {} assignment)
            {
                await using var gate=new NpgsqlCommand("SELECT id FROM assignments WHERE id=$1 AND course_id=$2 AND submission_mode='individual' AND status='published' AND (opens_at IS NULL OR opens_at<=$3) AND (due_at IS NULL OR due_at>=$3) FOR SHARE",conn);Add(gate,assignment,course,clock.GetUtcNow());
                if(await gate.ExecuteScalarAsync(ct) is not Guid)throw new CaseChatException(409,"所選作業不屬於本課、尚未開放或已截止。");
            }
            await using(var gate=new NpgsqlCommand("SELECT id FROM case_analysis_sessions WHERE id=$1 FOR UPDATE",conn)){Add(gate,session);await gate.ExecuteScalarAsync(ct);}
            await using(var save=new NpgsqlCommand("""
                INSERT INTO case_formal_submissions(id,course_id,student_id,stage,version,assignment_id,analysis_message_id,answer_text,feedback_text,annotations,model_name,prompt_version,submitted_at,rule_version,rule_snapshot)
                SELECT $1,$2,$3,$4,$5,$6,m.id,m.content,a.content,COALESCE(a.annotations,'[]'::jsonb),a.model_name,a.prompt_version,$7,
                       COALESCE(a.rule_version,0),COALESCE(a.rule_snapshot,$10::jsonb)
                FROM case_analysis_messages m JOIN case_analysis_messages a ON a.in_reply_to=m.id AND a.role='assistant'
                WHERE m.id=$8 AND m.session_id=$9 AND m.role='user' AND m.response_status='completed' AND m.trashed_at IS NULL
                """,conn))
            {
                Add(save,input.RequestId,course,actor,stage,version);save.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=NpgsqlDbType.Uuid,Value=(object?)input.AssignmentId??DBNull.Value});Add(save,clock.GetUtcNow(),input.MessageId,session,JsonSerializer.Serialize(CaseClassificationRules.Defaults,PerformanceJson));
                if(await save.ExecuteNonQueryAsync(ct)!=1)throw new CaseChatException(409,"請選擇已完成 AI 分析且未移至垃圾桶的作答。");
            }
            await FormalAudit(conn,course,actor,input.RequestId,"case_formal.submitted",version,ct);await tx.CommitAsync(ct);
        }
        return await FormalSubmissions(course,actor,ct);
    }

    public async Task<CaseFormalEnvelope> ReturnFormal(Guid course,Guid actor,Guid id,string reason,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(reason)||reason.Length>2000)throw new CaseChatException(400,"請填寫 1 至 2,000 字元的退回意見。");
        await using(var conn=await source.OpenConnectionAsync(ct))await using(var tx=await conn.BeginTransactionAsync(ct))
        {
            var access=await Access(conn,course,actor,true,true,ct);if(!CanReadFormal(access))throw new CaseChatException(403,"你沒有本課正式作業的退回權限。");
            Guid student;string stage;
            await using(var find=new NpgsqlCommand("SELECT student_id,stage FROM case_formal_submissions WHERE id=$1 AND course_id=$2",conn))
            {Add(find,id,course);await using var row=await find.ExecuteReaderAsync(ct);if(!await row.ReadAsync(ct))throw new CaseChatException(404,"找不到本課正式提交。");student=row.GetGuid(0);stage=row.GetString(1);}
            await Lock(conn,course,student,stage,false,ct);
            int version;string status;string? savedReason;
            await using(var read=new NpgsqlCommand("SELECT id,version,status,return_reason FROM case_formal_submissions WHERE course_id=$1 AND student_id=$2 AND stage=$3 ORDER BY version DESC LIMIT 1 FOR UPDATE",conn))
            {
                Add(read,course,student,stage);await using var row=await read.ExecuteReaderAsync(ct);await row.ReadAsync(ct);
                if(row.GetGuid(0)!=id)throw new CaseChatException(409,"只能退回最新正式提交版本。");version=row.GetInt32(1);status=row.GetString(2);savedReason=row.IsDBNull(3)?null:row.GetString(3);
            }
            if(status=="returned")
            {if(savedReason!=reason.Trim())throw new CaseChatException(409,"此版本已退回，既有退回意見不可覆寫。");}
            else
            {
                await using(var update=new NpgsqlCommand("UPDATE case_formal_submissions SET status='returned',returned_at=$2,returned_by=$3,return_reason=$4 WHERE id=$1",conn)){Add(update,id,clock.GetUtcNow(),actor,reason.Trim());await update.ExecuteNonQueryAsync(ct);}
                await FormalAudit(conn,course,actor,id,"case_formal.returned",version,ct);
            }
            await tx.CommitAsync(ct);
        }
        return await FormalSubmissions(course,actor,ct);
    }
    private static async Task FormalAudit(NpgsqlConnection conn,Guid course,Guid actor,Guid id,string action,int version,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata) VALUES($1,'courses',$2,$3,jsonb_build_object('submissionId',$4::text,'version',$5::int))",conn);Add(cmd,actor,course,action,id.ToString(),version);await cmd.ExecuteNonQueryAsync(ct);
    }
}
