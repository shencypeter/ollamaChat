using System.Text.Json;
using AiTeachingAssistant.Models;
using Npgsql;
namespace AiTeachingAssistant.Services;

public static class LearningTimePolicy
{
    public const int MaximumGapSeconds=25;
    public static string? MediaType(string name,byte[] prefix,string resourceType="file")
    {
        if(resourceType=="youtube")return "video/youtube";
        var doc=FilePreviewPolicy.ContentType(name,prefix);if(doc!=null)return doc;
        if(Path.GetExtension(name).Equals(".mp4",StringComparison.OrdinalIgnoreCase)&&prefix.Length>=12&&prefix.AsSpan(4,4).SequenceEqual("ftyp"u8))return "video/mp4";
        if(Path.GetExtension(name).Equals(".webm",StringComparison.OrdinalIgnoreCase)&&prefix.AsSpan().StartsWith(new byte[]{0x1a,0x45,0xdf,0xa3}))return "video/webm";
        return null;
    }
    // The client never supplies elapsed seconds. Position is only an additional cap
    // for video; forward seeks and gaps do not earn credit.
    public static long Credit(DateTimeOffset prior,DateTimeOffset now,bool active,bool video,double? previousPosition,double? position,double playbackRate=1)
    {
        var elapsed=(now-prior).TotalMilliseconds;
        if(!double.IsFinite(playbackRate)||playbackRate<.25||playbackRate>4)return 0;
        if(!active||elapsed<=0||elapsed>MaximumGapSeconds*1000)return 0;
        if(video){
            if(!previousPosition.HasValue||!position.HasValue||!double.IsFinite(position.Value)||position<0)return 0;
            var movement=(position.Value-previousPosition.Value)*1000;
            if(movement<=0||movement>elapsed*playbackRate+1500)return 0;
            elapsed=Math.Min(elapsed,movement/playbackRate);
        }
        return (long)Math.Floor(elapsed);
    }
}

