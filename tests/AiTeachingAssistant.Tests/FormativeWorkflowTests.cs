using System.Text.Json;
using AiTeachingAssistant.Controllers;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
namespace AiTeachingAssistant.Tests;

public sealed class FormativeDatabaseFactAttribute:FactAttribute
{
    public FormativeDatabaseFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")))Skip="Opt-in PostgreSQL test; set FORMATIVE_TEST_CONNECTION to an already migrated test-accessible database.";}
}
public class FormativeWorkflowTests
{
    [Fact] public void EndpointRequiresAuthenticationAndExplicitLearnerCapability()=>Assert.Single(typeof(FormativeFeedbackController).GetCustomAttributes(typeof(AuthorizeAttribute),true));
    [Fact] public void AnonymousRoleClaimsCannotEnableStudentExperience()
    {
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role,PortalRoles.Administrator),new Claim("can_teach","True")]))};context.Request.Headers[LearnerExperience.Header]="student";
        var capability=new LearnerExperience(new SimpleAccessor{HttpContext=context});Assert.False(capability.IsPractice);Assert.False(capability.CanUseFormative);
    }
    [Fact] public void BrowserCannotInjectAuthoritativeContext()=>Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<RequestFormativeFeedback>("""{"studentId":"fake","stage":"generalChat","caseFacts":"fake"}""",new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    [Theory][InlineData(PortalRoles.Administrator,true)][InlineData(PortalRoles.Member,true)][InlineData(PortalRoles.Member,false)]
    public void ExperiencePreservesIdentityAndRequiresStaff(string role,bool allowed)
    {
        var id=Guid.NewGuid();var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim(ClaimTypes.Role,role),new Claim("can_teach",allowed.ToString())],"test"))};context.Request.Headers[LearnerExperience.Header]="student";
        var experience=new LearnerExperience(new SimpleAccessor{HttpContext=context});Assert.Equal(allowed,experience.IsPractice);
        Assert.Equal(id.ToString(),context.User.FindFirstValue(ClaimTypes.NameIdentifier));Assert.True(context.User.IsInRole(role));
    }
    [Fact] public void MixedCourseStudentIsFormalEvenWhenTeachingAnotherCourse(){
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim("can_teach","True")],"test"))};context.Request.Headers[LearnerExperience.Header]="student";
        var experience=new LearnerExperience(new SimpleAccessor{HttpContext=context}){CourseRole="student"};Assert.False(experience.IsPractice);
        experience.CourseRole="teacher";Assert.True(experience.IsPractice);experience.CourseRole="none";Assert.False(experience.IsPractice);
    }
    [FormativeDatabaseFact] public async Task StaffLearnerPracticeIsPrivateAndNeverAppearsInOfficialReviewOrGrades()
    {
        await using var f=await Fixture.Create();var(a,s)=await f.Draft();
        var practice=new AssignmentWorkflow(f.Source,TimeProvider.System,Practice(f.Teacher,PortalRoles.Administrator));
        var assistant=new AssignmentWorkflow(f.Source,TimeProvider.System,Practice(f.Ta,PortalRoles.Member));
        Assert.True((await practice.Get(a.Id,f.Teacher,default)).IsPractice);Assert.Equal("student",(await practice.Get(a.Id,f.Teacher,default)).CourseRole);
        var firstRequest=f.SaveRequest(a,s,"teacher practice");firstRequest.ExpectedVersion=0;
        foreach(var (view,actor) in new[]{(practice,f.Teacher),(assistant,f.Ta)}){
            foreach(var submit in new[]{false,true}){
                firstRequest.Submit=submit;
                Assert.Equal(403,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>view.Save(a.Id,actor,firstRequest,default))).StatusCode);
            }
            Assert.Null((await view.Own(a.Id,actor,default)).Id);
        }
        Assert.Empty(await f.Workflow.Submitted(a.Id,f.Teacher,default));
        Assert.Equal("saved synthetic answer",(await f.Workflow.Own(a.Id,f.Student,default)).Answers[0].Text);
        var draft=await f.Workflow.Create(f.Course,f.Teacher,new(){Title="private teaching draft",Questions=[new(){PromptText="test"}]},default);
        Assert.Equal(404,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>practice.Get(draft.Id,f.Teacher,default))).StatusCode);
    }
    private static LearnerExperience Practice(Guid actor,string role)
    {
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString()),new Claim(ClaimTypes.Role,role),new Claim("can_teach","True")],"test"))};context.Request.Headers[LearnerExperience.Header]="student";
        return new(new SimpleAccessor{HttpContext=context});
    }
    private sealed class SimpleAccessor:IHttpContextAccessor{public HttpContext? HttpContext{get;set;}}

    [FormativeDatabaseFact] public async Task SavedOwnerDraftUsesAuthoritativeSingleQuestionAndPersistsExactVersion()
    {
        await using var f=await Fixture.Create();var (a,s)=await f.Draft();var model=new FakeModel();
        var result=await f.Workflow.AskFeedback(a.Id,a.Questions[0].Id,f.Student,f.Request(s),model,f.Options,default);
        Assert.Equal("completed",result.Status);Assert.Equal(s.Answers[0].CurrentRevisionId,result.AnswerRevisionId);Assert.False(result.IsStale);
        Assert.Equal("synthetic-model",result.Model);Assert.Equal("llama3",result.Profile);Assert.Equal(64,result.AnswerSha256.Length);
        Assert.Equal(2,model.Messages!.Count);Assert.Equal("system",model.Messages[0].Role);Assert.Equal("user",model.Messages[1].Role);
        using var input=JsonDocument.Parse(model.Messages[1].Content);
        Assert.Equal(a.Instructions,input.RootElement.GetProperty("caseFacts").GetString());
        Assert.Equal(a.Questions[0].PromptText,input.RootElement.GetProperty("assignmentContext").GetProperty("questionText").GetString());
        Assert.Equal("premorbid",input.RootElement.GetProperty("assignmentContext").GetProperty("stage").GetString());
        Assert.Single(input.RootElement.GetProperty("answers").EnumerateArray());Assert.DoesNotContain("UNRELATED_ANSWER",model.Messages[1].Content);
        Assert.DoesNotContain("prior conversation",model.Messages[1].Content);Assert.Equal(0L,await f.Scalar("SELECT count(*) FROM grades WHERE grader_user_id=$1",f.Teacher));
        var serialized=JsonSerializer.Serialize(result);Assert.DoesNotContain("requestInspection",serialized,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("thinking",serialized,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("RawResponse",serialized);
    }
    [FormativeDatabaseFact] public async Task OtherStudentAndStaffCannotInspectOrEvaluatePrivateDraft()
    {
        await using var f=await Fixture.Create();var(a,s)=await f.Draft();await f.Workflow.AskFeedback(a.Id,a.Questions[0].Id,f.Student,f.Request(s),new FakeModel(),f.Options,default);
        Assert.Empty(await f.Workflow.Feedback(a.Id,a.Questions[0].Id,f.Other,default));
        var other=await f.Workflow.Save(a.Id,f.Other,new(){ExpectedVersion=0,Answers=a.Questions.Select(q=>new AnswerInput{QuestionId=q.Id,Text="other synthetic answer"}).ToList()},default);
        var model=new FakeModel();var wrong=new RequestFormativeFeedback{ExpectedVersion=other.Version,AnswerRevisionId=s.Answers[0].CurrentRevisionId!.Value};
        Assert.Equal(409,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>f.Workflow.AskFeedback(a.Id,a.Questions[0].Id,f.Other,wrong,model,f.Options,default))).StatusCode);Assert.Null(model.Messages);
        foreach(var staff in new[]{f.Teacher,f.Ta})
        {
            Assert.Equal(403,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>f.Workflow.Feedback(a.Id,a.Questions[0].Id,staff,default))).StatusCode);
            Assert.Equal(403,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>f.Workflow.AskFeedback(a.Id,a.Questions[0].Id,staff,f.Request(s),model,f.Options,default))).StatusCode);
        }
    }
    [FormativeDatabaseFact] public async Task LaterEditsMarkFeedbackStaleAndConflictsNeverEvaluateDifferentText()
    {
        await using var f=await Fixture.Create();var(a,s)=await f.Draft();await f.Workflow.AskFeedback(a.Id,a.Questions[0].Id,f.Student,f.Request(s),new FakeModel(),f.Options,default);
        var saved=await f.Workflow.Save(a.Id,f.Student,f.SaveRequest(a,s,"changed synthetic answer"),default);
        Assert.True((await f.Workflow.Feedback(a.Id,a.Questions[0].Id,f.Student,default)).Single().IsStale);
        var model=new FakeModel();Assert.Equal(409,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>f.Workflow.AskFeedback(a.Id,a.Questions[0].Id,f.Student,f.Request(s),model,f.Options,default))).StatusCode);Assert.Null(model.Messages);
        var submitted=await f.Workflow.Save(a.Id,f.Student,f.SaveRequest(a,saved,"changed synthetic answer",true),default);Assert.Equal("submitted",submitted.Status);
        Assert.Single(await f.Workflow.Snapshots(a.Id,submitted.Id!.Value,f.Student,default));
    }
    [FormativeDatabaseFact] public async Task InvalidTimeoutAndTransportFailurePreserveDraftAndAllowSubmission()
    {
        await using var f=await Fixture.Create();foreach(var mode in new[]{"invalid","timeout","http"})
        {
            var(a,s)=await f.Draft();var result=await f.Workflow.AskFeedback(a.Id,a.Questions[0].Id,f.Student,f.Request(s),new FakeModel{Failure=mode},f.Options,default);
            Assert.Equal("failed",result.Status);Assert.Null(result.Assessment);Assert.NotNull(result.CompletedAt);Assert.Contains("草稿",result.Error);
            var own=await f.Workflow.Own(a.Id,f.Student,default);Assert.Equal(s.Answers[0].Text,own.Answers[0].Text);Assert.Equal(s.Version,own.Version);
            var submitted=await f.Workflow.Save(a.Id,f.Student,f.SaveRequest(a,own,own.Answers[0].Text,true),default);Assert.Equal("submitted",submitted.Status);
            Assert.Equal(0L,await f.Scalar("SELECT count(*) FROM grades WHERE grader_user_id=$1",f.Teacher));
        }
    }
    [FormativeDatabaseFact] public async Task NoFeedbackIsRequiredAndInferenceDoesNotLockOrOverwriteConcurrentSave()
    {
        await using var f=await Fixture.Create();var(a,s)=await f.Draft();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var resume=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var model=new FakeModel{Pause=async()=>{entered.SetResult();await resume.Task.WaitAsync(TimeSpan.FromSeconds(15));}};
        var pending=f.Workflow.AskFeedback(a.Id,a.Questions[0].Id,f.Student,f.Request(s),model,f.Options,default);await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var saved=await f.Workflow.Save(a.Id,f.Student,f.SaveRequest(a,s,"concurrent revision"),default).WaitAsync(TimeSpan.FromSeconds(5));
        resume.SetResult();Assert.True((await pending).IsStale);Assert.Equal("concurrent revision",(await f.Workflow.Own(a.Id,f.Student,default)).Answers[0].Text);
        var(b,initial)=await f.Draft();Assert.Empty(await f.Workflow.Feedback(b.Id,b.Questions[0].Id,f.Student,default));
        Assert.Equal("submitted",(await f.Workflow.Save(b.Id,f.Student,f.SaveRequest(b,initial,initial.Answers[0].Text,true),default)).Status);
    }
    [FormativeDatabaseFact] public async Task ClosedDeadlineArchivedAndUnpublishedPoliciesArePreserved()
    {
        await using var f=await Fixture.Create();
        foreach(var policy in new[]{"closed","deadline","future","archived"})
        {
            var(a,s)=await f.Draft();
            if(policy=="closed")await f.Workflow.Transition(a.Id,f.Teacher,"closed",default);
            if(policy=="deadline")await f.Execute("UPDATE assignments SET due_at=now()-interval '1 hour' WHERE id=$1",a.Id);
            if(policy=="future")await f.Execute("UPDATE assignments SET opens_at=now()+interval '1 hour' WHERE id=$1",a.Id);
            if(policy=="archived")await f.Execute("UPDATE courses SET status='archived' WHERE id=$1",f.Course);
            Assert.Equal(409,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>f.Workflow.AskFeedback(a.Id,a.Questions[0].Id,f.Student,f.Request(s),new FakeModel(),f.Options,default))).StatusCode);
            if(policy=="archived")await f.Execute("UPDATE courses SET status='active' WHERE id=$1",f.Course);
        }
        var draft=await f.Workflow.Create(f.Course,f.Teacher,new(){Title="unpublished",Questions=[new(){PromptText="synthetic",StageCode="premorbid"}]},default);
        Assert.Equal(404,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>f.Workflow.Feedback(draft.Id,draft.Questions[0].Id,f.Student,default))).StatusCode);
    }
    [FormativeDatabaseFact] public async Task MissingStageDoesNotFallBackToGeneralChatAndMissingCaseRemainsUnverified()
    {
        await using var f=await Fixture.Create();var(a,s)=await f.Draft(stage:null,instructions:"");
        Assert.Equal(400,(await Assert.ThrowsAsync<AssignmentWorkflowException>(()=>f.Workflow.AskFeedback(a.Id,a.Questions[0].Id,f.Student,f.Request(s),new FakeModel(),f.Options,default))).StatusCode);
        var(b,t)=await f.Draft(instructions:"");var result=await f.Workflow.AskFeedback(b.Id,b.Questions[0].Id,f.Student,f.Request(t),new FakeModel(),f.Options,default);Assert.False(result.Assessment!.Evaluations[0].CaseEvidenceAvailable);
    }
    private sealed class FakeModel:IOllamaClient
    {
        public string? Failure;public Func<Task>? Pause;public IReadOnlyList<OllamaMessage>? Messages;
        public async Task<(string Content,string? Thinking,RequestMetrics Metrics)> AssessAsync(IReadOnlyList<OllamaMessage> messages,bool compare,CancellationToken ct)
        {
            Messages=messages;Assert.False(compare);if(Pause is not null)await Pause();
            if(Failure=="timeout")throw new TaskCanceledException();if(Failure=="http")throw new HttpRequestException("do not disclose transport details");
            using var input=JsonDocument.Parse(messages.Last().Content);var available=input.RootElement.GetProperty("caseFacts").ValueKind==JsonValueKind.String;
            var content=Failure=="invalid"?"invalid":JsonSerializer.Serialize(new AssessmentResult([new("current",available,[],[],[],[],[],"合成測試發現",2,"合成測試理由")],""),AssessmentContract.JsonOptions);
            return(content,"private model thinking must not persist",new(0,0,0,0,0,0,0,0,0,"stop"));
        }
        public Task<(string Content,string? Thinking,RequestMetrics Metrics)> ChatAsync(IReadOnlyList<OllamaMessage> m,CancellationToken ct)=>throw new Exception("No chat/history allowed");
        public Task<string> SummarizeAsync(string s,IReadOnlyList<ChatMessage> m,int n,CancellationToken ct)=>throw new Exception("No summary allowed");
        public Task<OllamaHealthResult> CheckHealthAsync(CancellationToken ct)=>throw new NotSupportedException();
    }
    private sealed class Fixture:IAsyncDisposable
    {
        public NpgsqlDataSource Source=null!;public AssignmentWorkflow Workflow=null!;
        public Guid Teacher=Guid.NewGuid(),Student=Guid.NewGuid(),Other=Guid.NewGuid(),Ta=Guid.NewGuid(),Course=Guid.NewGuid();
        public OllamaOptions Options=new(){Model="synthetic-model",Profile="llama3"};
        public static async Task<Fixture> Create()
        {
            var f=new Fixture();f.Source=NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")!);f.Workflow=new(f.Source,TimeProvider.System);
            foreach(var user in new[]{f.Teacher,f.Student,f.Other,f.Ta})await f.Execute("INSERT INTO app_users(id,display_name,email) VALUES($1,'合成形成性測試',$2)",user,$"formative-{user:N}@example.invalid");
            await f.Execute("INSERT INTO courses(id,code,title,term_code,owner_user_id,status) VALUES($1,$2,'合成測試課程','test',$3,'active')",f.Course,"TEST-"+f.Course,f.Teacher);
            foreach(var pair in new[]{(f.Teacher,"teacher"),(f.Student,"student"),(f.Other,"student"),(f.Ta,"ta")})await f.Execute("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,$3)",f.Course,pair.Item1,pair.Item2);return f;
        }
        public async Task<(AssignmentDetail,SubmissionDetail)> Draft(string? stage="premorbid",string instructions="合成案例：個案在提示下完成任務。")
        {
            var a=await Workflow.Create(Course,Teacher,new(){Title="合成練習",Instructions=instructions,Questions=[new(){PromptText="權威題目：請限制推論",StageCode=stage},new(){PromptText="其他題目",StageCode="illnessCourse"}]},default);
            await Workflow.Transition(a.Id,Teacher,"published",default);
            var s=await Workflow.Save(a.Id,Student,new(){Answers=a.Questions.Select((q,i)=>new AnswerInput{QuestionId=q.Id,Text=i==0?"saved synthetic answer":"UNRELATED_ANSWER"}).ToList()},default);return(a,s);
        }
        public RequestFormativeFeedback Request(SubmissionDetail s)=>new(){ExpectedVersion=s.Version,AnswerRevisionId=s.Answers[0].CurrentRevisionId!.Value};
        public SaveSubmissionRequest SaveRequest(AssignmentDetail a,SubmissionDetail s,string text,bool submit=false)=>new(){ExpectedVersion=s.Version,Submit=submit,Answers=a.Questions.Select((q,i)=>new AnswerInput{QuestionId=q.Id,Text=i==0?text:"UNRELATED_ANSWER"}).ToList()};
        public async Task Execute(string sql,params object[] values){await using var cmd=Source.CreateCommand(sql);foreach(var v in values)cmd.Parameters.AddWithValue(v);await cmd.ExecuteNonQueryAsync();}
        public async Task<object?> Scalar(string sql,params object[] values){await using var cmd=Source.CreateCommand(sql);foreach(var v in values)cmd.Parameters.AddWithValue(v);return await cmd.ExecuteScalarAsync();}
        public async ValueTask DisposeAsync()
        {
            await using var conn=await Source.OpenConnectionAsync();await using var tx=await conn.BeginTransactionAsync();
            var ids=new[]{Teacher,Student,Other,Ta};
            await using(var check=new NpgsqlCommand("SELECT count(*) FROM app_users WHERE id=ANY($1) AND email LIKE 'formative-%@example.invalid'",conn)){check.Parameters.AddWithValue(ids);if((long)(await check.ExecuteScalarAsync())! !=4)throw new Exception("Synthetic cleanup ownership mismatch");}
            const string scope="SELECT id FROM assignments WHERE course_id=$1";
            foreach(var sql in new[]{"DELETE FROM evaluation_runs WHERE answer_revision_id IN (SELECT r.id FROM answer_revisions r JOIN submission_answers sa ON sa.id=r.answer_id WHERE sa.assignment_id IN ("+scope+"))",
                "DELETE FROM submission_revisions WHERE submission_id IN (SELECT id FROM submissions WHERE assignment_id IN ("+scope+"))",
                "UPDATE submission_answers SET current_revision_id=NULL WHERE assignment_id IN ("+scope+")",
                "DELETE FROM answer_revisions WHERE answer_id IN (SELECT id FROM submission_answers WHERE assignment_id IN ("+scope+"))",
                "DELETE FROM submission_answers WHERE assignment_id IN ("+scope+")","DELETE FROM submissions WHERE assignment_id IN ("+scope+")","DELETE FROM courses WHERE id=$1"})
            {await using var cmd=new NpgsqlCommand(sql,conn);cmd.Parameters.AddWithValue(Course);await cmd.ExecuteNonQueryAsync();}
            foreach(var sql in new[]{"DELETE FROM audit_events WHERE actor_user_id=ANY($1)","DELETE FROM app_users WHERE id=ANY($1)"})
            {await using var cmd=new NpgsqlCommand(sql,conn);cmd.Parameters.AddWithValue(ids);await cmd.ExecuteNonQueryAsync();}
            await tx.CommitAsync();await Source.DisposeAsync();
        }
    }
}
