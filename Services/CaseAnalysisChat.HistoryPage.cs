using AiTeachingAssistant.Models;
using Npgsql;
using System.Text.Json;

namespace AiTeachingAssistant.Services;

public sealed partial class CaseAnalysisChat
{
    // Rank before filtering so model/trash views retain the same global #N.
    // Only authenticated owner/course/stage/practice rows enter either query.
    private const string HistoryRanks="""
        WITH ranked AS (
          SELECT m.id,m.session_id,m.trashed_at,
            row_number() OVER(ORDER BY m.created_at,s.started_at,s.id DESC,m.sequence_number,m.id)::int AS n,
            COALESCE('model:'||COALESCE(NULLIF(btrim(a.model_name),''),NULLIF(btrim(m.model_name),'')),'unknown') AS model_key,
            COALESCE(NULLIF(btrim(a.model_name),''),NULLIF(btrim(m.model_name),'')) AS model_name
          FROM case_analysis_sessions s JOIN case_analysis_messages m ON m.session_id=s.id AND m.role='user'
          LEFT JOIN case_analysis_messages a ON a.in_reply_to=m.id AND a.session_id=s.id AND a.role='assistant'
          WHERE s.course_id=$1 AND s.student_id=$2 AND s.stage=$3 AND s.is_practice=$4
        )
        """;

