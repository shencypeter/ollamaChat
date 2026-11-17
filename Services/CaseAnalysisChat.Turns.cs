using AiTeachingAssistant.Models;
using Npgsql;

namespace AiTeachingAssistant.Services;

public sealed partial class CaseAnalysisChat
{
    private sealed record Turn(Guid Course,Guid Actor,string Stage,Guid Session,Guid Message,Guid Attempt,IReadOnlyList<OllamaMessage> Context);
    public async Task<CaseStageHistory> Send(Guid course,Guid actor,string stage,CaseChatInput input,CancellationToken ct)
    {
        stage=AnalysisStageCatalog.Get(Stage(stage)).Key;
        if(input.RequestId==Guid.Empty||input.RevisesMessageId==Guid.Empty||string.IsNullOrWhiteSpace(input.Content)||input.Content.Length>chatOptions.Value.MaxMessageCharacters)throw new CaseChatException(400,"請輸入有效內容及獨立的訊息識別碼，單則最多8,000字元。");
        var turn=await BeginTurn(course,actor,stage,input.RequestId,input.Content,false,ct,input.RevisesMessageId);
        if(turn is not null)await CompleteTurn(turn,ct);
        return await History(course,actor,stage,ct);
    }
    public async Task<CaseStageHistory> Retry(Guid course,Guid actor,string stage,Guid message,CancellationToken ct)
    {
        stage=AnalysisStageCatalog.Get(Stage(stage)).Key;
        var turn=await BeginTurn(course,actor,stage,message,null,true,ct);
        if(turn is not null)await CompleteTurn(turn,ct);
        return await History(course,actor,stage,ct);
    }
    private static async Task Lock(NpgsqlConnection conn,Guid course,Guid actor,string stage,bool practice,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,741026037))",conn);Add(cmd,$"{course}:{actor}:{stage}:{practice}");await cmd.ExecuteNonQueryAsync(ct);
    }
    private async Task<Turn?> BeginTurn(Guid course,Guid actor,string stage,Guid requestId,string? content,bool retry,CancellationToken ct,Guid? revisesMessageId=null)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        var access=await Access(conn,course,actor,true,true,ct);await Lock(conn,course,actor,stage,access.Practice,ct);
        Guid? active=null;
        await using(var cmd=new NpgsqlCommand("SELECT id FROM case_analysis_sessions WHERE course_id=$1 AND student_id=$2 AND stage=$3 AND is_practice=$4 AND status='active' FOR UPDATE",conn)){Add(cmd,course,actor,stage,access.Practice);active=await cmd.ExecuteScalarAsync(ct) as Guid?;}
        if(active.HasValue)
        {
            await using var expire=new NpgsqlCommand("UPDATE case_analysis_messages SET response_status='failed',failure_code='expired',lease_until=NULL WHERE session_id=$1 AND response_status='pending' AND lease_until<=$2",conn);Add(expire,active.Value,clock.GetUtcNow());await expire.ExecuteNonQueryAsync(ct);
        }
        Guid? foundSession=null;string? foundStatus=null,foundModel=null,foundVersion=null;
        await using(var cmd=new NpgsqlCommand("SELECT m.session_id,m.role,m.content,m.response_status,s.course_id,s.student_id,s.stage,s.is_practice,m.model_name,m.prompt_version,m.revises_message_id FROM case_analysis_messages m JOIN case_analysis_sessions s ON s.id=m.session_id WHERE m.id=$1",conn))
        {
            Add(cmd,requestId);await using var r=await cmd.ExecuteReaderAsync(ct);
            if(await r.ReadAsync(ct))
            {
                if(r.GetString(1)!="user"||r.GetGuid(4)!=course||r.GetGuid(5)!=actor||r.GetString(6)!=stage||r.GetBoolean(7)!=access.Practice)throw new CaseChatException(retry?404:409,"無法使用此訊息識別碼。");
                if(!retry&&(r.GetString(2)!=content||(r.IsDBNull(10)?(Guid?)null:r.GetGuid(10))!=revisesMessageId))throw new CaseChatException(409,"相同訊息識別碼不可更改內容或修改來源，請重新送出。");
                foundSession=r.GetGuid(0);content=r.GetString(2);foundStatus=r.GetString(3);foundModel=r.GetString(8);foundVersion=r.GetString(9);
            }
        }
        if(retry&&!foundSession.HasValue)throw new CaseChatException(404,"找不到可重試的本人訊息。");
        if(foundStatus=="completed"||foundStatus=="failed"&&!retry){await tx.CommitAsync(ct);return null;}
        if(foundStatus=="pending")throw new CaseChatException(409,"這則訊息仍在等待回覆，請稍後重新載入。");
        if(retry&&foundSession!=active)throw new CaseChatException(409,"已結束的對話不可重試，請在目前工作階段提問。");
        if(retry&&(foundModel!=modelOptions.Value.Model||foundVersion!=CaseChatPrompts.VersionFor(Stage(stage))))throw new CaseChatException(409,"模型或提示版本已變更，請開始新的對話。");
        if(!retry&&revisesMessageId.HasValue)
        {
            await using var parent=new NpgsqlCommand("SELECT m.response_status FROM case_analysis_messages m JOIN case_analysis_sessions s ON s.id=m.session_id WHERE m.id=$1 AND m.role='user' AND s.course_id=$2 AND s.student_id=$3 AND s.stage=$4 AND s.is_practice=$5",conn);
            Add(parent,revisesMessageId.Value,course,actor,stage,access.Practice);
            if(await parent.ExecuteScalarAsync(ct) is not string status)throw new CaseChatException(404,"找不到可修改的本人同階段作答。");
            else if(status!="completed")throw new CaseChatException(409,"請先完成原作答的回饋，失敗回覆可使用重試。");
        }
        if(!active.HasValue)
        {
            active=Guid.NewGuid();await using var create=new NpgsqlCommand("INSERT INTO case_analysis_sessions(id,course_id,student_id,stage,is_practice,started_at,last_activity_at) VALUES($1,$2,$3,$4,$5,$6,$6)",conn);Add(create,active.Value,course,actor,stage,access.Practice,clock.GetUtcNow());await create.ExecuteNonQueryAsync(ct);
        }
        await using(var pending=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM case_analysis_messages WHERE session_id=$1 AND response_status='pending')",conn)){Add(pending,active.Value);if(await pending.ExecuteScalarAsync(ct) is true)throw new CaseChatException(409,"本階段仍有訊息等待回覆，請勿同時送出。");}
        var attempt=Guid.NewGuid();var lease=clock.GetUtcNow().AddSeconds(modelOptions.Value.TimeoutSeconds+30);
        if(retry)
        {
            await using(var latest=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM case_analysis_messages newer JOIN case_analysis_messages original ON original.id=$1 WHERE newer.session_id=original.session_id AND newer.sequence_number>original.sequence_number)",conn)){Add(latest,requestId);if(await latest.ExecuteScalarAsync(ct) is true)throw new CaseChatException(409,"只能重試目前對話最後一則未完成的提問，或重新開始對話。");}
            await using var update=new NpgsqlCommand("UPDATE case_analysis_messages SET response_status='pending',attempt_token=$2,lease_until=$3,failure_code=NULL WHERE id=$1",conn);Add(update,requestId,attempt,lease);await update.ExecuteNonQueryAsync(ct);
        }
        else
        {
            var sequence=await NextSequence(conn,active.Value,ct);
            await using var insert=new NpgsqlCommand("INSERT INTO case_analysis_messages(id,session_id,role,content,sequence_number,response_status,model_name,prompt_version,attempt_token,lease_until,created_at,revises_message_id) VALUES($1,$2,'user',$3,$4,'pending',$5,$6,$7,$8,$9,$10)",conn);
            Add(insert,requestId,active.Value,content!,sequence,modelOptions.Value.Model,CaseChatPrompts.VersionFor(Stage(stage)),attempt,lease,clock.GetUtcNow());insert.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Uuid,Value=(object?)revisesMessageId??DBNull.Value});await insert.ExecuteNonQueryAsync(ct);
        }
        await using(var update=new NpgsqlCommand("UPDATE case_analysis_sessions SET last_activity_at=$2 WHERE id=$1",conn)){Add(update,active.Value,clock.GetUtcNow());await update.ExecuteNonQueryAsync(ct);}
        // Revisions and feedback remain in the database for review/diff/statistics.
        // Each formal round is assessed independently, including a retry.
        var messages=new List<OllamaMessage>{new("system",CaseChatPrompts.Get(Stage(stage))),new("user",content!)};
        await tx.CommitAsync(ct);
        return new(course,actor,stage,active.Value,requestId,attempt,messages);
    }
    private static async Task<int> NextSequence(NpgsqlConnection conn,Guid session,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("UPDATE case_analysis_sessions SET next_sequence=next_sequence+1 WHERE id=$1 RETURNING next_sequence-1",conn);Add(cmd,session);return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }
    private async Task CompleteTurn(Turn turn,CancellationToken ct)
    {
        try
        {
            CaseChatBudget.EnsureFits(turn.Context,chatOptions.Value,modelOptions.Value,modelOptions.Value.CaseFeedbackMaxTokens);
            var result=await provider.CaseFeedbackAsync(turn.Context,Stage(turn.Stage),ct);
            CaseAnnotationContract.EnsureComplete(result.Metrics.DoneReason);
            var feedback=CaseAnnotationContract.Parse(result.Content,turn.Context.Last(m=>m.Role=="user").Content);
            if(feedback.Feedback.Length>chatOptions.Value.MaxResponseCharacters)throw new CaseChatException(502,"模型回覆超出長度限制，原提問已保留，可重試。",new("output_size",$"回饋長度 {feedback.Feedback.Length} 字元，超過應用程式回覆上限 {chatOptions.Value.MaxResponseCharacters}。"));
            await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
            await Access(conn,turn.Course,turn.Actor,true,true,ct);
            await using(var session=new NpgsqlCommand("SELECT status FROM case_analysis_sessions WHERE id=$1 FOR UPDATE",conn)){Add(session,turn.Session);if(await session.ExecuteScalarAsync(ct) is not "active")throw new CaseChatException(409,"對話已結束，未保存逾時回覆。");}
            await using(var guard=new NpgsqlCommand("UPDATE case_analysis_messages SET response_status='completed',lease_until=NULL WHERE id=$1 AND response_status='pending' AND attempt_token=$2 AND lease_until>$3",conn)){Add(guard,turn.Message,turn.Attempt,clock.GetUtcNow());if(await guard.ExecuteNonQueryAsync(ct)!=1)throw new CaseChatException(409,"這次回覆已失效，請重新載入紀錄。");}
            var sequence=await NextSequence(conn,turn.Session,ct);
            await using(var insert=new NpgsqlCommand("INSERT INTO case_analysis_messages(id,session_id,role,content,sequence_number,response_status,in_reply_to,model_name,prompt_version,created_at,performance_metrics,annotations) VALUES($1,$2,'assistant',$3,$4,'completed',$5,$6,$7,$8,$9::jsonb,$10::jsonb)",conn)){Add(insert,Guid.NewGuid(),turn.Session,feedback.Feedback,sequence,turn.Message,modelOptions.Value.Model,CaseChatPrompts.VersionFor(Stage(turn.Stage)),clock.GetUtcNow(),System.Text.Json.JsonSerializer.Serialize(ChatPerformanceMetrics.Clean(result.Metrics),PerformanceJson),System.Text.Json.JsonSerializer.Serialize(feedback.Annotations,PerformanceJson));await insert.ExecuteNonQueryAsync(ct);}
            await using(var update=new NpgsqlCommand("UPDATE case_analysis_sessions SET last_activity_at=$2 WHERE id=$1",conn)){Add(update,turn.Session,clock.GetUtcNow());await update.ExecuteNonQueryAsync(ct);}
            await tx.CommitAsync(ct);
        }
        catch(Exception error)
        {
            // Never persist the upstream error body/exception; only bounded application codes.
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var fail=source.CreateCommand("UPDATE case_analysis_messages SET response_status='failed',failure_code=$3,lease_until=NULL WHERE id=$1 AND attempt_token=$2 AND response_status='pending'");
            Add(fail,turn.Message,turn.Attempt,error is CaseChatException {StatusCode:413}?"context_limit":error is CaseChatException {StatusCode:403 or 404 or 409}?"access_changed":"provider_failed");
            await fail.ExecuteNonQueryAsync(cleanup.Token);
            if(error is CaseChatException)throw;
            throw new CaseChatException(502,"AI暫時無法回覆，原提問已保留；可重新載入後重試。");
        }
    }
    public async Task<CaseStageHistory> Restart(Guid course,Guid actor,string stage,CancellationToken ct)
    {
        stage=AnalysisStageCatalog.Get(Stage(stage)).Key;
        await using(var conn=await source.OpenConnectionAsync(ct))
        {
            await using var tx=await conn.BeginTransactionAsync(ct);var access=await Access(conn,course,actor,true,true,ct);await Lock(conn,course,actor,stage,access.Practice,ct);
            await using(var pending=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM case_analysis_sessions s JOIN case_analysis_messages m ON m.session_id=s.id WHERE s.course_id=$1 AND s.student_id=$2 AND s.stage=$3 AND s.is_practice=$4 AND s.status='active' AND m.response_status='pending' AND m.lease_until>$5)",conn)){Add(pending,course,actor,stage,access.Practice,clock.GetUtcNow());if(await pending.ExecuteScalarAsync(ct) is true)throw new CaseChatException(409,"請等候目前回覆完成後再重新開始。");}
            await using(var expire=new NpgsqlCommand("UPDATE case_analysis_messages m SET response_status='failed',failure_code='expired',lease_until=NULL FROM case_analysis_sessions s WHERE m.session_id=s.id AND s.course_id=$1 AND s.student_id=$2 AND s.stage=$3 AND s.is_practice=$4 AND s.status='active' AND m.response_status='pending' AND m.lease_until<=$5",conn)){Add(expire,course,actor,stage,access.Practice,clock.GetUtcNow());await expire.ExecuteNonQueryAsync(ct);}
            await using(var close=new NpgsqlCommand("UPDATE case_analysis_sessions SET status='closed',completed_at=$5 WHERE course_id=$1 AND student_id=$2 AND stage=$3 AND is_practice=$4 AND status='active'",conn)){Add(close,course,actor,stage,access.Practice,clock.GetUtcNow());await close.ExecuteNonQueryAsync(ct);}
            await tx.CommitAsync(ct);
        }
        return await History(course,actor,stage,ct);
    }
}
