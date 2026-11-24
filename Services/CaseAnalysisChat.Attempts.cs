using AiTeachingAssistant.Models;
using Npgsql;
using NpgsqlTypes;

namespace AiTeachingAssistant.Services;

public sealed partial class CaseAnalysisChat
{
    public async Task<CaseStageHistory> SetAttemptTrashed(Guid course,Guid actor,string stage,Guid message,bool trashed,CancellationToken ct,CaseHistoryQuery? page=null)
    {
        stage=AnalysisStageCatalog.Get(Stage(stage)).Key;
        await using(var conn=await source.OpenConnectionAsync(ct))
        await using(var tx=await conn.BeginTransactionAsync(ct))
        {
            var access=await Access(conn,course,actor,true,true,ct);
            await Lock(conn,course,actor,stage,access.Practice,ct);
            Guid session;
            await using(var find=new NpgsqlCommand("""
                SELECT s.id FROM case_analysis_messages m JOIN case_analysis_sessions s ON s.id=m.session_id
                WHERE m.id=$1 AND m.role='user' AND s.course_id=$2 AND s.student_id=$3 AND s.stage=$4 AND s.is_practice=$5
                """,conn))
            {
                Add(find,message,course,actor,stage,access.Practice);
                if(await find.ExecuteScalarAsync(ct) is not Guid found)throw new CaseChatException(404,"找不到可整理的本人作答。");
                session=found;
            }
            // Same row-lock order as completion: session first, then user message.
            await using(var gate=new NpgsqlCommand("SELECT id FROM case_analysis_sessions WHERE id=$1 FOR UPDATE",conn)){Add(gate,session);await gate.ExecuteScalarAsync(ct);}
            string status;DateTimeOffset? lease,previous;
            await using(var read=new NpgsqlCommand("SELECT response_status,lease_until,trashed_at FROM case_analysis_messages WHERE id=$1 FOR UPDATE",conn))
            {
                Add(read,message);await using var row=await read.ExecuteReaderAsync(ct);await row.ReadAsync(ct);
                status=row.GetString(0);lease=row.IsDBNull(1)?null:row.GetFieldValue<DateTimeOffset>(1);previous=row.IsDBNull(2)?null:row.GetFieldValue<DateTimeOffset>(2);
            }
            // SubmitFormal takes the same stage lock, so submission and trash cannot both commit.
            if(trashed)
            {
                await using var submitted=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM case_formal_submissions WHERE analysis_message_id=$1 AND course_id=$2 AND student_id=$3)",conn);
                Add(submitted,message,course,actor);
                if(await submitted.ExecuteScalarAsync(ct) is true)throw new CaseChatException(409,"此作答已正式提交，不能移至垃圾桶；退回的正式版本也會保留。");
            }
            var now=clock.GetUtcNow();
            if(status=="pending")
            {
                if(!lease.HasValue||lease>now)throw new CaseChatException(409,"本次作答仍在等待 AI 回覆，請完成後再移至垃圾桶。");
                await using var expired=new NpgsqlCommand("UPDATE case_analysis_messages SET response_status='failed',failure_code='expired',lease_until=NULL WHERE id=$1",conn);Add(expired,message);await expired.ExecuteNonQueryAsync(ct);
            }
            if(trashed!=previous.HasValue)
            {
                await using(var update=new NpgsqlCommand("UPDATE case_analysis_messages SET trashed_at=$2 WHERE id=$1",conn))
                {
                    Add(update,message);update.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=NpgsqlDbType.TimestampTz,Value=trashed?(object)now:DBNull.Value});await update.ExecuteNonQueryAsync(ct);
                }
                await using var audit=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata) VALUES($1,'courses',$2,$3,jsonb_build_object('messageId',$4::text,'sessionId',$5::text,'stage',$6::text))",conn);
                Add(audit,actor,course,trashed?"case_chat.attempt_trashed":"case_chat.attempt_restored",message.ToString(),session.ToString(),stage);await audit.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        return await History(course,actor,stage,ct,page);
    }
}
