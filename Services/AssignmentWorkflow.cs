using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiTeachingAssistant.Models;
using Npgsql;
using NpgsqlTypes;
namespace AiTeachingAssistant.Services;

public sealed partial class AssignmentWorkflow(NpgsqlDataSource source, TimeProvider clock,LearnerExperience? experience=null)
{
    private bool Practice=>experience?.IsPractice==true;
    private static void Fail(int status, string message) => throw new AssignmentWorkflowException(status, message);
    private static void Add(NpgsqlCommand cmd, object value) => cmd.Parameters.AddWithValue(value);
    private static void Date(NpgsqlCommand cmd, DateTimeOffset? value) => cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = value.HasValue ? value.Value.UtcDateTime : DBNull.Value });
    private static DateTimeOffset? ReadDate(NpgsqlDataReader r, int index) => r.IsDBNull(index) ? null : r.GetFieldValue<DateTimeOffset>(index);
    private async Task<string> CourseRole(NpgsqlConnection conn, Guid course, Guid actor, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT course_role FROM course_members WHERE course_id=$1 AND user_id=$2 AND status='active'", conn);
        Add(cmd, course); Add(cmd, actor);
        var role=await cmd.ExecuteScalarAsync(ct) as string ?? throw new AssignmentWorkflowException(403, "你不是本課程的有效成員。");
        return Practice?"student":role;
    }
    internal static void Validate(SaveAssignmentRequest request)
    {
        if(request.LearningRequirements==null||request.LearningRequirements.Any(x=>x==null))Fail(400,"教材學習門檻格式無效。");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Questions is null || request.Questions.Count is < 1 or > 20 || request.Questions.Any(q => q is null || string.IsNullOrWhiteSpace(q.PromptText))) Fail(400, "請填寫作業名稱與 1 至 20 道題目。");
        if (request.OpensAt.HasValue && request.DueAt.HasValue && request.DueAt <= request.OpensAt) Fail(400, "截止時間必須晚於開放時間。");
        foreach (var q in request.Questions!) {QuestionPolicy.Validate(q);if(q.GradingGuidance==null||q.GradingGuidance.Length>8000)Fail(400,"教師評分參考不可超過 8000 字元。");}
        QuizGrading.Allocate(request.MaxScore,request.Questions!.Select(q=>q.Points).ToArray());
        foreach (var q in request.Questions!)
            if (!string.IsNullOrWhiteSpace(q.StageCode) && (!AnalysisStageCatalog.TryParse(q.StageCode, out var stage) || stage == AnalysisStage.GeneralChat)) Fail(400, "題目的分析階段無效。");
    }
    private static async Task Audit(NpgsqlConnection conn, Guid actor, Guid entity, string type, string action, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action) VALUES($1,$2,$3,$4)", conn);
        Add(cmd, actor); Add(cmd, type); Add(cmd, entity); Add(cmd, action); await cmd.ExecuteNonQueryAsync(ct);
    }
    private async Task<AssignmentDetail> Load(NpgsqlConnection conn, Guid id, Guid actor, bool forUpdate, CancellationToken ct)
    {
        AssignmentDetail a;
        bool requiresStart;
        await using (var cmd = new NpgsqlCommand("""
            SELECT a.id,a.course_id,c.title,m.course_role,c.status,a.title,a.instructions,a.status,a.opens_at,a.due_at,a.submission_mode,a.allow_revisions,a.max_score,a.rubric_id,EXISTS(SELECT 1 FROM course_grade_items i WHERE i.source_assignment_id=a.id),a.time_limit_minutes,a.requires_start,(SELECT started_at FROM quiz_learning_starts WHERE assignment_id=a.id AND user_id=$2 AND is_practice=$3),a.notify_students
            FROM assignments a JOIN courses c ON c.id=a.course_id
            LEFT JOIN course_members m ON m.course_id=c.id AND m.user_id=$2 AND m.status='active'
            WHERE a.id=$1
            """ + (forUpdate ? " FOR UPDATE OF a" : ""), conn))
        {
            Add(cmd, id); Add(cmd, actor);Add(cmd,Practice);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) throw new AssignmentWorkflowException(404, "找不到作業。");
            if (r.IsDBNull(3)) throw new AssignmentWorkflowException(403, "你不是本課程的有效成員。");
            var role = Practice?"student":r.GetString(3); var status = r.GetString(7);
            if (!AssignmentPolicy.IsStaff(role) && status == "draft") throw new AssignmentWorkflowException(404, "找不到已發布的作業。");
            if (r.GetString(10) != "individual") throw new AssignmentWorkflowException(409, "本階段只支援個人作業。");
            requiresStart=r.GetBoolean(16);
            a = new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), role, r.GetString(4), r.GetString(5), r.GetString(6), status, ReadDate(r, 8), ReadDate(r, 9), [], "",r.GetBoolean(11),r.GetDecimal(12),r.IsDBNull(13)?null:r.GetGuid(13),false,r.GetBoolean(14),null,r.IsDBNull(15)?null:r.GetInt32(15),ReadDate(r,17),r.GetBoolean(18));
        }
        var gate=await LearningTime.Gate(conn,id,actor,Practice,ct);
        gate=gate with{RequiresStart=requiresStart&&!Practice};
        a=a with{LearningGate=gate};
        var questions = new List<AssignmentQuestion>();
        if(!AssignmentPolicy.IsStaff(a.CourseRole)&&(gate.Locked || a.OpensAt>clock.GetUtcNow()))
            return a with{EditVersion="",IsPractice=Practice};
        await using (var cmd = new NpgsqlCommand("SELECT id,prompt_text,stage_code,sort_order,question_type,options::text,standard_answer::text,max_score,grading_guidance,section_title FROM assignment_questions WHERE assignment_id=$1 ORDER BY sort_order", conn))
        {
            Add(cmd, id); await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) questions.Add(new(r.GetGuid(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetInt32(3),r.GetString(4),JsonSerializer.Deserialize<List<string>>(r.GetString(5)),AssignmentPolicy.IsStaff(a.CourseRole)?JsonSerializer.Deserialize<List<int>>(r.GetString(6)):null,r.IsDBNull(7)?null:r.GetDecimal(7),AssignmentPolicy.IsStaff(a.CourseRole)?r.GetString(8):null,r.GetString(9)));
        }
        a = a with { Questions = questions, IsPractice=Practice };
        return a with { EditVersion = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(a)))) };
    }
    public async Task<AssignmentDetail> Get(Guid id, Guid actor, CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        return await Load(conn, id, actor, false, ct);
    }
    public async Task<IReadOnlyList<AssignmentListItem>> List(Guid course, Guid actor, CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct); var role = await CourseRole(conn, course, actor, ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT a.id,a.title,a.status,a.opens_at,a.due_at,
              (SELECT count(*)::int FROM assignment_questions WHERE assignment_id=a.id),
              (SELECT status FROM submissions WHERE assignment_id=a.id AND student_user_id=$2 AND attempt_no=1 AND is_practice=$4),
              CASE WHEN $3 THEN (SELECT count(*)::int FROM submissions WHERE assignment_id=a.id AND status='submitted' AND NOT is_practice) ELSE 0 END
            FROM assignments a WHERE a.course_id=$1 AND a.submission_mode='individual' AND ($3 OR a.status IN ('published','closed')) ORDER BY a.created_at DESC
            """, conn);
        Add(cmd, course); Add(cmd, actor); Add(cmd, AssignmentPolicy.IsStaff(role));Add(cmd,Practice);
        await using var r = await cmd.ExecuteReaderAsync(ct); var items = new List<AssignmentListItem>();
        while (await r.ReadAsync(ct)) items.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),ReadDate(r,3),ReadDate(r,4),r.GetInt32(5),r.IsDBNull(6)?null:r.GetString(6),r.GetInt32(7)));
        return items;
    }
    internal static async Task WriteQuestions(NpgsqlConnection conn, Guid id, SaveAssignmentRequest request, CancellationToken ct)
    {
        for (var i=0; i<request.Questions.Count; i++)
        {
            var q=request.Questions[i];
            await using var cmd=new NpgsqlCommand("INSERT INTO assignment_questions(assignment_id,prompt_text,stage_code,sort_order,question_type,options,standard_answer,max_score,grading_guidance,section_title) VALUES($1,$2,$3,$4,$5,$6::jsonb,$7::jsonb,$8,$9,$10)",conn);
            Add(cmd,id);Add(cmd,q.PromptText.Trim());cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Text,Value=string.IsNullOrWhiteSpace(q.StageCode)?DBNull.Value:q.StageCode });Add(cmd,i+1);Add(cmd,q.QuestionType);Add(cmd,JsonSerializer.Serialize(q.QuestionType=="true_false"?new List<string>{"是","非"}:q.Options));Add(cmd,JsonSerializer.Serialize(q.StandardAnswer));cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlDbType.Numeric,Value=(object?)q.Points??DBNull.Value});Add(cmd,q.GradingGuidance);Add(cmd,q.SectionTitle.Trim());await cmd.ExecuteNonQueryAsync(ct);
        }
    }
    public async Task<AssignmentDetail> Create(Guid course, Guid actor, SaveAssignmentRequest request, CancellationToken ct)
    {
        if(request.LearningRequirements is {Count:>0})Fail(400,"教材時間門檻僅適用線上測驗。");
        Validate(request); await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        AssignmentPolicy.RequireStaff(await CourseRole(conn,course,actor,ct));
        await using(var guard=new NpgsqlCommand("SELECT status FROM courses WHERE id=$1 FOR SHARE",conn)) {Add(guard,course);if(await guard.ExecuteScalarAsync(ct) as string=="archived")Fail(409,"封存課程無法新增作業。");}
        var id=Guid.NewGuid();
        await using(var cmd=new NpgsqlCommand("INSERT INTO assignments(id,course_id,title,instructions,opens_at,due_at) VALUES($1,$2,$3,$4,$5,$6)",conn))
        {Add(cmd,id);Add(cmd,course);Add(cmd,request.Title.Trim());Add(cmd,request.Instructions);Date(cmd,request.OpensAt);Date(cmd,request.DueAt);await cmd.ExecuteNonQueryAsync(ct);}
        await WriteTeachingOptions(conn,id,course,request,ct);
        await WriteQuestions(conn,id,request,ct);await Audit(conn,actor,id,"assignments","assignment.created",ct);
        var result=await Load(conn,id,actor,false,ct);await tx.CommitAsync(ct);return result;
    }
    public async Task<AssignmentDetail> Update(Guid id, Guid actor, SaveAssignmentRequest request, CancellationToken ct)
    {
        Validate(request);await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        var a=await Load(conn,id,actor,true,ct);AssignmentPolicy.RequireStaff(a.CourseRole);
        if(a.Status!="draft" || a.CourseStatus=="archived")Fail(409,"只可編輯未發布的作業草稿。");
        if(a.IsQuiz && (request.AllowRevisions || request.RubricId.HasValue))Fail(400,"線上測驗採一次提交與直接人工評分。");
        if(request.ExpectedEditVersion!=a.EditVersion)Fail(409,"作業已被其他人修改，請重新載入後再編輯。");
        await using(var guard=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM submissions WHERE assignment_id=$1)",conn)) {Add(guard,id);if(await guard.ExecuteScalarAsync(ct) is true)Fail(409,"已有作答紀錄，無法變更題目。");}
        await using(var cmd=new NpgsqlCommand("UPDATE assignments SET title=$2,instructions=$3,opens_at=$4,due_at=$5,time_limit_minutes=$6,requires_start=(requires_start OR $6 IS NOT NULL),notify_students=$7 WHERE id=$1",conn))
        {Add(cmd,id);Add(cmd,request.Title.Trim());Add(cmd,request.Instructions);Date(cmd,request.OpensAt);Date(cmd,request.DueAt);cmd.Parameters.AddWithValue(NpgsqlDbType.Integer,(object?)request.TimeLimitMinutes??DBNull.Value);Add(cmd,request.NotifyStudents);await cmd.ExecuteNonQueryAsync(ct);}
        await using(var delete=new NpgsqlCommand("DELETE FROM assignment_questions WHERE assignment_id=$1",conn)){Add(delete,id);await delete.ExecuteNonQueryAsync(ct);}
        await WriteTeachingOptions(conn,id,a.CourseId,request,ct);
        if(!a.IsQuiz&&request.LearningRequirements.Count>0)Fail(400,"教材時間門檻僅適用線上測驗。");
        await LearningTime.SaveRequirements(conn,id,a.CourseId,request.LearningRequirements,ct);
        await WriteQuestions(conn,id,request,ct);
        if(a.IsQuiz){await using var sync=new NpgsqlCommand("UPDATE course_grade_items SET title=$2,max_score=$3,version=version+1 WHERE source_assignment_id=$1",conn);Add(sync,id);Add(sync,request.Title.Trim());Add(sync,request.MaxScore);await sync.ExecuteNonQueryAsync(ct);}
        await Audit(conn,actor,id,"assignments","assignment.updated",ct);
        var result=await Load(conn,id,actor,false,ct);await tx.CommitAsync(ct);return result;
    }
    public async Task<AssignmentDetail> Transition(Guid id, Guid actor, string target, CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        var a=await Load(conn,id,actor,true,ct);AssignmentPolicy.RequireStaff(a.CourseRole);
        if(a.CourseStatus=="archived" || (target=="published"?a.Status!="draft" || a.Questions.Count==0:a.Status!="published"))Fail(409,"作業狀態已變更，請重新載入。");
        await using(var cmd=new NpgsqlCommand("UPDATE assignments SET status=$2 WHERE id=$1",conn)){Add(cmd,id);Add(cmd,target);await cmd.ExecuteNonQueryAsync(ct);}
        if(target=="published" && a.RubricId.HasValue)
        { await using var freeze=new NpgsqlCommand("UPDATE rubrics SET status='published' WHERE id=$1",conn);Add(freeze,a.RubricId.Value);await freeze.ExecuteNonQueryAsync(ct); }
        await Audit(conn,actor,id,"assignments","assignment."+target,ct);
        var result=await Load(conn,id,actor,false,ct);await tx.CommitAsync(ct);return result;
    }
    private async Task<SubmissionDetail> LoadSubmission(NpgsqlConnection conn, AssignmentDetail a, Guid student, CancellationToken ct)
    {
        Guid? id=null;string status="not_started",name="";DateTimeOffset? submitted=null;
        await using(var cmd=new NpgsqlCommand("SELECT s.id,s.status,s.submitted_at,u.display_name FROM submissions s JOIN app_users u ON u.id=s.student_user_id WHERE s.assignment_id=$1 AND s.student_user_id=$2 AND s.attempt_no=1 AND s.is_practice=$3",conn))
        {Add(cmd,a.Id);Add(cmd,student);Add(cmd,Practice);await using var r=await cmd.ExecuteReaderAsync(ct);if(await r.ReadAsync(ct)){id=r.GetGuid(0);status=r.GetString(1);submitted=ReadDate(r,2);name=r.GetString(3);}}
        var answers=new List<SubmissionAnswer>();var version=0;
        foreach(var q in a.Questions)
        {
            var revisions=new List<AnswerRevision>();Guid? current=null;string text="";
            if(id.HasValue)
            {
                await using var cmd=new NpgsqlCommand("SELECT r.id,r.revision_no,r.answer_text,r.created_at,r.is_submitted,sa.current_revision_id FROM submission_answers sa JOIN answer_revisions r ON r.answer_id=sa.id WHERE sa.submission_id=$1 AND sa.question_id=$2 ORDER BY r.revision_no DESC",conn);
                Add(cmd,id.Value);Add(cmd,q.Id);await using var r=await cmd.ExecuteReaderAsync(ct);
                while(await r.ReadAsync(ct))
                {
                    var revision=new AnswerRevision(r.GetGuid(0),r.GetInt32(1),r.GetString(2),r.GetFieldValue<DateTimeOffset>(3),r.GetBoolean(4));revisions.Add(revision);
                    if(!r.IsDBNull(5) && r.GetGuid(5)==revision.Id){current=revision.Id;text=revision.Text;version+=revision.RevisionNo;}
                }
            }
            answers.Add(new(q.Id,text,current,revisions));
        }
        return new(id,a.Id,student,name,status,version,submitted,answers);
    }
    public async Task<SubmissionDetail> Own(Guid id,Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);var a=await Load(conn,id,actor,false,ct);
        if(a.CourseRole!="student")Fail(403,"此頁只提供課程學生查看自己的作答。");
        if(Practice)return new(null,id,actor,"","not_started",0,null,a.Questions.Select(q=>new SubmissionAnswer(q.Id,"",null,[])).ToArray());
        return await LoadSubmission(conn,a,actor,ct);
    }
    public async Task<SubmissionDetail> Save(Guid id,Guid actor,SaveSubmissionRequest request,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        // Serializes assignment state changes and saves; the version prevents stale-tab overwrites.
        var a=await Load(conn,id,actor,true,ct);
        AssignmentPolicy.RequireWritable(a.CourseRole,a.Status,a.CourseStatus,a.OpensAt,a.DueAt,clock.GetUtcNow());
        if(Practice)Fail(403,"學生視角預覽無法儲存作答。");
        if(a.LearningGate?.Locked==true)Fail(409,"請先開始測驗。");
        if(a.TimeLimitMinutes is int minutes && (a.StartedAt==null || clock.GetUtcNow()>=a.StartedAt.Value.AddMinutes(minutes)))
            Fail(409,"測驗作答時間已截止。");
        if(request.Answers is null || request.Answers.Count!=a.Questions.Count || request.Answers.Select(x=>x.QuestionId).Distinct().Count()!=a.Questions.Count || request.Answers.Any(x=>!a.Questions.Any(q=>q.Id==x.QuestionId)))Fail(400,"作答必須對應本作業的全部題目，不能含其他作業題目。");
        if(request.Submit && request.Answers!.Any(x=>string.IsNullOrWhiteSpace(x.Text)&&(!a.IsQuiz||a.Questions.Single(q=>q.Id==x.QuestionId).QuestionType=="essay")))Fail(400,"提交前請完成每一道題目。");
        foreach(var input in request.Answers!) QuestionPolicy.ValidateAnswer(a.Questions.Single(q=>q.Id==input.QuestionId),input.Text,request.Submit);
        var prior=await LoadSubmission(conn,a,actor,ct);
        if(prior.Status == "withdrawn" || (prior.Status == "submitted" && !a.AllowRevisions))Fail(409,"此份作答已提交或撤回，無法再修改。");
        if(prior.Status=="submitted" && !request.Submit)Fail(409,"修訂已提交作答時請直接提交新版本；原提交內容不會被草稿覆寫。");
        if(prior.Version!=request.ExpectedVersion)Fail(409,"作答已在其他頁面更新。請先保留目前文字，再重新載入最新版本。");
        var submission=prior.Id??Guid.NewGuid();
        if(prior.Id is null)
        {
            await using var cmd=new NpgsqlCommand("INSERT INTO submissions(id,assignment_id,student_user_id,attempt_no,is_practice) VALUES($1,$2,$3,1,$4)",conn);
            Add(cmd,submission);Add(cmd,id);Add(cmd,actor);Add(cmd,Practice);await cmd.ExecuteNonQueryAsync(ct);
        }
        foreach(var input in request.Answers!)
        {
            var old=prior.Answers.Single(x=>x.QuestionId==input.QuestionId);
            if(prior.Status!="submitted" && old.CurrentRevisionId.HasValue && old.Text==input.Text)continue;
            var answerId=Guid.NewGuid();
            await using(var cmd=new NpgsqlCommand("INSERT INTO submission_answers(id,submission_id,assignment_id,question_id) VALUES($1,$2,$3,$4) ON CONFLICT(submission_id,question_id) DO UPDATE SET question_id=EXCLUDED.question_id RETURNING id",conn))
            {Add(cmd,answerId);Add(cmd,submission);Add(cmd,id);Add(cmd,input.QuestionId);answerId=(Guid)(await cmd.ExecuteScalarAsync(ct))!;}
            var revisionId=Guid.NewGuid();var number=old.Revisions.Count==0?1:old.Revisions.Max(r=>r.RevisionNo)+1;
            await using(var cmd=new NpgsqlCommand("INSERT INTO answer_revisions(id,answer_id,revision_no,answer_text,created_by_user_id) VALUES($1,$2,$3,$4,$5)",conn))
            {Add(cmd,revisionId);Add(cmd,answerId);Add(cmd,number);Add(cmd,input.Text);Add(cmd,actor);await cmd.ExecuteNonQueryAsync(ct);}
            await using(var cmd=new NpgsqlCommand("UPDATE submission_answers SET current_revision_id=$2 WHERE id=$1",conn)){Add(cmd,answerId);Add(cmd,revisionId);await cmd.ExecuteNonQueryAsync(ct);}
        }
        if(request.Submit)
        {
            await using(var cmd=new NpgsqlCommand("UPDATE answer_revisions SET is_submitted=true WHERE id IN (SELECT current_revision_id FROM submission_answers WHERE submission_id=$1)",conn)){Add(cmd,submission);await cmd.ExecuteNonQueryAsync(ct);}
            await using(var cmd=new NpgsqlCommand("UPDATE submissions SET status='submitted',submitted_at=now() WHERE id=$1",conn)){Add(cmd,submission);await cmd.ExecuteNonQueryAsync(ct);}
            await CaptureSnapshot(conn,submission,actor,ct);
        }
        await Audit(conn,actor,submission,"submissions",request.Submit?"submission.submitted":"submission.saved",ct);
        var result=await LoadSubmission(conn,a,actor,ct);await tx.CommitAsync(ct);return result;
    }
    public async Task<IReadOnlyList<SubmissionListItem>> Submitted(Guid id,Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);var a=await Load(conn,id,actor,false,ct);AssignmentPolicy.RequireStaff(a.CourseRole);
        await using var cmd=new NpgsqlCommand("""
            SELECT s.id,s.student_user_id,u.display_name,s.submitted_at,
              (SELECT count(*)::int FROM submission_answers WHERE submission_id=s.id),
              COALESCE((SELECT CASE WHEN g.released_at IS NOT NULL THEN 'released' WHEN g.grader_user_id IS NULL THEN 'auto_scored' ELSE 'draft' END FROM grades g
                WHERE g.submission_revision_id=(SELECT id FROM submission_revisions WHERE submission_id=s.id ORDER BY revision_no DESC LIMIT 1)
                ORDER BY g.graded_at DESC,g.id DESC LIMIT 1),CASE WHEN EXISTS(SELECT 1 FROM quiz_grading qg JOIN submission_revisions r ON r.id=qg.snapshot_id WHERE r.submission_id=s.id) THEN 'pending_essay' ELSE 'ungraded' END)
            FROM submissions s JOIN app_users u ON u.id=s.student_user_id WHERE s.assignment_id=$1 AND s.status='submitted' AND NOT s.is_practice ORDER BY s.submitted_at DESC
            """,conn);Add(cmd,id);
        await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<SubmissionListItem>();
        while(await r.ReadAsync(ct))result.Add(new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetFieldValue<DateTimeOffset>(3),r.GetInt32(4),r.GetString(5)));return result;
    }
    public async Task<IReadOnlyList<object>> ClassRoster(Guid id,Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);
        var assignment=await Load(conn,id,actor,false,ct);
        AssignmentPolicy.RequireStaff(assignment.CourseRole);
        await using var cmd=new NpgsqlCommand("""
            SELECT u.id,u.student_number,u.display_name,s.id,s.submitted_at,g.score,g.released_at,
                   (SELECT sum((mark->>'autoScore')::numeric) FROM jsonb_array_elements(q.findings) mark),
                   EXISTS(SELECT 1 FROM jsonb_array_elements(q.findings) mark WHERE mark->>'questionType'='essay'),
                   start.started_at
            FROM course_members m JOIN app_users u ON u.id=m.user_id AND u.status='active'
            LEFT JOIN submissions s ON s.assignment_id=$1 AND s.student_user_id=u.id AND s.status='submitted' AND NOT s.is_practice AND s.attempt_no=1
            LEFT JOIN LATERAL (SELECT id FROM submission_revisions WHERE submission_id=s.id ORDER BY revision_no DESC LIMIT 1) r ON true
            LEFT JOIN quiz_grading q ON q.snapshot_id=r.id
            LEFT JOIN LATERAL (SELECT score,released_at FROM grades WHERE submission_revision_id=r.id ORDER BY graded_at DESC,id DESC LIMIT 1) g ON true
            LEFT JOIN quiz_learning_starts start ON start.assignment_id=$1 AND start.user_id=u.id AND NOT start.is_practice
            WHERE m.course_id=$2 AND m.course_role='student' AND m.status='active'
            ORDER BY u.student_number NULLS LAST,u.display_name,u.id
            """,conn);
        Add(cmd,id);Add(cmd,assignment.CourseId);
        var rows=new List<object>();
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct)){
            var submitted=ReadDate(reader,4);var start=ReadDate(reader,9);
            rows.Add(new{studentId=reader.GetGuid(0),studentNumber=reader.IsDBNull(1)?null:reader.GetString(1),studentName=reader.GetString(2),
                id=reader.IsDBNull(3)?(Guid?)null:reader.GetGuid(3),submittedAt=submitted,total=reader.IsDBNull(5)?(decimal?)null:reader.GetDecimal(5),
                released=!reader.IsDBNull(6),objective=reader.IsDBNull(7)?(decimal?)null:reader.GetDecimal(7),hasEssays=reader.GetBoolean(8),
                elapsedSeconds=(long?)(submitted-start)?.TotalSeconds});
        }
        return rows;
    }
    public async Task<SubmissionDetail> Review(Guid id,Guid submission,Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);var a=await Load(conn,id,actor,false,ct);AssignmentPolicy.RequireStaff(a.CourseRole);
        await using var cmd=new NpgsqlCommand("SELECT student_user_id FROM submissions WHERE id=$1 AND assignment_id=$2 AND status='submitted' AND student_user_id IS NOT NULL AND NOT is_practice",conn);Add(cmd,submission);Add(cmd,id);
        if(await cmd.ExecuteScalarAsync(ct) is not Guid student)throw new AssignmentWorkflowException(404,"找不到已提交的作答。");
        return await LoadSubmission(conn,a,student,ct);
    }
}
