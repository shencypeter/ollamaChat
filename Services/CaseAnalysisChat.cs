using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Text.Json;

namespace AiTeachingAssistant.Services;

public sealed partial class CaseAnalysisChat(NpgsqlDataSource source,IOllamaClient provider,IOptions<OllamaOptions> modelOptions,IOptions<CaseChatOptions> chatOptions,TimeProvider clock,ICaseChatModels? models=null)
{
    private static readonly JsonSerializerOptions PerformanceJson=new(JsonSerializerDefaults.Web);
    private static void Add(NpgsqlCommand cmd,params object[] values){foreach(var value in values)cmd.Parameters.AddWithValue(value);}
    private static AnalysisStage Stage(string key)
    {
        if(!AnalysisStageCatalog.TryParse(key,out var stage)||stage==AnalysisStage.GeneralChat)throw new CaseChatException(400,"請選擇四個個案分析階段之一。");
        return stage;
    }
    public async Task<CaseChatAccess> Access(Guid course,Guid actor,bool write,CancellationToken ct,bool requireEnabled=true)
    {
        await using var conn=await source.OpenConnectionAsync(ct);return await Access(conn,course,actor,write,requireEnabled,ct);
    }
    private static async Task<CaseChatAccess> Access(NpgsqlConnection conn,Guid course,Guid actor,bool write,bool enabled,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            SELECT m.course_role,c.status,c.case_analysis_enabled,m.ta_restrictions
            FROM courses c JOIN course_members m ON m.course_id=c.id JOIN app_users u ON u.id=m.user_id
            WHERE c.id=$1 AND m.user_id=$2 AND m.status='active' AND u.status='active'
            FOR SHARE OF c,m
            """,conn);Add(cmd,course,actor);
        await using var r=await cmd.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))throw new CaseChatException(404,"找不到可存取的課程。");
        var result=new CaseChatAccess(r.GetString(0),r.GetString(1),r.GetBoolean(2),r.GetFieldValue<string[]>(3));
        if(result.Status=="draft"&&result.Role=="student"||enabled&&!result.Enabled)throw new CaseChatException(404,"本課程尚未開啟個案分析。");
        if(write&&result.Status!="active")throw new CaseChatException(409,"目前課程未開放聊天；既有紀錄仍可查閱。");
        return result;
    }
    public async Task SetEnabled(Guid course,Guid actor,bool enabled,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        // Acquire the exclusive course lock first. Upgrading two independent SHARE
        // locks after Access would deadlock concurrent settings requests.
        await using(var gate=new NpgsqlCommand("SELECT id FROM courses WHERE id=$1 AND owner_user_id=$2 FOR UPDATE",conn))
        {Add(gate,course,actor);if(await gate.ExecuteScalarAsync(ct) is not Guid)throw new CaseChatException(403,"只有本課程擁有者可設定個案分析功能。");}
        var access=await Access(conn,course,actor,false,false,ct);
        if(access.Role!="teacher"||access.Status=="archived")throw new CaseChatException(403,"只有本課程教師可設定個案分析功能。");
        await using(var cmd=new NpgsqlCommand("UPDATE courses SET case_analysis_enabled=$2 WHERE id=$1",conn)){Add(cmd,course,enabled);await cmd.ExecuteNonQueryAsync(ct);}
        await using(var cmd=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata) VALUES($1,'courses',$2,'course.case_analysis_setting',jsonb_build_object('enabled',$3::boolean))",conn)){Add(cmd,actor,course,enabled);await cmd.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct);
    }
    public async Task<CaseStageHistory> History(Guid course,Guid actor,string stage,CancellationToken ct,CaseHistoryQuery? page=null)
    {
        stage=AnalysisStageCatalog.Get(Stage(stage)).Key;var access=await Access(course,actor,false,ct);
        return page?.Paged==true?await ReadHistoryPage(course,actor,stage,access.Practice,page,ct):await ReadHistory(course,actor,stage,access.Practice,ct);
    }
    public async Task<CaseStageHistory> ReviewHistory(Guid course,Guid actor,Guid student,string stage,CancellationToken ct)
    {
        stage=AnalysisStageCatalog.Get(Stage(stage)).Key;if(!(await Access(course,actor,false,ct)).CanReview)throw new CaseChatException(403,"你沒有本課個案分析紀錄的查閱權限。");
        throw new CaseChatException(403,"學生 AI 分析草稿僅供本人查閱；請改由正式提交紀錄審閱作業。");
    }
    private async Task<CaseStageHistory> ReadHistory(Guid course,Guid student,string stage,bool practice,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        var sessions=new List<(Guid Id,string Status,DateTimeOffset Last)>();
        await using(var cmd=new NpgsqlCommand("SELECT id,status,last_activity_at FROM case_analysis_sessions WHERE course_id=$1 AND student_id=$2 AND stage=$3 AND is_practice=$4 ORDER BY started_at DESC,id",conn))
        {Add(cmd,course,student,stage,practice);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))sessions.Add((r.GetGuid(0),r.GetString(1),r.GetFieldValue<DateTimeOffset>(2)));}
        var messages=sessions.ToDictionary(s=>s.Id,_=>new List<CaseChatMessage>());
        if(sessions.Count>0)
        {
            await using var cmd=new NpgsqlCommand("SELECT m.session_id,m.id,m.role,m.content,m.sequence_number,CASE WHEN m.response_status='pending' AND m.lease_until<=$2 THEN 'failed' ELSE m.response_status END,m.in_reply_to,m.model_name,m.prompt_version,m.created_at,m.performance_metrics,m.revises_message_id,m.annotations,CASE WHEN m.role='user' THEN m.trashed_at ELSE parent.trashed_at END,m.rule_version,m.rule_snapshot FROM case_analysis_messages m LEFT JOIN case_analysis_messages parent ON parent.id=m.in_reply_to AND parent.role='user' WHERE m.session_id=ANY($1) ORDER BY m.sequence_number",conn);Add(cmd,sessions.Select(s=>s.Id).ToArray(),clock.GetUtcNow());
            await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))messages[r.GetGuid(0)].Add(new(r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetInt32(4),r.GetString(5),r.IsDBNull(6)?null:r.GetGuid(6),r.GetString(7),r.GetString(8),r.GetFieldValue<DateTimeOffset>(9),r.IsDBNull(10)?null:JsonSerializer.Deserialize<RequestMetrics>(r.GetString(10),PerformanceJson),r.IsDBNull(11)?null:r.GetGuid(11),r.IsDBNull(12)?null:JsonSerializer.Deserialize<CaseTextAnnotation[]>(r.GetString(12),PerformanceJson),r.IsDBNull(13)?null:r.GetFieldValue<DateTimeOffset>(13),r.IsDBNull(14)?null:r.GetInt32(14),r.IsDBNull(15)?CaseClassificationRules.Defaults:JsonSerializer.Deserialize<CaseClassificationRuleDefinition[]>(r.GetString(15),PerformanceJson)));
        }
        await tx.CommitAsync(ct);
        return new(sessions.Select(s=>new CaseChatSession(s.Id,stage,s.Status,messages[s.Id],Statistics(messages[s.Id],s.Last))).ToArray(),sessions.Where(s=>s.Status=="active").Select(s=>(Guid?)s.Id).FirstOrDefault());
    }
    private static CaseChatStatistics Statistics(IReadOnlyList<CaseChatMessage> messages,DateTimeOffset last)
        =>new(messages.Count(m=>m.Role=="user"),messages.Count(m=>m.Role=="assistant"),messages.Count(m=>m.Role=="assistant"&&messages.Any(u=>u.Id==m.ReplyTo&&u.Role=="user"&&u.Status=="completed")),last,messages.Count(m=>m.RevisesMessageId.HasValue));

    public async Task<IReadOnlyList<CaseChatStudent>> Records(Guid course,Guid actor,CancellationToken ct)
    {
        if(!(await Access(course,actor,false,ct)).CanReview)throw new CaseChatException(403,"你沒有本課個案分析紀錄的查閱權限。");
        var people=new List<(Guid Id,string Name,string? Number)>();
        await using(var cmd=source.CreateCommand("""
            SELECT u.id,u.display_name,u.student_number FROM app_users u
            WHERE EXISTS(SELECT 1 FROM course_members m WHERE m.course_id=$1 AND m.user_id=u.id AND m.course_role='student' AND m.status='active')
               OR EXISTS(SELECT 1 FROM case_analysis_sessions s WHERE s.course_id=$1 AND s.student_id=u.id AND NOT s.is_practice)
            ORDER BY u.student_number NULLS LAST,u.display_name,u.id
            """)){Add(cmd,course);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))people.Add((r.GetGuid(0),r.GetString(1),r.IsDBNull(2)?null:r.GetString(2)));}
        var counts=new Dictionary<(Guid,string),CaseChatStatistics>();
        await using(var cmd=source.CreateCommand("""
            SELECT s.student_id,s.stage,count(m.id) FILTER(WHERE m.role='user')::int,
              count(m.id) FILTER(WHERE m.role='assistant')::int,
              count(m.id) FILTER(WHERE m.role='assistant' AND EXISTS(SELECT 1 FROM case_analysis_messages u WHERE u.id=m.in_reply_to AND u.role='user' AND u.response_status='completed'))::int,
              max(s.last_activity_at),count(m.id) FILTER(WHERE m.revises_message_id IS NOT NULL)::int
            FROM case_analysis_sessions s LEFT JOIN case_analysis_messages m ON m.session_id=s.id
            WHERE s.course_id=$1 AND NOT s.is_practice GROUP BY s.student_id,s.stage
            """)){Add(cmd,course);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))counts[(r.GetGuid(0),r.GetString(1))]=new(r.GetInt32(2),r.GetInt32(3),r.GetInt32(4),r.GetFieldValue<DateTimeOffset>(5),r.GetInt32(6));}
        return people.Select(p=>{
            var stages=AnalysisStageCatalog.All.Where(s=>s.Stage!=AnalysisStage.GeneralChat).Select(s=>new CaseChatStageStatistics(s.Key,counts.GetValueOrDefault((p.Id,s.Key))??new(0,0,0,null))).ToArray();
            return new CaseChatStudent(p.Id,p.Name,p.Number,stages,new(stages.Sum(s=>s.Statistics.StudentMessages),stages.Sum(s=>s.Statistics.AssistantReplies),stages.Sum(s=>s.Statistics.RoundTrips),stages.Max(s=>s.Statistics.LastActivityAt),stages.Sum(s=>s.Statistics.RevisionCount)));
        }).ToArray();
    }
}
