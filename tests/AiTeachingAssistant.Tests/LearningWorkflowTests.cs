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
        Assert.Null((await history.Courses(student,default)).Single(x=>x.Id==course).Average);
        Assert.Single((await history.Activities(student,null,null,null,null,null,1,1,false,default)).Items);
        await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>history.Activities(student,null,null,null,null,null,0,20,false,default));
        await Exec("UPDATE courses SET status='archived' WHERE id=$1",second);
        Assert.Empty((await history.Activities(student,second,null,null,null,null,1,20,true,default)).Items);
        Assert.Equal(2,(await history.Courses(student,default)).Count);
    }
    [FormativeDatabaseFact] public async Task TimedQuizRejectsEarlyAccessAndExpiredSavesDespiteRepeatedStart()
    {
        var opens=clock.GetUtcNow().AddSeconds(10);
        var draft=await Quiz(course,opens:opens);
        await workflow.Update(draft.Id,teacher,new(){Title=draft.Title,OpensAt=opens,TimeLimitMinutes=1,ExpectedEditVersion=draft.EditVersion,Questions=[new(){PromptText="合成測驗"}]},default);
        await workflow.Transition(draft.Id,teacher,"published",default);
        Assert.Empty((await workflow.Get(draft.Id,student,default)).Questions);
        await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>workflow.StartLearningQuiz(draft.Id,student,default));
        clock.Advance(10);await workflow.StartLearningQuiz(draft.Id,student,default);
        var exam=await workflow.Get(draft.Id,student,default);Assert.Single(exam.Questions);
        clock.Advance(60);await workflow.StartLearningQuiz(draft.Id,student,default);
        Assert.Equal(exam.StartedAt,(await workflow.Get(draft.Id,student,default)).StartedAt);
        var error=await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>workflow.Save(draft.Id,student,new(){Submit=true,Answers=[new(){QuestionId=exam.Questions[0].Id,Text="synthetic"}]},default));
        Assert.Equal(409,error.StatusCode);Assert.Null((await workflow.Own(draft.Id,student,default)).Id);
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
        await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>time.Start(course,r,teacher,true,null,default));
        Assert.Equal(0,(await time.Resource(course,r,teacher,false,default)).AccumulatedSeconds);
        Assert.False((await workflow.Get(a.Id,student,default)).LearningGate!.Met);
        var own=await time.Start(course,r,student,false,null,default);clock.Advance(10);await time.Heartbeat(course,r,own.Id,student,false,new(){Active=true},default);
        Assert.True((await workflow.Get(a.Id,student,default)).LearningGate!.Met);
        await Exec("UPDATE assignments SET due_at=$2 WHERE id=$1",a.Id,clock.GetUtcNow().AddSeconds(-1));
        Assert.Equal(409,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>workflow.StartLearningQuiz(a.Id,student,default))).StatusCode);
        await Exec("UPDATE courses SET status='archived' WHERE id=$1",course);
        await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>time.Start(course,r,student,false,null,default));
    }
    [FormativeDatabaseFact] public async Task YouTubeTimeUsesVideoEvidenceAndStaffStatisticsExcludePractice()
    {
        var resource=(Guid)(await Exec("INSERT INTO course_resources(course_id,file_name,content,uploaded_by,resource_type,youtube_video_id) VALUES($1,'YouTube 課程回放',$2,$3,'youtube','M7lc1UVf-VE') RETURNING id",course,Array.Empty<byte>(),teacher))!;
        var meta=await time.Resource(course,resource,student,false,default);Assert.Equal("video/youtube",meta.MediaType);Assert.Equal("M7lc1UVf-VE",meta.YouTubeVideoId);
        var quiz=await Gated(resource);var session=await time.Start(course,resource,student,false,0,default);
        clock.Advance(1);Assert.Equal(0,(await time.Heartbeat(course,resource,session.Id,student,false,new(){Active=true,Position=0.7},default)).AccumulatedSeconds);
        clock.Advance(10);Assert.Equal(10,(await time.Heartbeat(course,resource,session.Id,student,false,new(){Active=true,Position=10.7},default)).AccumulatedSeconds);
        clock.Advance(10);Assert.Equal(10,(await time.Heartbeat(course,resource,session.Id,student,false,new(){Active=true,Position=300},default)).AccumulatedSeconds);
        clock.Advance(5);Assert.Equal(15,(await time.Heartbeat(course,resource,session.Id,student,false,new(){Active=true,Close=true,Position=305},default)).AccumulatedSeconds);
        Assert.True((await workflow.Get(quiz.Id,student,default)).LearningGate!.Met);
        await workflow.StartLearningQuiz(quiz.Id,student,default);
        await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>time.Start(course,resource,teacher,true,0,default));
        var stats=System.Text.Json.JsonSerializer.SerializeToElement(await time.Statistics(course,resource,teacher,false,default));Assert.Equal(15,stats.GetProperty("totalSeconds").GetInt64());Assert.Equal(1,stats.GetProperty("watchedCount").GetInt32());Assert.Equal(2,stats.GetProperty("studentCount").GetInt32());
        Assert.Equal(403,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>time.Statistics(course,resource,student,false,default))).StatusCode);
        Assert.Equal(403,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>time.Statistics(course,resource,teacher,true,default))).StatusCode);
        Assert.Equal(404,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>time.Resource(second,resource,student,false,default))).StatusCode);
    }
    [FormativeDatabaseFact] public async Task PlaybackSpeedKeepsWallTimeSeparateFromContentAndRejectsSeek()
    {
        var resource=(Guid)(await Exec("INSERT INTO course_resources(course_id,file_name,content,uploaded_by,resource_type,youtube_video_id) VALUES($1,'Speed replay',$2,$3,'youtube','M7lc1UVf-VE') RETURNING id",course,Array.Empty<byte>(),teacher))!;
        var quiz=await Gated(resource);
        await Exec("UPDATE quiz_learning_requirements SET minimum_seconds=20 WHERE assignment_id=$1",quiz.Id);
        var session=await time.Start(course,resource,student,false,0,default,2);
        clock.Advance(10);var beat=await time.Heartbeat(course,resource,session.Id,student,false,new(){Active=true,Position=20,PlaybackRate=2},default);
        Assert.Equal(10,beat.AccumulatedSeconds);Assert.Equal(20,beat.ContentSeconds);
        var gate=(await workflow.Get(quiz.Id,student,default)).LearningGate!;Assert.True(gate.Met);Assert.Equal(20,gate.Requirements[0].AccumulatedSeconds);
        await workflow.StartLearningQuiz(quiz.Id,student,default);
        clock.Advance(10);Assert.Equal(10,(await time.Heartbeat(course,resource,session.Id,student,false,new(){Active=true,Position=300,PlaybackRate=2},default)).AccumulatedSeconds);
        clock.Advance(10);Assert.Equal(10,(await time.Heartbeat(course,resource,session.Id,student,false,new(){Active=true,Position=320,PlaybackRate=1},default)).AccumulatedSeconds);
        clock.Advance(5);Assert.Equal(15,(await time.Heartbeat(course,resource,session.Id,student,false,new(){Active=true,Close=true,Position=330,PlaybackRate=2},default)).AccumulatedSeconds);
        var stats=System.Text.Json.JsonSerializer.SerializeToElement(await time.Statistics(course,resource,teacher,false,default));
        var speed=stats.GetProperty("students").EnumerateArray().Single(x=>x.GetProperty("id").GetGuid()==student).GetProperty("playbackSpeeds")[0];
        Assert.Equal(2,speed.GetProperty("playbackRate").GetDouble());Assert.Equal(15,speed.GetProperty("watchSeconds").GetInt64());Assert.Equal(30,speed.GetProperty("contentSeconds").GetInt64());
        await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>time.Start(course,resource,student,false,0,default,double.NaN));
    }
    private async Task<(Guid Assignment,Guid Snapshot,Guid Question)> ShowcaseEssay(bool closed=false)
    {
        var a=await Quiz(course);await workflow.Transition(a.Id,teacher,"published",default);
        var saved=await workflow.Save(a.Id,student,new(){Submit=true,Answers=[new(){QuestionId=a.Questions[0].Id,Text="PRIVATE synthetic source answer"}]},default);
        var snapshot=(Guid)(await Exec("SELECT id FROM submission_revisions WHERE submission_id=$1",saved.Id!.Value))!;
        await Exec("INSERT INTO grades(submission_revision_id,grader_user_id,score,released_at) VALUES($1,$2,80,now())",snapshot,teacher);
        if(closed)await workflow.Transition(a.Id,teacher,"closed",default);
        return(a.Id,snapshot,a.Questions[0].Id);
    }
    private static ShowcasePublishInput Approval(int version)=>new(){ExpectedVersion=version,Anonymized=true,Authorized=true,NoAnswerLeak=true,AuthorizationReference="synthetic-policy-approval"};
    [FormativeDatabaseFact]public async Task ShowcasePublishesOnlyReviewedSnapshotAndSupportsTaWithdrawal()
    {
        var source=await ShowcaseEssay();var show=new CourseShowcases(db);
        await Exec("UPDATE course_members SET course_role='ta' WHERE course_id=$1 AND user_id=$2",course,peer);
        var id=await show.Create(course,peer,false,new(){SnapshotId=source.Snapshot,QuestionId=source.Question},default);
        Assert.Equal(404,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Get(course,id,student,false,false,default))).StatusCode);
        Assert.Equal(403,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Sources(course,student,false,default))).StatusCode);
        await show.Save(course,id,peer,false,new(){Title="Anonymous example",Background="Reviewed prompt",Answer="Anonymous answer",Notes="Teaching notes",ExpectedVersion=1},null,default);
        Assert.Equal(403,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Publish(course,id,peer,false,Approval(2),default))).StatusCode);
        Assert.Equal(409,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Publish(course,id,teacher,false,Approval(2),default))).StatusCode);
        await workflow.Transition(source.Assignment,teacher,"closed",default);
        Assert.Equal(400,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Publish(course,id,teacher,false,new(){ExpectedVersion=2},default))).StatusCode);
        await show.Publish(course,id,teacher,false,Approval(2),default);
        var visible=Assert.IsType<ShowcasePublic>(await show.Get(course,id,student,false,false,default));Assert.Equal("Anonymous answer",visible.Answer);
        await Assert.ThrowsAsync<PostgresException>(()=>Exec("UPDATE course_showcase_versions SET answer='tampered' WHERE showcase_id=$1 AND revision=2",id));
        await Assert.ThrowsAsync<PostgresException>(()=>Exec("UPDATE course_showcase_publications SET authorization_reference='tampered' WHERE showcase_id=$1",id));
        var json=System.Text.Json.JsonSerializer.Serialize(visible);Assert.DoesNotContain("PRIVATE",json);Assert.DoesNotContain("Snapshot",json);Assert.DoesNotContain(student.ToString(),json);
        await show.Save(course,id,teacher,false,new(){Title="Changed draft",Background="",Answer="Not yet public",Notes="",ExpectedVersion=3},null,default);
        Assert.Equal("Anonymous answer",Assert.IsType<ShowcasePublic>(await show.Get(course,id,student,false,false,default)).Answer);
        Assert.Equal(409,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Publish(course,id,teacher,false,Approval(3),default))).StatusCode);
        await show.Withdraw(course,id,peer,false,4,default);await show.Withdraw(course,id,peer,false,1,default);
        Assert.Equal(1L,(long)(await Exec("SELECT count(*) FROM course_showcase_publications WHERE showcase_id=$1 AND revision=2 AND approved_by=$2",id,teacher))!);
        await show.Publish(course,id,teacher,false,Approval(5),default);
        Assert.Equal(2L,(long)(await Exec("SELECT count(*) FROM course_showcase_publications WHERE showcase_id=$1",id))!);
        await show.Withdraw(course,id,peer,false,6,default);
        Assert.Equal(404,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Get(course,id,student,false,false,default))).StatusCode);
    }
    [FormativeDatabaseFact]public async Task ShowcasePdfRequiresSeparateCopyAndOldAttachmentDisappearsWhenWithdrawn()
    {
        var report=(Guid)(await Exec("INSERT INTO report_assignments(course_id,title,max_score,status,created_by) VALUES($1,'Synthetic PDF',100,'closed',$2) RETURNING id",course,teacher))!;
        var original="%PDF-1.4 PRIVATE ORIGINAL"u8.ToArray();var safe="%PDF-1.4 ANONYMOUS COPY"u8.ToArray();
        var submitted=(Guid)(await Exec("INSERT INTO report_submissions(report_id,student_id,file_name,content,score,published_score,graded_by,graded_at) VALUES($1,$2,'PRIVATE-name.pdf',$3,90,90,$4,now()) RETURNING id",report,student,original,teacher))!;
        var show=new CourseShowcases(db);var id=await show.Create(course,teacher,false,new(){ReportSubmissionId=submitted},default);
        Assert.Equal(400,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Publish(course,id,teacher,false,Approval(1),default))).StatusCode);
        Assert.Equal(403,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Upload(course,id,student,false,1,safe,default))).StatusCode);
        await show.Upload(course,id,teacher,false,1,safe,default);await show.Publish(course,id,teacher,false,Approval(2),default);
        Assert.Equal(safe,await show.Attachment(course,id,student,false,false,default));
        Assert.Equal(403,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Attachment(course,id,student,false,true,default))).StatusCode);
        await Exec("UPDATE report_assignments SET status='published' WHERE id=$1",report);
        Assert.Equal(404,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Attachment(course,id,student,false,false,default))).StatusCode);
        await Exec("UPDATE report_assignments SET status='closed' WHERE id=$1",report);
        await Exec("UPDATE courses SET status='archived' WHERE id=$1",course);
        Assert.Equal(403,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Save(course,id,teacher,false,new(){Title="blocked",ExpectedVersion=3},null,default))).StatusCode);
        await show.Withdraw(course,id,teacher,false,3,default);
        Assert.Equal(404,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Attachment(course,id,student,false,false,default))).StatusCode);
        Assert.Equal(original,(byte[])(await Exec("SELECT content FROM report_submissions WHERE id=$1",submitted))!);
    }
    [FormativeDatabaseFact]public async Task ShowcaseRejectsCrossCoursePracticeAndUnreleasedSources()
    {
        var source=await ShowcaseEssay(true);var show=new CourseShowcases(db);
        Assert.Equal(404,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Create(second,teacher,false,new(){SnapshotId=source.Snapshot,QuestionId=source.Question},default))).StatusCode);
        Assert.Equal(403,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Create(course,teacher,true,new(){SnapshotId=source.Snapshot,QuestionId=source.Question},default))).StatusCode);
        await Exec("UPDATE grades SET released_at=NULL WHERE submission_revision_id=$1",source.Snapshot);
        Assert.Equal(404,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>show.Create(course,teacher,false,new(){SnapshotId=source.Snapshot,QuestionId=source.Question},default))).StatusCode);
    }
    public async Task DisposeAsync()
    {
        if(db==null)return;
        var cs=new[]{course,second,taught};
        foreach(var sql in new[]{
            "DELETE FROM course_showcases WHERE course_id=ANY($1)",
            "DELETE FROM quiz_learning_starts WHERE assignment_id IN (SELECT id FROM assignments WHERE course_id=ANY($1))",
            "DELETE FROM quiz_learning_requirements WHERE assignment_id IN (SELECT id FROM assignments WHERE course_id=ANY($1))",
            "DELETE FROM learning_time_sessions WHERE resource_id IN (SELECT id FROM course_resources WHERE course_id=ANY($1))",
            "DELETE FROM course_grade_scores WHERE course_id=ANY($1)","DELETE FROM course_grade_items WHERE course_id=ANY($1)",
            "DELETE FROM report_submissions WHERE report_id IN (SELECT id FROM report_assignments WHERE course_id=ANY($1))",
            "DELETE FROM report_assignments WHERE course_id=ANY($1)",
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
