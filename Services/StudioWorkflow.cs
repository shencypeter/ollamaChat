using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using Npgsql;
using NpgsqlTypes;
namespace AiTeachingAssistant.Services;

public sealed class StudioWorkflow(NpgsqlDataSource source,LearnerExperience experience)
{
    private static readonly JsonSerializerOptions Json=StudioFeedbackContract.Json;
    private static void Add(NpgsqlCommand cmd,object value)=>cmd.Parameters.AddWithValue(value);
    private static void Fail(int status,string message)=>throw new AssignmentWorkflowException(status,message);
    public static bool Staff(string role)=>role is "teacher" or "ta";
    public async Task<string> Access(NpgsqlConnection conn,Guid course,Guid actor,bool staff,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("SELECT m.course_role FROM course_members m WHERE m.course_id=$1 AND m.user_id=$2 AND m.status='active'",conn);Add(cmd,course);Add(cmd,actor);
        var role=await cmd.ExecuteScalarAsync(ct) as string;
        if(experience.IsPractice && role!=null)role="student";
        if(role==null || (staff&&!Staff(role)))Fail(403,"你沒有此課程的存取權限。");return role!;
    }
    public async Task<StudioCase> LoadCase(NpgsqlConnection conn,Guid caseId,Guid actor,bool admin,bool references,CancellationToken ct,Guid? versionId=null)
    {
        await using(var access=new NpgsqlCommand("SELECT course_id FROM studio_cases WHERE id=$1",conn)){Add(access,caseId);if(await access.ExecuteScalarAsync(ct) is not Guid course)Fail(404,"找不到可讀取的案例。");else await Access(conn,course,actor,admin,ct);}
        await using var cmd=new NpgsqlCommand("""
            SELECT c.id,c.course_id,k.title,v.id,v.version_no,v.title,v.material,v.prompts::text,v.reference_points::text,
              COALESCE(c.active_version_id=v.id,false),c.draft_version_id
            FROM studio_cases c JOIN courses k ON k.id=c.course_id
            JOIN studio_case_versions v ON v.case_id=c.id AND v.id=COALESCE($2::uuid,CASE WHEN $3 THEN COALESCE(c.draft_version_id,c.active_version_id) ELSE c.active_version_id END)
            WHERE c.id=$1
            """,conn);Add(cmd,caseId);cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlDbType.Uuid,Value=(object?)versionId??DBNull.Value});Add(cmd,admin);
        StudioCase result;
        await using(var r=await cmd.ExecuteReaderAsync(ct)){
            if(!await r.ReadAsync(ct))throw new AssignmentWorkflowException(404,"找不到可讀取的案例。");
            result=new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetGuid(3),r.GetInt32(4),r.GetString(5),r.GetString(6),JsonSerializer.Deserialize<Dictionary<string,string>>(r.GetString(7),Json)!,references?JsonSerializer.Deserialize<Dictionary<string,string>>(r.GetString(8),Json):null,r.GetBoolean(9),admin&&!r.IsDBNull(10)?r.GetGuid(10):null);
        }
        await Access(conn,result.CourseId,actor,admin,ct);return result;
    }
    public async Task<IReadOnlyList<object>> Cases(Guid actor,bool admin,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var cmd=new NpgsqlCommand("""
            SELECT c.id,c.course_id,k.title,v.title,v.version_no,c.active_version_id IS NOT NULL
            FROM studio_cases c JOIN courses k ON k.id=c.course_id
            JOIN studio_case_versions v ON v.id=CASE WHEN $2 THEN COALESCE(c.draft_version_id,c.active_version_id) ELSE c.active_version_id END
            WHERE EXISTS(SELECT 1 FROM course_members m WHERE m.course_id=c.course_id AND m.user_id=$1 AND m.status='active' AND (NOT $2 OR m.course_role IN ('teacher','ta')))
            ORDER BY c.created_at DESC
            """,conn);Add(cmd,actor);Add(cmd,admin);await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<object>();
        while(await r.ReadAsync(ct))result.Add(new{id=r.GetGuid(0),courseId=r.GetGuid(1),courseTitle=r.GetString(2),title=r.GetString(3),versionNo=r.GetInt32(4),published=r.GetBoolean(5)});return result;
    }
    public async Task<StudioAttempt> ReadAttempt(NpgsqlConnection conn,Guid id,CancellationToken ct)
    {
        int version;DateTimeOffset? submitted;bool practice;
        await using(var cmd=new NpgsqlCommand("SELECT version,submitted_at,is_practice FROM studio_attempts WHERE id=$1",conn)){
            Add(cmd,id);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new AssignmentWorkflowException(404,"找不到學習紀錄。");version=r.GetInt32(0);submitted=r.IsDBNull(1)?null:r.GetFieldValue<DateTimeOffset>(1);practice=r.GetBoolean(2);
        }
        var answers=new List<StudioAnswer>();var feedback=new List<StudioFeedback>();
        await using(var cmd=new NpgsqlCommand("SELECT id,stage,revision_no,answer_text,created_at FROM studio_answer_versions WHERE attempt_id=$1 ORDER BY stage,revision_no DESC",conn)){
            Add(cmd,id);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))answers.Add(new(r.GetGuid(0),r.GetString(1),r.GetInt32(2),r.GetString(3),r.GetFieldValue<DateTimeOffset>(4)));
        }
        await using(var cmd=new NpgsqlCommand("SELECT f.id,f.answer_version_id,f.status,f.findings::text,f.error_code,f.created_at,f.completed_at FROM studio_feedback_runs f JOIN studio_answer_versions a ON a.id=f.answer_version_id WHERE a.attempt_id=$1 ORDER BY f.created_at DESC,f.id DESC",conn)){
            Add(cmd,id);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct)){
                var created=r.GetFieldValue<DateTimeOffset>(5);var interrupted=r.GetString(2)=="pending" && created<DateTimeOffset.UtcNow.AddSeconds(-310);
                feedback.Add(new(r.GetGuid(0),r.GetGuid(1),interrupted?"failed":r.GetString(2),r.IsDBNull(3)?null:JsonSerializer.Deserialize<StudioFeedbackContent>(r.GetString(3),Json),interrupted?"interrupted":r.IsDBNull(4)?null:r.GetString(4),created,r.IsDBNull(6)?null:r.GetFieldValue<DateTimeOffset>(6)));
            }
        }
        return new(id,version,submitted,practice,answers,feedback);
    }
    public async Task<(StudioCase Case,StudioAttempt Attempt)> OwnContext(Guid id,Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        Guid caseId,version;bool practice;
        await using(var cmd=new NpgsqlCommand("SELECT v.case_id,a.case_version_id,a.is_practice FROM studio_attempts a JOIN studio_case_versions v ON v.id=a.case_version_id WHERE a.id=$1 AND a.student_id=$2",conn)){
            Add(cmd,id);Add(cmd,actor);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new AssignmentWorkflowException(404,"找不到本人學習紀錄。");caseId=r.GetGuid(0);version=r.GetGuid(1);practice=r.GetBoolean(2);
        }
        if(practice!=experience.IsPractice)Fail(403,"作答模式不符。");
        var c=await LoadCase(conn,caseId,actor,false,false,ct,version);if(await Access(conn,c.CourseId,actor,false,ct)!="student")Fail(403,"僅學生本人可讀取私人學習紀錄。");
        var attempt=await ReadAttempt(conn,id,ct);await tx.CommitAsync(ct);return(c,attempt);
    }
    public async Task<StudioAttempt> Own(Guid caseId,Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        var c=await LoadCase(conn,caseId,actor,false,false,ct);if(await Access(conn,c.CourseId,actor,false,ct)!="student")Fail(403,"請從學生體驗或學生本人帳號進入案例作答。");
        if(!experience.IsPractice){await using var profile=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM studio_profiles p JOIN app_users u ON u.id=p.user_id WHERE p.user_id=$1 AND length(trim(u.display_name))>0 AND length(trim(u.student_number))>0)",conn);Add(profile,actor);if(await profile.ExecuteScalarAsync(ct) is not true)Fail(428,"請先完成基本資料。");}
        if(!experience.IsPractice){
            Guid? ongoing=null;await using(var existing=new NpgsqlCommand("SELECT a.id FROM studio_attempts a JOIN studio_case_versions v ON v.id=a.case_version_id WHERE v.case_id=$1 AND a.student_id=$2 AND NOT a.is_practice AND a.submitted_at IS NULL ORDER BY v.version_no DESC LIMIT 1 FOR UPDATE OF a",conn)){Add(existing,caseId);Add(existing,actor);if(await existing.ExecuteScalarAsync(ct) is Guid found)ongoing=found;}
            if(ongoing.HasValue){var previous=await ReadAttempt(conn,ongoing.Value,ct);await tx.CommitAsync(ct);return previous;}
        }
        Guid id;await using(var cmd=new NpgsqlCommand("INSERT INTO studio_attempts(case_version_id,student_id,is_practice) VALUES($1,$2,$3) ON CONFLICT(case_version_id,student_id,is_practice) DO UPDATE SET student_id=EXCLUDED.student_id RETURNING id",conn)){
            Add(cmd,c.VersionId);Add(cmd,actor);Add(cmd,experience.IsPractice);id=(Guid)(await cmd.ExecuteScalarAsync(ct))!;
        }
        var result=await ReadAttempt(conn,id,ct);await tx.CommitAsync(ct);return result;
    }
    private async Task<(StudioCase Case,StudioAttempt Attempt)> Writable(NpgsqlConnection conn,Guid id,Guid actor,CancellationToken ct)
    {
        Guid caseId,version;bool practice;
        await using(var cmd=new NpgsqlCommand("SELECT v.case_id,a.case_version_id,a.is_practice FROM studio_attempts a JOIN studio_case_versions v ON v.id=a.case_version_id WHERE a.id=$1 AND a.student_id=$2 FOR UPDATE OF a",conn)){
            Add(cmd,id);Add(cmd,actor);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new AssignmentWorkflowException(404,"找不到本人作答。");caseId=r.GetGuid(0);version=r.GetGuid(1);practice=r.GetBoolean(2);
        }
        if(practice!=experience.IsPractice)Fail(403,"作答模式不符。");
        var c=await LoadCase(conn,caseId,actor,false,true,ct,version);if(await Access(conn,c.CourseId,actor,false,ct)!="student")Fail(403,"僅學生本人可修改。");
        await using(var course=new NpgsqlCommand("SELECT status FROM courses WHERE id=$1 FOR SHARE",conn)){Add(course,c.CourseId);if(await course.ExecuteScalarAsync(ct) as string=="archived")Fail(409,"封存課程不能繼續作答。");}
        var attempt=await ReadAttempt(conn,id,ct);if(attempt.SubmittedAt!=null)Fail(409,"已提交的案例作答不可改寫。");return(c,attempt);
    }
    public async Task<StudioAttempt> Save(Guid id,Guid actor,StudioSaveInput input,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);var(c,a)=await Writable(conn,id,actor,ct);
        if(input.ExpectedVersion!=a.Version)Fail(409,"作答版本已更新，請先保留文字並重新載入。");
        if(input.Answers==null||input.Answers.Count!=4||StudioFeedbackContract.Stages.Any(s=>!input.Answers.ContainsKey(s))||input.Answers.Any(x=>x.Value==null||x.Value.Length>16000))Fail(400,"請提供四階段答案，每階段最多 16000 字元。");
        if(input.Submit && input.Answers!.Values.Any(string.IsNullOrWhiteSpace))Fail(400,"提交前請完成四階段作答。");
        foreach(var stage in StudioFeedbackContract.Stages){var prior=a.Answers.FirstOrDefault(x=>x.Stage==stage);var text=input.Answers![stage];if(prior?.Text==text || (prior==null&&text==""))continue;
            await using var cmd=new NpgsqlCommand("INSERT INTO studio_answer_versions(attempt_id,stage,revision_no,answer_text) VALUES($1,$2,$3,$4)",conn);Add(cmd,id);Add(cmd,stage);Add(cmd,(prior?.RevisionNo??0)+1);Add(cmd,text);await cmd.ExecuteNonQueryAsync(ct);
        }
        await using(var update=new NpgsqlCommand("UPDATE studio_attempts SET version=version+1,submitted_at=CASE WHEN $2 THEN now() ELSE NULL END WHERE id=$1",conn)){Add(update,id);Add(update,input.Submit);await update.ExecuteNonQueryAsync(ct);}
        if(input.Submit)await Audit(conn,actor,id,"studio.submitted",ct);
        var result=await ReadAttempt(conn,id,ct);await tx.CommitAsync(ct);return result;
    }
    public async Task<StudioFeedback> Ask(Guid id,Guid actor,Guid answerId,IOllamaClient model,OllamaOptions options,CancellationToken ct)
    {
        var run=Guid.NewGuid();IReadOnlyList<OllamaMessage> messages;int timeout;
        await using(var conn=await source.OpenConnectionAsync(ct))await using(var tx=await conn.BeginTransactionAsync(ct)){
            var(c,a)=await Writable(conn,id,actor,ct);var answer=a.Answers.FirstOrDefault(x=>x.Id==answerId)??throw new AssignmentWorkflowException(409,"找不到已儲存答案版本。");
            if(a.Answers.First(x=>x.Stage==answer.Stage).Id!=answerId||string.IsNullOrWhiteSpace(answer.Text))Fail(409,"只能請求目前已儲存的非空白答案回饋。");
            int max;await using(var settings=new NpgsqlCommand("SELECT feedback_enabled,timeout_seconds,max_requests_per_revision FROM studio_settings WHERE id=true",conn)){
                await using var r=await settings.ExecuteReaderAsync(ct);await r.ReadAsync(ct);if(!r.GetBoolean(0))Fail(409,"AI 回饋目前由管理者暫停；草稿仍可儲存或提交。");timeout=r.GetInt32(1);max=r.GetInt32(2);
            }
            // A crashed/disconnected request can be retried after its bounded lifetime.
            await using(var expire=new NpgsqlCommand("UPDATE studio_feedback_runs SET status='failed',error_code='interrupted',completed_at=now() WHERE answer_version_id=$1 AND status='pending' AND created_at<now()-interval '310 seconds'",conn)){Add(expire,answerId);await expire.ExecuteNonQueryAsync(ct);}
            await using(var check=new NpgsqlCommand("SELECT count(*),count(*) FILTER(WHERE status='pending') FROM studio_feedback_runs WHERE answer_version_id=$1",conn)){Add(check,answerId);await using var r=await check.ExecuteReaderAsync(ct);await r.ReadAsync(ct);if(r.GetInt64(1)>0)Fail(409,"此版本的 AI 回饋仍在處理中，請稍後重新載入。");if(r.GetInt64(0)>=max)Fail(429,"此答案版本已達回饋次數上限，請修訂並儲存後再試。");}
            Guid templateId;string template;await using(var t=new NpgsqlCommand("SELECT id,body FROM studio_templates ORDER BY version_no DESC LIMIT 1",conn)){await using var r=await t.ExecuteReaderAsync(ct);await r.ReadAsync(ct);templateId=r.GetGuid(0);template=r.GetString(1);}
            messages=StudioFeedbackContract.Build(c,answer,template);
            await using(var insert=new NpgsqlCommand("INSERT INTO studio_feedback_runs(id,answer_version_id,template_id,model_name,model_profile,prompt_version) VALUES($1,$2,$3,$4,$5,$6)",conn)){Add(insert,run);Add(insert,answerId);Add(insert,templateId);Add(insert,options.Model);Add(insert,options.Profile);Add(insert,StudioFeedbackContract.Version);await insert.ExecuteNonQueryAsync(ct);}
            await tx.CommitAsync(ct);
        }
        StudioFeedbackContent? content=null;string? error=null;
        using(var time=CancellationTokenSource.CreateLinkedTokenSource(ct)){
            time.CancelAfter(TimeSpan.FromSeconds(Math.Min(timeout,options.TimeoutSeconds)));try{var response=await model.FeedbackAsync(messages,time.Token);content=StudioFeedbackContract.Parse(response.Content);}catch(OperationCanceledException){error="timeout";}catch(Exception e)when(e is HttpRequestException or InvalidOperationException){error="unavailable";}
        }
        using var completion=new CancellationTokenSource(TimeSpan.FromSeconds(10));await using(var conn=await source.OpenConnectionAsync(completion.Token)){
            await using var cmd=new NpgsqlCommand("UPDATE studio_feedback_runs SET status=$2,findings=$3::jsonb,error_code=$4,completed_at=now() WHERE id=$1 RETURNING created_at,completed_at",conn);Add(cmd,run);Add(cmd,content==null?"failed":"completed");cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlDbType.Text,Value=content==null?DBNull.Value:JsonSerializer.Serialize(content,Json)});cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlDbType.Text,Value=(object?)error??DBNull.Value});await using var r=await cmd.ExecuteReaderAsync(completion.Token);await r.ReadAsync(completion.Token);return new(run,answerId,content==null?"failed":"completed",content,error,r.GetFieldValue<DateTimeOffset>(0),r.GetFieldValue<DateTimeOffset>(1));
        }
    }
    public static async Task Audit(NpgsqlConnection conn,Guid actor,Guid entity,string action,CancellationToken ct)
    {await using var cmd=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action) VALUES($1,'case_studio',$2,$3)",conn);Add(cmd,actor);Add(cmd,entity);Add(cmd,action);await cmd.ExecuteNonQueryAsync(ct);}
}
