using System.Text.Json;
using AiTeachingAssistant.Models;
using Npgsql;
namespace AiTeachingAssistant.Services;

public static class LearningTimePolicy
{
    public const int MaximumGapSeconds=25;
    public static string? MediaType(string name,byte[] prefix)
    {
        var doc=FilePreviewPolicy.ContentType(name,prefix);if(doc!=null)return doc;
        if(Path.GetExtension(name).Equals(".mp4",StringComparison.OrdinalIgnoreCase)&&prefix.Length>=12&&prefix.AsSpan(4,4).SequenceEqual("ftyp"u8))return "video/mp4";
        if(Path.GetExtension(name).Equals(".webm",StringComparison.OrdinalIgnoreCase)&&prefix.AsSpan().StartsWith(new byte[]{0x1a,0x45,0xdf,0xa3}))return "video/webm";
        return null;
    }
    // The client never supplies elapsed seconds. Position is only an additional cap
    // for video; forward seeks and gaps do not earn credit.
    public static long Credit(DateTimeOffset prior,DateTimeOffset now,bool active,bool video,double? previousPosition,double? position)
    {
        var elapsed=(now-prior).TotalMilliseconds;
        if(!active||elapsed<=0||elapsed>MaximumGapSeconds*1000)return 0;
        if(video){
            if(!previousPosition.HasValue||!position.HasValue||!double.IsFinite(position.Value)||position<0)return 0;
            var movement=(position.Value-previousPosition.Value)*1000;
            if(movement<=0||movement>elapsed+1500)return 0;
            elapsed=Math.Min(elapsed,movement);
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
            await using var cmd=new NpgsqlCommand("SELECT file_name,substring(content from 1 for 16) FROM course_resources WHERE id=$1 AND course_id=$2",conn);Add(cmd,item.ResourceId);Add(cmd,course);
            await using var r=await cmd.ExecuteReaderAsync(ct);
            if(!await r.ReadAsync(ct)||LearningTimePolicy.MediaType(r.GetString(0),r.GetFieldValue<byte[]>(1))==null)throw new AssignmentWorkflowException(400,"門檻教材必須是本課可追蹤的 PDF、圖片或 MP4／WebM 影片。");
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
              COALESCE((SELECT sum(s.credited_milliseconds)/1000 FROM learning_time_sessions s WHERE s.resource_id=r.id AND s.user_id=$2 AND s.is_practice=$3),0)::bigint,
              r.file_name,substring(r.content from 1 for 16)
            FROM quiz_learning_requirements q JOIN course_resources r ON r.id=q.resource_id
            WHERE q.assignment_id=$1 ORDER BY r.created_at,r.id
            """,conn)){
            Add(cmd,assignment);Add(cmd,actor);Add(cmd,practice);await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))items.Add(new(r.GetGuid(0),r.GetString(1),r.GetInt32(2),r.GetInt64(3),LearningTimePolicy.MediaType(r.GetString(4),r.GetFieldValue<byte[]>(5))));}
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
              COALESCE((SELECT sum(credited_milliseconds)/1000 FROM learning_time_sessions WHERE resource_id=$1 AND user_id=$3 AND is_practice=$4),0)::bigint
            FROM course_resources WHERE id=$1 AND course_id=$2
            """,conn);Add(cmd,id);Add(cmd,course);Add(cmd,actor);Add(cmd,practice);await using var r=await cmd.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))throw new AssignmentWorkflowException(404,"找不到教材。");
        var media=LearningTimePolicy.MediaType(r.GetString(0),r.GetFieldValue<byte[]>(2));if(media==null)throw new AssignmentWorkflowException(415,"此教材尚不支援有效閱讀時間追蹤，請下載後開啟。");
        return new(id,course,r.GetString(1),r.GetString(0),media,r.GetInt64(3),status!="archived"&&(role=="student"||practice),practice);
    }
    private static async Task LockUser(NpgsqlConnection conn,Guid actor,CancellationToken ct)
    {await using var cmd=new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1::text,17001))",conn);Add(cmd,actor.ToString());await cmd.ExecuteNonQueryAsync(ct);}
    public async Task<LearningSession> Start(Guid course,Guid id,Guid actor,bool practice,double? position,CancellationToken ct)
    {
        var resource=await Resource(course,id,actor,practice,ct);if(!resource.CanTrack)throw new AssignmentWorkflowException(403,"只有未封存課程的學生或本人學生體驗可累積時間。");
        if(position.HasValue&&(!double.IsFinite(position.Value)||position<0))throw new AssignmentWorkflowException(400,"影片位置無效。");
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);await LockUser(conn,actor,ct);
        var (role,status)=await Access(conn,course,actor,practice,ct);if(status=="archived"||role!="student"&&!practice)throw new AssignmentWorkflowException(403,"目前無法累積時間。");
        await using(var close=new NpgsqlCommand("UPDATE learning_time_sessions SET closed=true WHERE user_id=$1 AND NOT closed",conn)){Add(close,actor);await close.ExecuteNonQueryAsync(ct);}
        var idSession=Guid.NewGuid();var now=clock.GetUtcNow();
        await using(var cmd=new NpgsqlCommand("INSERT INTO learning_time_sessions(id,resource_id,user_id,is_practice,started_at,last_seen_at,last_position) VALUES($1,$2,$3,$4,$5,$5,$6)",conn)){
            Add(cmd,idSession);Add(cmd,id);Add(cmd,actor);Add(cmd,practice);Add(cmd,now);cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Double,Value=(object?)position??DBNull.Value});await cmd.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct);return new(idSession,resource.AccumulatedSeconds);
    }
    public async Task<LearningSession> Heartbeat(Guid course,Guid resource,Guid session,Guid actor,bool practice,LearningHeartbeat input,CancellationToken ct)
    {
        if(input.Position.HasValue&&(!double.IsFinite(input.Position.Value)||input.Position<0))throw new AssignmentWorkflowException(400,"影片位置無效。");
        var meta=await Resource(course,resource,actor,practice,ct);if(!meta.CanTrack)throw new AssignmentWorkflowException(403,"目前無法累積教材時間。");
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);await LockUser(conn,actor,ct);
        var (role,status)=await Access(conn,course,actor,practice,ct);if(status=="archived"||role!="student"&&!practice)throw new AssignmentWorkflowException(403,"目前無法累積時間。");
        DateTimeOffset previous;double? position;
        await using(var cmd=new NpgsqlCommand("SELECT last_seen_at,last_position FROM learning_time_sessions WHERE id=$1 AND resource_id=$2 AND user_id=$3 AND is_practice=$4 AND NOT closed FOR UPDATE",conn)){
            Add(cmd,session);Add(cmd,resource);Add(cmd,actor);Add(cmd,practice);await using var r=await cmd.ExecuteReaderAsync(ct);
            if(!await r.ReadAsync(ct))throw new AssignmentWorkflowException(409,"此學習計時已暫停，或已在另一個教材／裝置開始，請重新開始。");previous=r.GetFieldValue<DateTimeOffset>(0);position=r.IsDBNull(1)?null:r.GetDouble(1);}
        var now=clock.GetUtcNow();var credit=LearningTimePolicy.Credit(previous,now,input.Active,meta.MediaType.StartsWith("video/"),position,input.Position);
        await using(var cmd=new NpgsqlCommand("UPDATE learning_time_sessions SET last_seen_at=$2,last_position=$3,credited_milliseconds=credited_milliseconds+$4,closed=NOT $5 WHERE id=$1",conn)){
            Add(cmd,session);Add(cmd,now);cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Double,Value=(object?)input.Position??DBNull.Value});Add(cmd,credit);Add(cmd,input.Active);await cmd.ExecuteNonQueryAsync(ct);}
        long seconds;await using(var cmd=new NpgsqlCommand("SELECT COALESCE(sum(credited_milliseconds)/1000,0)::bigint FROM learning_time_sessions WHERE resource_id=$1 AND user_id=$2 AND is_practice=$3",conn)){Add(cmd,resource);Add(cmd,actor);Add(cmd,practice);seconds=(long)(await cmd.ExecuteScalarAsync(ct))!;}
        await tx.CommitAsync(ct);return new(session,seconds);
    }
}
