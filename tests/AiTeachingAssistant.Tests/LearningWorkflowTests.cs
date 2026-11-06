using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Npgsql;
namespace AiTeachingAssistant.Tests;

public class LearningTimePolicyTests
{
    [Fact] public void ClientCannotSupplyAuthoritativeElapsedSeconds()=>Assert.Throws<System.Text.Json.JsonException>(()=>System.Text.Json.JsonSerializer.Deserialize<LearningHeartbeat>("""{"active":true,"elapsedSeconds":86400}""",new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    private readonly DateTimeOffset start=DateTimeOffset.Parse("2026-11-04T00:00:00Z");
    [Fact] public void ServerElapsedTimeBoundsCreditAndIdleGapsEarnNothing()
    {
        Assert.Equal(10000,LearningTimePolicy.Credit(start,start.AddSeconds(10),true,false,null,null));
        Assert.Equal(0,LearningTimePolicy.Credit(start,start.AddSeconds(10),false,false,null,null));
        Assert.Equal(0,LearningTimePolicy.Credit(start,start.AddSeconds(26),true,false,null,null));
        Assert.Equal(0,LearningTimePolicy.Credit(start,start.AddSeconds(-1),true,false,null,null));
    }
    [Fact] public void VideoSeeksPauseAndMissingPositionsEarnNothing()
    {
        Assert.Equal(8000,LearningTimePolicy.Credit(start,start.AddSeconds(10),true,true,0,8));
        foreach(var next in new double?[]{null,0,-1,300,double.NaN})Assert.Equal(0,LearningTimePolicy.Credit(start,start.AddSeconds(10),true,true,0,next));
        Assert.Equal(0,LearningTimePolicy.Credit(start,start.AddSeconds(10),false,true,0,10));
    }
    [Fact] public void UnsupportedContentCannotBecomeATimedMaterialByRenaming()
    {
        Assert.Null(LearningTimePolicy.MediaType("fake.mp4","<html>evil</html>"u8.ToArray()));
        Assert.Null(LearningTimePolicy.MediaType("fake.pdf","<html>evil</html>"u8.ToArray()));
        Assert.Equal("application/pdf",LearningTimePolicy.MediaType("notes.pdf","%PDF-1.4"u8.ToArray()));
    }
}

public class LearningWorkflowTests:IAsyncLifetime
{
    private NpgsqlDataSource db=null!;
    private readonly Guid teacher=Guid.NewGuid(),student=Guid.NewGuid(),peer=Guid.NewGuid(),course=Guid.NewGuid(),second=Guid.NewGuid(),taught=Guid.NewGuid();
    private readonly TestClock clock=new();
    private AssignmentWorkflow workflow=>new(db,clock);
    private LearningTime time=>new(db,clock);
    private LearningHistory history=>new(db,clock);
    private static bool Enabled=>!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION"));
    public async Task InitializeAsync()
    {
        if(!Enabled)return;db=NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")!);
        foreach(var id in new[]{teacher,student,peer})await Exec("INSERT INTO app_users(id,display_name) VALUES($1,'合成歷程測試')",id);
        foreach(var id in new[]{course,second,taught}){
            await Exec("INSERT INTO courses(id,code,title,term_code,owner_user_id,status) VALUES($1,$2,'同名合成課程',$3,$4,'active')",id,"LH-"+id.ToString("N"),id==second?"114-2":"115-1",teacher);
            await Exec("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,'teacher'),($1,$3,'student'),($1,$4,'student')",id,teacher,id==taught?peer:student,id==taught?student:peer);
        }
        // The mixed-role student's third course must not appear as a formal enrollment.
        await Exec("UPDATE course_members SET course_role='ta' WHERE course_id=$1 AND user_id=$2",taught,student);
    }
    private async Task<object?> Exec(string sql,params object[] values)
    {await using var cmd=db.CreateCommand(sql);foreach(var x in values)cmd.Parameters.AddWithValue(x);return await cmd.ExecuteScalarAsync();}
    private async Task<AssignmentDetail> Quiz(Guid c,string name="合成測驗",DateTimeOffset? opens=null,DateTimeOffset? due=null)
    {
        var a=await workflow.Create(c,teacher,new(){Title=name,OpensAt=opens,DueAt=due,Questions=[new(){PromptText="合成申論，不含真實答案"}]},default);
        await Exec("INSERT INTO course_grade_items(course_id,title,category,max_score,weight,created_by,source_assignment_id) VALUES($1,$2,'quiz',100,1,$3,$4)",c,name,teacher,a.Id);
        return await workflow.Get(a.Id,teacher,default);
    }
    private async Task<Guid> Resource(Guid c)
    {return (Guid)(await Exec("INSERT INTO course_resources(course_id,file_name,content,uploaded_by) VALUES($1,'synthetic.pdf',$2,$3) RETURNING id",c,"%PDF-1.4 synthetic"u8.ToArray(),teacher))!;}
    private async Task<AssignmentDetail> Gated(params Guid[] resources)
    {
        var a=await Quiz(course);a=await workflow.Update(a.Id,teacher,new(){Title=a.Title,ExpectedEditVersion=a.EditVersion,Questions=[new(){PromptText="門檻題目"}],LearningRequirements=resources.Select(r=>new LearningRequirementInput{ResourceId=r,MinimumSeconds=10}).ToList()},default);
        return await workflow.Transition(a.Id,teacher,"published",default);
    }
    [FormativeDatabaseFact] public async Task CrossCourseHistoryHidesDraftsPracticeAndUnreleasedGradesWithoutCreatingSubmissions()
    {
        var a=await Quiz(course);await workflow.Transition(a.Id,teacher,"published",default);
        var future=await Quiz(second,"未來測驗",clock.GetUtcNow().AddDays(1));await workflow.Transition(future.Id,teacher,"published",default);
        await Quiz(second,"私人教師草稿");
        var saved=await workflow.Save(a.Id,student,new(){Submit=true,Answers=[new(){QuestionId=a.Questions[0].Id,Text="synthetic answer"}]},default);
        var snapshots=await workflow.Snapshots(a.Id,saved.Id!.Value,teacher,default);
        var grade=await workflow.SaveGrade(a.Id,snapshots[0].Id,teacher,new(){Score=0,Feedback="不可提前公開"},default);
        var page=await history.Activities(student,null,null,null,null,null,1,20,false,default);
        Assert.Equal(2,page.Total);var quiz=page.Items.Single(x=>x.Id==a.Id);Assert.Null(quiz.Score);Assert.Equal("",quiz.Feedback);Assert.Equal("pending",quiz.State);
        Assert.Equal(2,(await history.Courses(student,default)).Count);Assert.Empty(await history.Courses(teacher,default));
        Assert.Empty((await history.Activities(student,taught,null,null,null,null,1,20,false,default)).Items);
        Assert.Empty((await history.Activities(teacher,null,null,null,null,null,1,20,false,default)).Items);
        Assert.Equal(1,(await history.Courses(student,default)).Single(x=>x.Id==course).Required);
        Assert.Equal(0,(await history.Courses(student,default)).Single(x=>x.Id==second).Required);
        Assert.Equal(1L,(long)(await Exec("SELECT count(*) FROM submissions WHERE assignment_id=ANY($1)",new[]{a.Id,future.Id}))!);
        await workflow.ReleaseGrade(a.Id,snapshots[0].Id,grade.Id,teacher,default);
        quiz=(await history.Activities(student,null,"115-1","quiz",null,null,1,20,false,default)).Items.Single();Assert.Equal(0m,quiz.Score);Assert.Equal("不可提前公開",quiz.Feedback);
        Assert.Equal(0m,(await history.Courses(student,default)).Single(x=>x.Id==course).Average);
        Assert.Single((await history.Activities(student,null,null,null,null,null,1,1,false,default)).Items);
        await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>history.Activities(student,null,null,null,null,null,0,20,false,default));
        await Exec("UPDATE courses SET status='archived' WHERE id=$1",second);
        Assert.Empty((await history.Activities(student,second,null,null,null,null,1,20,true,default)).Items);
        Assert.Equal(2,(await history.Courses(student,default)).Count);
    }
    [FormativeDatabaseFact] public async Task GatesRequireEveryResourceAndAnExplicitStartAndKeepEvidenceAfterStart()
    {
        var first=await Resource(course);var next=await Resource(course);var a=await Gated(first,next);
        var locked=await workflow.Get(a.Id,student,default);Assert.True(locked.LearningGate!.Locked);Assert.Empty(locked.Questions);
        Assert.Equal(409,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>workflow.StartLearningQuiz(a.Id,student,default))).StatusCode);
        Assert.Equal(409,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>workflow.Save(a.Id,student,new(){Answers=[]},default))).StatusCode);
        foreach(var resource in new[]{first,next}){
            var session=await time.Start(course,resource,student,false,null,default);clock.Advance(10);
            var result=await time.Heartbeat(course,resource,session.Id,student,false,new(){Active=true},default);Assert.Equal(10,result.AccumulatedSeconds);
            if(resource==first)Assert.False((await workflow.Get(a.Id,student,default)).LearningGate!.Met);
        }
        locked=await workflow.Get(a.Id,student,default);Assert.True(locked.LearningGate!.Met);Assert.Empty(locked.Questions);
        await workflow.StartLearningQuiz(a.Id,student,default);var open=await workflow.Get(a.Id,student,default);Assert.Single(open.Questions);Assert.True(open.LearningGate!.Started);
        await Exec("UPDATE course_resources SET display_title='改名不改內容',title_version=title_version+1 WHERE id=$1",first);
        Assert.True((await workflow.Get(a.Id,student,default)).LearningGate!.Started);
        var saved=await workflow.Save(a.Id,student,new(){Submit=true,Answers=[new(){QuestionId=open.Questions[0].Id,Text="synthetic"}]},default);Assert.Equal("submitted",saved.Status);
        Assert.Equal(1L,(long)(await Exec("SELECT count(*) FROM quiz_learning_starts WHERE assignment_id=$1",a.Id))!);
        Assert.False((await workflow.Get(a.Id,peer,default)).LearningGate!.Met);
        var material=await history.Activities(student,null,null,"material",null,null,1,20,false,default);Assert.Equal(2,material.Total);Assert.All(material.Items,x=>Assert.Equal(10,x.EffectiveSeconds));
    }
    [FormativeDatabaseFact] public async Task TimeIsIsolatedDeduplicatedAndBoundedAndCrossCourseRequirementsFail()
    {
        var first=await Resource(course);var other=await Resource(second);var a=await Quiz(course);
        await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>workflow.Update(a.Id,teacher,new(){Title=a.Title,ExpectedEditVersion=a.EditVersion,Questions=[new(){PromptText="q"}],LearningRequirements=[new(){ResourceId=other,MinimumSeconds=10}]},default));
        var one=await time.Start(course,first,student,false,null,default);clock.Advance(5);
        var two=await time.Start(course,first,student,false,null,default);
        Assert.Equal(409,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>time.Heartbeat(course,first,one.Id,student,false,new(){Active=true},default))).StatusCode);
        clock.Advance(10);Assert.Equal(10,(await time.Heartbeat(course,first,two.Id,student,false,new(){Active=true},default)).AccumulatedSeconds);
        Assert.Equal(10,(await time.Heartbeat(course,first,two.Id,student,false,new(){Active=true},default)).AccumulatedSeconds);
        clock.Advance(30);Assert.Equal(10,(await time.Heartbeat(course,first,two.Id,student,false,new(){Active=true},default)).AccumulatedSeconds);
        await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>time.Heartbeat(course,first,two.Id,peer,false,new(){Active=true},default));
        await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>time.Resource(second,first,student,false,default));
        Assert.Equal(0,(await time.Resource(course,first,teacher,true,default)).AccumulatedSeconds);
        await Exec("UPDATE course_members SET status='inactive' WHERE course_id=$1 AND user_id=$2",course,student);
        await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>time.Heartbeat(course,first,two.Id,student,false,new(){Active=true},default));
        Assert.DoesNotContain(await history.Courses(student,default),c=>c.Id==course);
    }
    [FormativeDatabaseFact] public async Task PracticeTimeNeverQualifiesFormalStudentAndDeadlineStillApplies()
    {
        var r=await Resource(course);var a=await Gated(r);
        var session=await time.Start(course,r,teacher,true,null,default);clock.Advance(10);
        Assert.Equal(10,(await time.Heartbeat(course,r,session.Id,teacher,true,new(){Active=true},default)).AccumulatedSeconds);
        Assert.Equal(0,(await time.Resource(course,r,teacher,false,default)).AccumulatedSeconds);
        Assert.False((await workflow.Get(a.Id,student,default)).LearningGate!.Met);
        var own=await time.Start(course,r,student,false,null,default);clock.Advance(10);await time.Heartbeat(course,r,own.Id,student,false,new(){Active=true},default);
        Assert.True((await workflow.Get(a.Id,student,default)).LearningGate!.Met);
        await Exec("UPDATE assignments SET due_at=$2 WHERE id=$1",a.Id,clock.GetUtcNow().AddSeconds(-1));
        Assert.Equal(409,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>workflow.StartLearningQuiz(a.Id,student,default))).StatusCode);
        await Exec("UPDATE courses SET status='archived' WHERE id=$1",course);
        await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>time.Start(course,r,student,false,null,default));
    }
    public async Task DisposeAsync()
    {
        if(db==null)return;
        var cs=new[]{course,second,taught};
        foreach(var sql in new[]{
            "DELETE FROM quiz_learning_starts WHERE assignment_id IN (SELECT id FROM assignments WHERE course_id=ANY($1))",
            "DELETE FROM quiz_learning_requirements WHERE assignment_id IN (SELECT id FROM assignments WHERE course_id=ANY($1))",
            "DELETE FROM learning_time_sessions WHERE resource_id IN (SELECT id FROM course_resources WHERE course_id=ANY($1))",
            "DELETE FROM course_grade_scores WHERE course_id=ANY($1)","DELETE FROM course_grade_items WHERE course_id=ANY($1)",
            "DELETE FROM grades WHERE submission_revision_id IN (SELECT r.id FROM submission_revisions r JOIN submissions s ON s.id=r.submission_id JOIN assignments a ON a.id=s.assignment_id WHERE a.course_id=ANY($1))",
            "DELETE FROM quiz_grading WHERE snapshot_id IN (SELECT r.id FROM submission_revisions r JOIN submissions s ON s.id=r.submission_id JOIN assignments a ON a.id=s.assignment_id WHERE a.course_id=ANY($1))",
            "DELETE FROM submission_revisions WHERE submission_id IN (SELECT s.id FROM submissions s JOIN assignments a ON a.id=s.assignment_id WHERE a.course_id=ANY($1))",
            "UPDATE submission_answers SET current_revision_id=NULL WHERE assignment_id IN (SELECT id FROM assignments WHERE course_id=ANY($1))",
            "DELETE FROM answer_revisions WHERE answer_id IN (SELECT id FROM submission_answers WHERE assignment_id IN (SELECT id FROM assignments WHERE course_id=ANY($1)))",
            "DELETE FROM submission_answers WHERE assignment_id IN (SELECT id FROM assignments WHERE course_id=ANY($1))",
            "DELETE FROM submissions WHERE assignment_id IN (SELECT id FROM assignments WHERE course_id=ANY($1))",
            "DELETE FROM assignment_questions WHERE assignment_id IN (SELECT id FROM assignments WHERE course_id=ANY($1))",
            "DELETE FROM assignments WHERE course_id=ANY($1)","DELETE FROM course_resources WHERE course_id=ANY($1)",
            "DELETE FROM course_members WHERE course_id=ANY($1)","DELETE FROM courses WHERE id=ANY($1)"})await Exec(sql,cs);
        await Exec("DELETE FROM audit_events WHERE actor_user_id=ANY($1)",new[]{teacher,student,peer});
        await Exec("DELETE FROM app_users WHERE id=ANY($1)",new[]{teacher,student,peer});await db.DisposeAsync();
    }
    private sealed class TestClock:TimeProvider
    {private DateTimeOffset now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>now;public void Advance(int seconds)=>now=now.AddSeconds(seconds);}
}