public sealed class LearningTime(NpgsqlDataSource source,TimeProvider clock)
{
    private static void Add(NpgsqlCommand cmd,object value)=>cmd.Parameters.AddWithValue(value);
    public static async Task SaveRequirements(NpgsqlConnection conn,Guid assignment,Guid course,IReadOnlyList<LearningRequirementInput>? inputs,CancellationToken ct)
    {
        if(inputs==null||inputs.Count>20||inputs.Select(x=>x.ResourceId).Distinct().Count()!=inputs.Count||inputs.Any(x=>x.MinimumSeconds is <1 or >86400))throw new AssignmentWorkflowException(400,"教材門檻最多 20 份，各需 1 秒至 24 小時，且不可重複。");
        foreach(var item in inputs){
            await using var cmd=new NpgsqlCommand("SELECT file_name,substring(content from 1 for 16),resource_type FROM course_resources WHERE id=$1 AND course_id=$2",conn);Add(cmd,item.ResourceId);Add(cmd,course);
            await using var r=await cmd.ExecuteReaderAsync(ct);
            if(!await r.ReadAsync(ct)||LearningTimePolicy.MediaType(r.GetString(0),r.GetFieldValue<byte[]>(1),r.GetString(2))==null)throw new AssignmentWorkflowException(400,"門檻教材必須是本課可追蹤的 PDF、圖片、MP4／WebM 或 YouTube 影片。");
        }
        await using(var cmd=new NpgsqlCommand("DELETE FROM quiz_learning_requirements WHERE assignment_id=$1",conn)){Add(cmd,assignment);await cmd.ExecuteNonQueryAsync(ct);}
        foreach(var item in inputs){await using var cmd=new NpgsqlCommand("INSERT INTO quiz_learning_requirements(assignment_id,resource_id,minimum_seconds) VALUES($1,$2,$3)",conn);Add(cmd,assignment);Add(cmd,item.ResourceId);Add(cmd,item.MinimumSeconds);await cmd.ExecuteNonQueryAsync(ct);}
    }
    public static async Task<LearningGate> Gate(NpgsqlConnection conn,Guid assignment,Guid actor,bool practice,CancellationToken ct)
    {
        bool started;await using(var cmd=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM quiz_learning_starts WHERE assignment_id=$1 AND user_id=$2 AND is_practice=$3)",conn)){Add(cmd,assignment);Add(cmd,actor);Add(cmd,practice);started=await cmd.ExecuteScalarAsync(ct) is true;}
        var items=new List<LearningRequirement>();
        await using(var cmd=new NpgsqlCommand("""
            SELECT r.id,COALESCE(NULLIF(r.display_title,''),r.file_name),q.minimum_seconds,
              COALESCE((SELECT floor(sum(s.content_milliseconds)/1000.0) FROM learning_time_sessions s WHERE s.resource_id=r.id AND s.user_id=$2 AND s.is_practice=$3),0)::bigint,
              r.file_name,substring(r.content from 1 for 16),r.resource_type
            FROM quiz_learning_requirements q JOIN course_resources r ON r.id=q.resource_id
            WHERE q.assignment_id=$1 ORDER BY r.created_at,r.id
            """,conn)){
            Add(cmd,assignment);Add(cmd,actor);Add(cmd,practice);await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))items.Add(new(r.GetGuid(0),r.GetString(1),r.GetInt32(2),r.GetInt64(3),LearningTimePolicy.MediaType(r.GetString(4),r.GetFieldValue<byte[]>(5),r.GetString(6))));}
        return new(started,items);
    }
    public static async Task RequireStarted(NpgsqlConnection conn,Guid assignment,Guid actor,bool practice,CancellationToken ct)
    {if((await Gate(conn,assignment,actor,practice,ct)).Locked)throw new AssignmentWorkflowException(409,"請先完成教材學習門檻並按「開始測驗」。");}
    private async Task<(string Role,string Status)> Access(NpgsqlConnection conn,Guid course,Guid actor,bool practice,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("SELECT m.course_role,c.status FROM course_members m JOIN courses c ON c.id=m.course_id WHERE m.user_id=$1 AND m.course_id=$2 AND m.status='active'",conn);Add(cmd,actor);Add(cmd,course);await using var r=await cmd.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))throw new AssignmentWorkflowException(403,"你不是本課程有效成員。");
        var role=r.GetString(0);if(practice&&role is not ("teacher" or "ta"))throw new AssignmentWorkflowException(403,"無法使用此課程的練習紀錄。");return(role,r.GetString(1));
    }
    public async Task<LearningResource> Resource(Guid course,Guid id,Guid actor,bool practice,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);var (role,status)=await Access(conn,course,actor,practice,ct);
        await using var cmd=new NpgsqlCommand("""
            SELECT file_name,COALESCE(NULLIF(display_title,''),file_name),substring(content from 1 for 16),
              COALESCE((SELECT floor(sum(credited_milliseconds)/1000.0) FROM learning_time_sessions WHERE resource_id=$1 AND user_id=$3 AND is_practice=$4),0)::bigint
             ,resource_type,youtube_video_id,COALESCE((SELECT floor(sum(content_milliseconds)/1000.0) FROM learning_time_sessions WHERE resource_id=$1 AND user_id=$3 AND is_practice=$4),0)::bigint FROM course_resources WHERE id=$1 AND course_id=$2
            """,conn);Add(cmd,id);Add(cmd,course);Add(cmd,actor);Add(cmd,practice);await using var r=await cmd.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))throw new AssignmentWorkflowException(404,"找不到教材。");
        var media=LearningTimePolicy.MediaType(r.GetString(0),r.GetFieldValue<byte[]>(2),r.GetString(4));if(media==null)throw new AssignmentWorkflowException(415,"此教材尚不支援有效閱讀時間追蹤，請下載後開啟。");
        return new(id,course,r.GetString(1),r.GetString(0),media,practice?0:r.GetInt64(3),status!="archived"&&role=="student"&&!practice,practice,r.IsDBNull(5)?null:r.GetString(5),practice?0:r.GetInt64(6));
    }
    public async Task<object> Statistics(Guid course,Guid id,Guid actor,bool practice,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);var (role,status)=await Access(conn,course,actor,false,ct);
        if(practice || role is not ("teacher" or "ta"))throw new AssignmentWorkflowException(403,"只有本課程教師／助教可查看正式教材參與紀錄。");
        string? media;
        await using(var check=new NpgsqlCommand("SELECT file_name,substring(content from 1 for 16),resource_type FROM course_resources WHERE course_id=$1 AND id=$2",conn)){
            Add(check,course);Add(check,id);await using var found=await check.ExecuteReaderAsync(ct);if(!await found.ReadAsync(ct))throw new AssignmentWorkflowException(404,"找不到本課教材。");media=LearningTimePolicy.MediaType(found.GetString(0),found.GetFieldValue<byte[]>(1),found.GetString(2));}
        var isVideo=media?.StartsWith("video/")==true;
        var breakdown=new Dictionary<Guid,List<object>>();
        // Load speed evidence before opening the student summary reader.
        if(isVideo)await using(var rates=new NpgsqlCommand("SELECT s.user_id,s.playback_rate,floor(sum(s.credited_milliseconds)/1000.0)::bigint,floor(sum(s.content_milliseconds)/1000.0)::bigint FROM learning_time_sessions s JOIN course_members m ON m.user_id=s.user_id AND m.course_id=$2 JOIN app_users u ON u.id=m.user_id WHERE s.resource_id=$1 AND NOT s.is_practice AND s.credited_milliseconds>0 AND m.status='active' AND m.course_role='student' AND u.status='active' GROUP BY s.user_id,s.playback_rate ORDER BY s.playback_rate",conn)){
            Add(rates,id);Add(rates,course);await using var rr=await rates.ExecuteReaderAsync(ct);
            while(await rr.ReadAsync(ct)){var user=rr.GetGuid(0);if(!breakdown.ContainsKey(user))breakdown[user]=[];breakdown[user].Add(new{playbackRate=rr.GetDouble(1),watchSeconds=rr.GetInt64(2),contentSeconds=rr.GetInt64(3)});}
        }
        await using var likeCount=new NpgsqlCommand("SELECT count(*) FROM resource_likes WHERE resource_id=$1",conn);Add(likeCount,id);var likes=(long)(await likeCount.ExecuteScalarAsync(ct))!;
        await using var cmd=new NpgsqlCommand("""
            SELECT u.id,u.display_name,u.student_number,
              COALESCE((SELECT floor(sum(s.credited_milliseconds)/1000.0) FROM learning_time_sessions s WHERE s.user_id=u.id AND s.resource_id=$1 AND NOT s.is_practice AND s.credited_milliseconds>0),0)::bigint,
              (SELECT count(*) FROM learning_time_sessions s WHERE s.user_id=u.id AND s.resource_id=$1 AND NOT s.is_practice AND s.credited_milliseconds>0),
              (SELECT max(s.last_seen_at) FROM learning_time_sessions s WHERE s.user_id=u.id AND s.resource_id=$1 AND NOT s.is_practice AND s.credited_milliseconds>0),
              COALESCE((SELECT floor(sum(s.content_milliseconds)/1000.0) FROM learning_time_sessions s WHERE s.user_id=u.id AND s.resource_id=$1 AND NOT s.is_practice AND s.credited_milliseconds>0),0)::bigint,
              (SELECT count(*) FROM resource_views v WHERE v.user_id=u.id AND v.resource_id=$1),
              (SELECT max(v.viewed_at) FROM resource_views v WHERE v.user_id=u.id AND v.resource_id=$1)
            FROM course_members m JOIN app_users u ON u.id=m.user_id
            WHERE m.course_id=$2 AND m.status='active' AND m.course_role='student' AND u.status='active'
            ORDER BY u.student_number NULLS LAST,u.display_name,u.id
            """,conn);Add(cmd,id);Add(cmd,course);await using var r=await cmd.ExecuteReaderAsync(ct);
        var rows=new List<object>();long total=0,totalContent=0,views=0;int watched=0,viewed=0;
        while(await r.ReadAsync(ct)){var seconds=r.GetInt64(3);var studentViews=r.GetInt64(7);total+=seconds;totalContent+=r.GetInt64(6);views+=studentViews;if(seconds>0)watched++;if(studentViews>0)viewed++;rows.Add(new{id=r.GetGuid(0),displayName=r.GetString(1),studentNumber=r.IsDBNull(2)?null:r.GetString(2),accumulatedSeconds=seconds,contentSeconds=r.GetInt64(6),playbackSpeeds=breakdown.GetValueOrDefault(r.GetGuid(0))??[],sessions=r.GetInt64(4),lastWatchedAt=r.IsDBNull(5)?(DateTimeOffset?)null:r.GetFieldValue<DateTimeOffset>(5),views=studentViews,lastViewedAt=r.IsDBNull(8)?(DateTimeOffset?)null:r.GetFieldValue<DateTimeOffset>(8)});}
        return new{resourceId=id,isVideo,studentCount=rows.Count,watchedCount=watched,totalSeconds=total,totalContentSeconds=totalContent,viewedCount=viewed,viewCount=views,likes,students=rows};
    }
    private static async Task LockUser(NpgsqlConnection conn,Guid actor,CancellationToken ct)
    {await using var cmd=new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1::text,17001))",conn);Add(cmd,actor.ToString());await cmd.ExecuteNonQueryAsync(ct);}
    public async Task<LearningSession> Start(Guid course,Guid id,Guid actor,bool practice,double? position,CancellationToken ct,double playbackRate=1)
    {
        if(!double.IsFinite(playbackRate)||playbackRate<.25||playbackRate>4)throw new AssignmentWorkflowException(400,"播放速度須介於0.25至4倍。");
        var resource=await Resource(course,id,actor,practice,ct);if(!resource.CanTrack)throw new AssignmentWorkflowException(403,"只有未封存課程的學生或本人學生體驗可累積時間。");
        if(position.HasValue&&(!double.IsFinite(position.Value)||position<0))throw new AssignmentWorkflowException(400,"影片位置無效。");
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);await LockUser(conn,actor,ct);
        var (role,status)=await Access(conn,course,actor,practice,ct);if(status=="archived"||role!="student"&&!practice)throw new AssignmentWorkflowException(403,"目前無法累積時間。");
        await using(var close=new NpgsqlCommand("UPDATE learning_time_sessions SET closed=true WHERE user_id=$1 AND NOT closed",conn)){Add(close,actor);await close.ExecuteNonQueryAsync(ct);}
        var idSession=Guid.NewGuid();var now=clock.GetUtcNow();
        await using(var cmd=new NpgsqlCommand("INSERT INTO learning_time_sessions(id,resource_id,user_id,is_practice,started_at,last_seen_at,last_position,playback_rate) VALUES($1,$2,$3,$4,$5,$5,$6,$7)",conn)){
            Add(cmd,idSession);Add(cmd,id);Add(cmd,actor);Add(cmd,practice);Add(cmd,now);cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Double,Value=(object?)position??DBNull.Value});Add(cmd,resource.MediaType.StartsWith("video/")?playbackRate:1d);await cmd.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct);return new(idSession,resource.AccumulatedSeconds,resource.ContentSeconds);
    }
    public async Task<LearningSession> Heartbeat(Guid course,Guid resource,Guid session,Guid actor,bool practice,LearningHeartbeat input,CancellationToken ct)
    {
        if(input.Position.HasValue&&(!double.IsFinite(input.Position.Value)||input.Position<0))throw new AssignmentWorkflowException(400,"影片位置無效。");
        var meta=await Resource(course,resource,actor,practice,ct);if(!meta.CanTrack)throw new AssignmentWorkflowException(403,"目前無法累積教材時間。");
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);await LockUser(conn,actor,ct);
        var (role,status)=await Access(conn,course,actor,practice,ct);if(status=="archived"||role!="student"&&!practice)throw new AssignmentWorkflowException(403,"目前無法累積時間。");
        DateTimeOffset previous;double? position;double playbackRate;
        await using(var cmd=new NpgsqlCommand("SELECT last_seen_at,last_position,playback_rate FROM learning_time_sessions WHERE id=$1 AND resource_id=$2 AND user_id=$3 AND is_practice=$4 AND NOT closed FOR UPDATE",conn)){
            Add(cmd,session);Add(cmd,resource);Add(cmd,actor);Add(cmd,practice);await using var r=await cmd.ExecuteReaderAsync(ct);
            if(!await r.ReadAsync(ct))throw new AssignmentWorkflowException(409,"此學習計時已暫停，或已在另一個教材／裝置開始，請重新開始。");previous=r.GetFieldValue<DateTimeOffset>(0);position=r.IsDBNull(1)?null:r.GetDouble(1);playbackRate=r.GetDouble(2);}
        var now=clock.GetUtcNow();var credit=LearningTimePolicy.Credit(previous,now,input.Active,meta.MediaType.StartsWith("video/"),position,input.Position,playbackRate);
        if(meta.MediaType.StartsWith("video/") && input.PlaybackRate!=playbackRate)credit=0;
        var content=meta.MediaType.StartsWith("video/")?(long)Math.Floor(credit*playbackRate):credit;
        await using(var cmd=new NpgsqlCommand("UPDATE learning_time_sessions SET last_seen_at=$2,last_position=$3,credited_milliseconds=credited_milliseconds+$4,closed=($6 OR NOT $5),content_milliseconds=content_milliseconds+$7 WHERE id=$1",conn)){
            Add(cmd,session);Add(cmd,now);cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Double,Value=(object?)input.Position??DBNull.Value});Add(cmd,credit);Add(cmd,input.Active);Add(cmd,input.Close);Add(cmd,content);await cmd.ExecuteNonQueryAsync(ct);}
        long seconds;long contentSeconds;await using(var cmd=new NpgsqlCommand("SELECT COALESCE(floor(sum(credited_milliseconds)/1000.0),0)::bigint,COALESCE(floor(sum(content_milliseconds)/1000.0),0)::bigint FROM learning_time_sessions WHERE resource_id=$1 AND user_id=$2 AND is_practice=$3",conn)){Add(cmd,resource);Add(cmd,actor);Add(cmd,practice);await using var r=await cmd.ExecuteReaderAsync(ct);await r.ReadAsync(ct);seconds=r.GetInt64(0);contentSeconds=r.GetInt64(1);}
        await tx.CommitAsync(ct);return new(session,seconds,contentSeconds);
    }
}
