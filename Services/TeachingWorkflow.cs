using System.Text.Json;
using AiTeachingAssistant.Models;
using Npgsql;
using NpgsqlTypes;
namespace AiTeachingAssistant.Services;

public sealed partial class AssignmentWorkflow
{
    private static readonly JsonSerializerOptions TeachingJson=new(JsonSerializerDefaults.Web);
    private static void OptionalGuid(NpgsqlCommand cmd,Guid? value)=>cmd.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=NpgsqlDbType.Uuid,Value=value.HasValue?value.Value:DBNull.Value});
    private static async Task WriteTeachingOptions(NpgsqlConnection conn,Guid id,Guid course,SaveAssignmentRequest request,CancellationToken ct)
    {
        if(request.MaxScore<=0 || request.MaxScore>999999 || Math.Round(request.MaxScore,2)!=request.MaxScore)Fail(400,"作業滿分必須為正數，最多兩位小數。");
        if(request.RubricId.HasValue)
        {
            await using var check=new NpgsqlCommand("SELECT id FROM rubrics WHERE id=$1 AND course_id=$2 AND status<>'retired' FOR UPDATE",conn);
            Add(check,request.RubricId.Value);Add(check,course);
            if(await check.ExecuteScalarAsync(ct) is not Guid)Fail(400,"規準必須屬於本課程且未停用。");
        }
        await using var cmd=new NpgsqlCommand("UPDATE assignments SET allow_revisions=$2,max_score=$3,rubric_id=$4 WHERE id=$1",conn);
        Add(cmd,id);Add(cmd,request.AllowRevisions);Add(cmd,request.MaxScore);OptionalGuid(cmd,request.RubricId);await cmd.ExecuteNonQueryAsync(ct);
    }
    private static async Task CaptureSnapshot(NpgsqlConnection conn,Guid submission,Guid actor,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            INSERT INTO submission_revisions(submission_id,revision_no,author_user_id,answers)
            SELECT $1,COALESCE((SELECT max(revision_no) FROM submission_revisions WHERE submission_id=$1),0)+1,$2,
              jsonb_agg(jsonb_build_object('questionId',sa.question_id,'revisionId',r.id,'text',r.answer_text) ORDER BY q.sort_order)
            FROM submission_answers sa JOIN assignment_questions q ON q.id=sa.question_id
            JOIN answer_revisions r ON r.id=sa.current_revision_id WHERE sa.submission_id=$1 RETURNING id
            """,conn);
        Add(cmd,submission);Add(cmd,actor);var snapshot=(Guid)(await cmd.ExecuteScalarAsync(ct))!;
        await using var find=new NpgsqlCommand("SELECT assignment_id,is_practice FROM submissions WHERE id=$1",conn);Add(find,submission);Guid assignment;bool practice;await using(var r=await find.ExecuteReaderAsync(ct)){await r.ReadAsync(ct);assignment=r.GetGuid(0);practice=r.GetBoolean(1);}
        await QuizGrading.Capture(conn,assignment,snapshot,practice,ct);
    }
    private async Task<(Guid Submission,Guid Student)> SnapshotAccess(NpgsqlConnection conn,AssignmentDetail a,Guid snapshot,Guid actor,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("SELECT s.id,s.student_user_id FROM submission_revisions r JOIN submissions s ON s.id=r.submission_id WHERE r.id=$1 AND s.assignment_id=$2 AND s.is_practice=$3",conn);
        Add(cmd,snapshot);Add(cmd,a.Id);Add(cmd,Practice);await using var reader=await cmd.ExecuteReaderAsync(ct);
        if(!await reader.ReadAsync(ct))throw new AssignmentWorkflowException(404,"找不到提交版本。");
        var student=reader.GetGuid(1);
        if(!AssignmentPolicy.IsStaff(a.CourseRole) && (a.CourseRole!="student" || actor!=student))Fail(403,"無權查看此提交版本。");
        return(reader.GetGuid(0),student);
    }
    public async Task<IReadOnlyList<SubmissionSnapshot>> Snapshots(Guid assignment,Guid submission,Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);var a=await Load(conn,assignment,actor,false,ct);
        await using(var check=new NpgsqlCommand("SELECT student_user_id FROM submissions WHERE id=$1 AND assignment_id=$2 AND is_practice=$3",conn))
        {Add(check,submission);Add(check,assignment);Add(check,Practice);if(await check.ExecuteScalarAsync(ct) is not Guid owner)throw new AssignmentWorkflowException(404,"找不到作答。");else if(!AssignmentPolicy.IsStaff(a.CourseRole) && owner!=actor)Fail(403,"只能查看自己的作答。");}
        await using var cmd=new NpgsqlCommand("SELECT id,revision_no,author_user_id,submitted_at,answers::text FROM submission_revisions WHERE submission_id=$1 ORDER BY revision_no DESC",conn);Add(cmd,submission);
        await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<SubmissionSnapshot>();
        while(await r.ReadAsync(ct))result.Add(new(r.GetGuid(0),r.GetInt32(1),r.GetGuid(2),r.GetFieldValue<DateTimeOffset>(3),JsonSerializer.Deserialize<List<SnapshotAnswer>>(r.GetString(4),TeachingJson)!));
        return result;
    }
    private static async Task<TeachingRubric> ReadRubric(NpgsqlConnection conn,Guid id,Guid course,CancellationToken ct)
    {
        Guid family;int version;string title,status;
        await using(var cmd=new NpgsqlCommand("SELECT family_id,version_no,title,status FROM rubrics WHERE id=$1 AND course_id=$2",conn))
        {Add(cmd,id);Add(cmd,course);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new AssignmentWorkflowException(404,"找不到課程規準。");family=r.GetGuid(0);version=r.GetInt32(1);title=r.GetString(2);status=r.GetString(3);}
        var criteria=new List<RubricCriterion>();
        await using(var cmd=new NpgsqlCommand("SELECT c.id,c.title,c.description,c.weight,array_agg(l.description ORDER BY l.level_no) FROM rubric_criteria c JOIN rubric_levels l ON l.criterion_id=c.id WHERE c.rubric_id=$1 GROUP BY c.id ORDER BY c.sort_order",conn))
        {Add(cmd,id);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))criteria.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetDecimal(3),r.GetFieldValue<string[]>(4)));}
        return new(id,course,family,version,title,status,criteria);
    }
    public async Task<IReadOnlyList<TeachingRubric>> Rubrics(Guid course,Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);AssignmentPolicy.RequireStaff(await CourseRole(conn,course,actor,ct));
        var ids=new List<Guid>();await using(var cmd=new NpgsqlCommand("SELECT id FROM rubrics WHERE course_id=$1 ORDER BY created_at DESC,id",conn))
        {Add(cmd,course);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))ids.Add(r.GetGuid(0));}
        var result=new List<TeachingRubric>();foreach(var id in ids)result.Add(await ReadRubric(conn,id,course,ct));return result;
    }
    public async Task<TeachingRubric?> AssignmentRubric(Guid assignment,Guid actor,CancellationToken ct)
    {await using var conn=await source.OpenConnectionAsync(ct);var a=await Load(conn,assignment,actor,false,ct);return a.RubricId.HasValue?await ReadRubric(conn,a.RubricId.Value,a.CourseId,ct):null;}
    public async Task<TeachingRubric> SaveRubric(Guid course,Guid? existing,Guid actor,SaveRubricRequest request,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(request.Title) || request.Criteria.Count is <1 or >20 || request.Criteria.Any(c=>string.IsNullOrWhiteSpace(c.Title)||c.Weight<0.001m||c.Weight>1000||Math.Round(c.Weight,3)!=c.Weight||c.Levels.Count!=4||c.Levels.Any(l=>string.IsNullOrWhiteSpace(l)||l.Length>4000)))Fail(400,"規準需有名稱、正權重（最多三位小數）及每項四個非空等級描述。");
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        AssignmentPolicy.RequireStaff(await CourseRole(conn,course,actor,ct));
        // Serializes all rubric version edits within a course, including simultaneous forks.
        await using(var gate=new NpgsqlCommand("SELECT id FROM courses WHERE id=$1 AND status<>'archived' FOR UPDATE",conn)){Add(gate,course);if(await gate.ExecuteScalarAsync(ct) is not Guid)Fail(409,"課程已封存。");}
        Guid id=Guid.NewGuid(),family=Guid.NewGuid();var version=1;
        if(existing.HasValue)
        {
            await using(var gate=new NpgsqlCommand("SELECT id FROM rubrics WHERE id=$1 AND course_id=$2 FOR UPDATE",conn))
            {Add(gate,existing.Value);Add(gate,course);if(await gate.ExecuteScalarAsync(ct) is not Guid)Fail(404,"找不到課程規準。");}
            var prior=await ReadRubric(conn,existing.Value,course,ct);family=prior.FamilyId;
            if(request.ExpectedVersion!=prior.VersionNo)Fail(409,"規準版本已改變，請重新載入。");
            await using var check=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM assignments WHERE rubric_id=$1) OR EXISTS(SELECT 1 FROM grades WHERE rubric_id=$1) OR $2",conn);
            Add(check,existing.Value);Add(check,prior.Status!="draft");var used=await check.ExecuteScalarAsync(ct) is true;
            if(!used)
            {
                id=existing.Value;version=prior.VersionNo+1;
                await using var delete=new NpgsqlCommand("DELETE FROM rubric_criteria WHERE rubric_id=$1",conn);Add(delete,id);await delete.ExecuteNonQueryAsync(ct);
                await using var update=new NpgsqlCommand("UPDATE rubrics SET title=$2,version_no=$3 WHERE id=$1",conn);Add(update,id);Add(update,request.Title.Trim());Add(update,version);await update.ExecuteNonQueryAsync(ct);
            }
            else
            {
                await using var latest=new NpgsqlCommand("SELECT max(version_no) FROM rubrics WHERE family_id=$1",conn);Add(latest,family);version=(int)(await latest.ExecuteScalarAsync(ct))!+1;
                await InsertRubric(conn,id,course,family,version,actor,request.Title.Trim(),ct);
            }
        }
        else await InsertRubric(conn,id,course,family,version,actor,request.Title.Trim(),ct);
        for(var i=0;i<request.Criteria.Count;i++)
        {
            var c=request.Criteria[i];var criterion=Guid.NewGuid();
            await using(var cmd=new NpgsqlCommand("INSERT INTO rubric_criteria(id,rubric_id,criterion_code,title,description,weight,sort_order) VALUES($1,$2,$3,$4,$5,$6,$7)",conn))
            {Add(cmd,criterion);Add(cmd,id);Add(cmd,"C"+(i+1));Add(cmd,c.Title.Trim());Add(cmd,c.Description);Add(cmd,c.Weight);Add(cmd,i+1);await cmd.ExecuteNonQueryAsync(ct);}
            for(var l=0;l<4;l++){await using var cmd=new NpgsqlCommand("INSERT INTO rubric_levels(criterion_id,level_no,description) VALUES($1,$2,$3)",conn);Add(cmd,criterion);Add(cmd,l+1);Add(cmd,c.Levels[l]);await cmd.ExecuteNonQueryAsync(ct);}
        }
        await Audit(conn,actor,id,"rubrics","rubric.saved",ct);var result=await ReadRubric(conn,id,course,ct);await tx.CommitAsync(ct);return result;
    }
    private static async Task InsertRubric(NpgsqlConnection conn,Guid id,Guid course,Guid family,int version,Guid actor,string title,CancellationToken ct)
    {await using var cmd=new NpgsqlCommand("INSERT INTO rubrics(id,course_id,family_id,version_no,title,created_by_user_id) VALUES($1,$2,$3,$4,$5,$6)",conn);Add(cmd,id);Add(cmd,course);Add(cmd,family);Add(cmd,version);Add(cmd,title);Add(cmd,actor);await cmd.ExecuteNonQueryAsync(ct);}
    public async Task<IReadOnlyList<ManualGrade>> Grades(Guid assignment,Guid snapshot,Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);var a=await Load(conn,assignment,actor,false,ct);await SnapshotAccess(conn,a,snapshot,actor,ct);
        return await ReadGrades(conn,snapshot,!AssignmentPolicy.IsStaff(a.CourseRole),ct);
    }
    private static async Task<IReadOnlyList<ManualGrade>> ReadGrades(NpgsqlConnection conn,Guid snapshot,bool releasedOnly,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("SELECT g.id,g.grader_user_id,COALESCE(u.display_name,'系統自動評分'),g.score,g.feedback,g.graded_at,g.released_at,g.supersedes_grade_id,g.rubric_id,g.rubric_scores::text,g.essay_scores::text FROM grades g LEFT JOIN app_users u ON u.id=g.grader_user_id WHERE g.submission_revision_id=$1 AND (NOT $2 OR g.released_at IS NOT NULL) ORDER BY g.graded_at DESC,g.id DESC",conn);
        Add(cmd,snapshot);Add(cmd,releasedOnly);await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<ManualGrade>();
        while(await r.ReadAsync(ct))result.Add(new(r.GetGuid(0),snapshot,r.IsDBNull(1)?null:r.GetGuid(1),r.GetString(2),r.GetDecimal(3),r.GetString(4),r.GetFieldValue<DateTimeOffset>(5),ReadDate(r,6),r.IsDBNull(7)?null:r.GetGuid(7),r.IsDBNull(8)?null:r.GetGuid(8),JsonSerializer.Deserialize<List<CriterionScore>>(r.GetString(9),TeachingJson)!,JsonSerializer.Deserialize<Dictionary<Guid,decimal>>(r.GetString(10),TeachingJson)));return result;
    }
    public async Task<QuizGradingResult?> QuizResult(Guid assignment,Guid snapshot,Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);var a=await Load(conn,assignment,actor,false,ct);await SnapshotAccess(conn,a,snapshot,actor,ct);
        var marks=await QuizGrading.Read(conn,snapshot,ct);if(marks==null)return null;
        var grades=await ReadGrades(conn,snapshot,false,ct);var latest=grades.FirstOrDefault();
        var visible=AssignmentPolicy.IsStaff(a.CourseRole)||Practice||latest?.ReleasedAt!=null;
        var pending=marks.Count(m=>m.QuestionType=="essay" && !(latest?.EssayScores?.ContainsKey(m.QuestionId)??false));
        return new(marks.Select(m=>visible?m:m with{AutoScore=null,Correct=null}).ToArray(),visible?marks.Sum(m=>m.AutoScore??0):null,pending,pending==0);
    }
    public async Task<ManualGrade> SaveGrade(Guid assignment,Guid snapshot,Guid actor,SaveGradeRequest request,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        var a=await Load(conn,assignment,actor,true,ct);AssignmentPolicy.RequireStaff(a.CourseRole);
        if(a.CourseStatus=="archived")Fail(409,"封存課程無法評分。");await SnapshotAccess(conn,a,snapshot,actor,ct);
        var history=await ReadGrades(conn,snapshot,false,ct);var latest=history.FirstOrDefault();
        if(latest?.Id!=request.ExpectedGradeId)Fail(409,"評分已被其他人更新，請重新載入。");
        var score=request.Score;
        var quiz=await QuizGrading.Read(conn,snapshot,ct);
        if(quiz!=null){
            if(quiz.All(m=>m.QuestionType!="essay"))Fail(400,"客觀題已自動評分，無需人工修改。");
            request.EssayScores=QuizGrading.Essays(quiz,request.EssayScores,score);
            if(QuizGrading.Total(quiz,request.EssayScores)!=score)Fail(400,"總分必須等於客觀題得分加申論題得分。");
        }
        TeachingPolicy.ValidateGrade(score,a.MaxScore);
        if(a.RubricId.HasValue)
        {var rubric=await ReadRubric(conn,a.RubricId.Value,a.CourseId,ct);score=TeachingPolicy.RubricScore(rubric,request.Criteria,a.MaxScore);if(score!=request.Score)Fail(400,"總分與規準加權結果不符。");}
        else if(request.Criteria.Count>0)Fail(400,"此作業未指定規準。");
        var id=Guid.NewGuid();
        await using(var cmd=new NpgsqlCommand("INSERT INTO grades(id,submission_revision_id,grader_user_id,rubric_id,score,feedback,supersedes_grade_id,rubric_scores,essay_scores) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)",conn))
        {Add(cmd,id);Add(cmd,snapshot);Add(cmd,actor);OptionalGuid(cmd,a.RubricId);Add(cmd,score);Add(cmd,request.Feedback);OptionalGuid(cmd,latest?.Id);cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb,JsonSerializer.Serialize(request.Criteria,TeachingJson));cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb,JsonSerializer.Serialize(request.EssayScores,TeachingJson));await cmd.ExecuteNonQueryAsync(ct);}
        await Audit(conn,actor,id,"grades","grade.draft_saved",ct);var result=(await ReadGrades(conn,snapshot,false,ct)).Single(g=>g.Id==id);await tx.CommitAsync(ct);return result;
    }
    public async Task<ManualGrade> ReleaseGrade(Guid assignment,Guid snapshot,Guid grade,Guid actor,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        var a=await Load(conn,assignment,actor,true,ct);AssignmentPolicy.RequireStaff(a.CourseRole);
        if(a.CourseStatus=="archived")Fail(409,"封存課程無法發布成績。");await SnapshotAccess(conn,a,snapshot,actor,ct);
        var latest=(await ReadGrades(conn,snapshot,false,ct)).FirstOrDefault();
        if(latest is null || latest.Id!=grade)Fail(409,"只能發布目前最新的評分草稿。");
        await using(var cmd=new NpgsqlCommand("UPDATE grades SET released_at=COALESCE(released_at,now()) WHERE id=$1",conn)){Add(cmd,grade);await cmd.ExecuteNonQueryAsync(ct);}
        await AssessmentGradeSync.ReleaseQuiz(conn,assignment,snapshot,latest!.Score,actor,ct);
        await Audit(conn,actor,grade,"grades","grade.released",ct);var result=(await ReadGrades(conn,snapshot,false,ct)).First();await tx.CommitAsync(ct);return result;
    }
}