    private async Task<CaseStageHistory> ReadHistoryPage(Guid course,Guid actor,string stage,bool practice,CaseHistoryQuery page,CancellationToken ct)
    {
        if(page.Before is <=0||page.Target==Guid.Empty||page.Target.HasValue&&page.Before.HasValue||page.Model?.Length>512||page.Model is {Length:>0} filter&&(filter!="unknown"&&!filter.StartsWith("model:",StringComparison.Ordinal)||filter.Any(char.IsControl)))
            throw new CaseChatException(400,"歷程分頁參數無效，請重新載入。");
        var modelKey=page.Model??"";
        await using var conn=await source.OpenConnectionAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        var models=new List<CaseHistoryModel>();
        await using(var cmd=new NpgsqlCommand(HistoryRanks+"SELECT model_key,model_name,count(*) FILTER(WHERE trashed_at IS NULL)::int,count(*) FILTER(WHERE trashed_at IS NOT NULL)::int FROM ranked GROUP BY model_key,model_name ORDER BY model_key",conn))
        {
            Add(cmd,course,actor,stage,practice);await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))models.Add(new(r.GetString(0),r.IsDBNull(1)?null:r.GetString(1),r.GetInt32(2),r.GetInt32(3)));
        }
        var counts=models.Where(m=>modelKey==""||m.Key==modelKey).ToArray();
        var before=page.Before??0;var focused=false;
        if(page.Target is {} target)
        {
            // A delayed completion or restored attempt can be older than the
            // latest five. Seek directly to it; never loop through other pages.
            await using var cmd=new NpgsqlCommand(HistoryRanks+"SELECT t.n,(SELECT count(*) FROM ranked x WHERE x.n>t.n AND ($6::text='' OR x.model_key=$6) AND (x.trashed_at IS NOT NULL)=$7) FROM ranked t WHERE t.id=$5 AND ($6::text='' OR t.model_key=$6) AND (t.trashed_at IS NOT NULL)=$7",conn);
            Add(cmd,course,actor,stage,practice,target,modelKey,page.Trashed);await using var r=await cmd.ExecuteReaderAsync(ct);
            if(!await r.ReadAsync(ct))throw new CaseChatException(404,"找不到可查看的本人作答。");
            if(r.GetInt64(1)>=5){before=checked(r.GetInt32(0)+1);focused=true;}
        }
        var selected=new List<(Guid Id,Guid Session,int Number)>();
        await using(var cmd=new NpgsqlCommand(HistoryRanks+"SELECT id,session_id,n FROM ranked WHERE ($5::text='' OR model_key=$5) AND (trashed_at IS NOT NULL)=$6 AND ($7::integer=0 OR n<$7) ORDER BY n DESC LIMIT 6",conn))
        {
            Add(cmd,course,actor,stage,practice,modelKey,page.Trashed,before);await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))selected.Add((r.GetGuid(0),r.GetGuid(1),r.GetInt32(2)));
        }
        var hasMore=selected.Count>5;selected=selected.Take(5).ToList();
        Guid? active=null,activeLast=null;
        await using(var cmd=new NpgsqlCommand("SELECT s.id,(SELECT m.id FROM case_analysis_messages m WHERE m.session_id=s.id ORDER BY m.sequence_number DESC LIMIT 1) FROM case_analysis_sessions s WHERE s.course_id=$1 AND s.student_id=$2 AND s.stage=$3 AND s.is_practice=$4 AND s.status='active' ORDER BY s.started_at DESC,s.id LIMIT 1",conn))
        {
            Add(cmd,course,actor,stage,practice);await using var r=await cmd.ExecuteReaderAsync(ct);if(await r.ReadAsync(ct)){active=r.GetGuid(0);activeLast=r.IsDBNull(1)?null:r.GetGuid(1);}
        }
        var messages=selected.Select(s=>s.Session).Distinct().ToDictionary(id=>id,_=>new List<CaseChatMessage>());
        var numbers=selected.ToDictionary(s=>s.Id,s=>s.Number);
        if(selected.Count>0)
        {
            await using var cmd=new NpgsqlCommand("""
                SELECT m.session_id,m.id,m.role,m.content,m.sequence_number,
                  CASE WHEN m.response_status='pending' AND m.lease_until<=$2 THEN 'failed' ELSE m.response_status END,
                  m.in_reply_to,m.model_name,m.prompt_version,m.created_at,m.performance_metrics,m.revises_message_id,m.annotations,
                  CASE WHEN m.role='user' THEN m.trashed_at ELSE parent.trashed_at END,m.rule_version,m.rule_snapshot
                FROM case_analysis_messages m LEFT JOIN case_analysis_messages parent ON parent.id=m.in_reply_to AND parent.role='user' AND parent.session_id=m.session_id
                WHERE m.session_id=ANY($3) AND ((m.role='user' AND m.id=ANY($1)) OR (m.role='assistant' AND m.in_reply_to=ANY($1) AND parent.id IS NOT NULL))
                ORDER BY m.sequence_number
                """,conn);
            Add(cmd,selected.Select(s=>s.Id).ToArray(),clock.GetUtcNow(),messages.Keys.ToArray());await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))messages[r.GetGuid(0)].Add(new(r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetInt32(4),r.GetString(5),r.IsDBNull(6)?null:r.GetGuid(6),r.IsDBNull(7)?null:r.GetString(7),r.IsDBNull(8)?null:r.GetString(8),r.GetFieldValue<DateTimeOffset>(9),r.IsDBNull(10)?null:JsonSerializer.Deserialize<RequestMetrics>(r.GetString(10),PerformanceJson),r.IsDBNull(11)?null:r.GetGuid(11),r.IsDBNull(12)?null:JsonSerializer.Deserialize<CaseTextAnnotation[]>(r.GetString(12),PerformanceJson),r.IsDBNull(13)?null:r.GetFieldValue<DateTimeOffset>(13),r.IsDBNull(14)?null:r.GetInt32(14),r.IsDBNull(15)?CaseClassificationRules.Defaults:JsonSerializer.Deserialize<CaseClassificationRuleDefinition[]>(r.GetString(15),PerformanceJson),numbers.TryGetValue(r.GetGuid(1),out var number)?number:null));
        }
        var sessions=new List<CaseChatSession>();
        if(messages.Count>0)
        {
            // Session statistics remain complete even though bodies are paged.
            await using var cmd=new NpgsqlCommand("""
                SELECT s.id,s.status,s.last_activity_at,count(m.id) FILTER(WHERE m.role='user')::int,
                  count(m.id) FILTER(WHERE m.role='assistant')::int,
                  count(m.id) FILTER(WHERE m.role='assistant' AND EXISTS(SELECT 1 FROM case_analysis_messages u WHERE u.id=m.in_reply_to AND u.role='user' AND u.response_status='completed'))::int,
                  count(m.id) FILTER(WHERE m.revises_message_id IS NOT NULL)::int
                FROM case_analysis_sessions s LEFT JOIN case_analysis_messages m ON m.session_id=s.id
                WHERE s.id=ANY($1) AND s.course_id=$2 AND s.student_id=$3 AND s.stage=$4 AND s.is_practice=$5
                GROUP BY s.id ORDER BY s.started_at DESC,s.id
                """,conn);
            Add(cmd,messages.Keys.ToArray(),course,actor,stage,practice);await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))sessions.Add(new(r.GetGuid(0),stage,r.GetString(1),messages[r.GetGuid(0)],new(r.GetInt32(3),r.GetInt32(4),r.GetInt32(5),r.GetFieldValue<DateTimeOffset>(2),r.GetInt32(6))));
        }
        await tx.CommitAsync(ct);
        return new(sessions,active,new(hasMore?selected[^1].Number:null,counts.Sum(m=>page.Trashed?m.TrashCount:m.NormalCount),counts.Sum(m=>m.TrashCount),models,activeLast,modelKey,page.Trashed,Focused:focused));
    }
}
