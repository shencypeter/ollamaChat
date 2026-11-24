using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Options;
using Npgsql;
using AiTeachingAssistant.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Text.Json;

namespace AiTeachingAssistant.Tests;

public partial class CaseAnalysisChatTests
{
    [FormativeDatabaseFact] public async Task RepetitiveLongBFeedbackCannotEnterHistoryOrCompletedRoundStatistics()
    {
        await using var f=await Fixture.Create();var original=new string('測',1100);
        f.Provider.Feedback=JsonSerializer.Serialize(new{annotations=Array.Empty<object>(),feedback=string.Concat(Enumerable.Repeat("這是一段重複的合成評語內容，",30))});
        var error=await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"illnessCourse",new(){RequestId=Guid.NewGuid(),Content=original},default));
        Assert.Equal("feedback_repetition",error.Diagnostic!.Code);
        var history=(await f.Workflow.History(f.Course,f.Student,"illnessCourse",default)).Sessions.Single();var user=Assert.Single(history.Messages);Assert.Equal(original,user.Content);Assert.Equal("failed",user.Status);Assert.Equal(0,history.Statistics.RoundTrips);
    }
    [FormativeDatabaseFact] public async Task OnlyCurrentCourseStaffReceiveSafeFailureDetailsAndTheirFailedRequest()
    {
        await using var f=await Fixture.Create();
        foreach(var (actor,role) in new[]{(f.Teacher,"teacher"),(f.Ta,"ta"),(f.Student,"student")}){
            using var h=new InspectionHarness(f,actor,role);h.Transport.Status=503;
            var action=Assert.IsAssignableFrom<ObjectResult>(await h.Controller.Send(f.Course,"illnessCourse",new(){RequestId=Guid.NewGuid(),Content="合成失敗診斷作答"},default));Assert.Equal(502,action.StatusCode);
            var result=CaseJson(action);Assert.DoesNotContain("SENSITIVE",result.ToString());
            Assert.Equal(role!="student",result.TryGetProperty("diagnostic",out var diagnostic));Assert.Equal(role!="student",result.TryGetProperty("requestInspection",out var capture));
            if(role!="student"){Assert.Equal("provider_http",diagnostic.GetProperty("code").GetString());Assert.Equal(503,diagnostic.GetProperty("upstreamStatus").GetInt32());Assert.Single(capture.GetProperty("requests").EnumerateArray());}
            var rawId=h.Raw.Create(f.Course,actor);var rawAction=Assert.IsAssignableFrom<ObjectResult>(await h.Controller.SendRaw(f.Course,rawId,new(){Content="合成Raw失敗診斷"},default));
            Assert.Equal(role=="student"?403:502,rawAction.StatusCode);var rawError=CaseJson(rawAction);
            Assert.Equal(role!="student",rawError.TryGetProperty("diagnostic",out _));Assert.Equal(role!="student",rawError.TryGetProperty("requestInspection",out _));
            Assert.Equal(role=="student"?1:2,h.Transport.Calls);
            await h.Raw.ClearAll(actor,default);
            var stored=await f.Sql("SELECT string_agg(content,'') FROM case_analysis_messages WHERE session_id IN (SELECT id FROM case_analysis_sessions WHERE course_id=$1 AND student_id=$2)",f.Course,actor);Assert.DoesNotContain("SENSITIVE",stored!.ToString());Assert.DoesNotContain("provider_http",stored.ToString());
        }
        using var revoked=new InspectionHarness(f,f.Teacher,"teacher");revoked.Transport.Status=503;revoked.Transport.BeforeReply=async()=>{await f.Sql("UPDATE course_members SET course_role='student' WHERE course_id=$1 AND user_id=$2",f.Course,f.Teacher);};
        var downgraded=CaseJson(await revoked.Controller.Send(f.Course,"premorbid",new(){RequestId=Guid.NewGuid(),Content="合成降權失敗診斷"},default));Assert.False(downgraded.TryGetProperty("diagnostic",out _));Assert.False(downgraded.TryGetProperty("requestInspection",out _));
    }
    [FormativeDatabaseFact] public async Task IllnessPromptVersionIsRecordedWithoutRewritingLegacyMessagesOrRetryingThemUnderNewRules()
    {
        await using var f=await Fixture.Create();var first=Guid.NewGuid();
        var history=await f.Workflow.Send(f.Course,f.Student,"illnessCourse",new(){RequestId=first,Content="合成B階段作答"},default);
        Assert.All(history.Sessions.Single().Messages,m=>Assert.Equal(CaseChatPrompts.VersionFor(AnalysisStage.IllnessCourse),m.PromptVersion));
        // Seed a legacy record; immutable historical metadata must never be rewritten.
        var failed=Guid.NewGuid();var session=history.ActiveSessionId!.Value;
        var sequence=await f.Sql("UPDATE case_analysis_sessions SET next_sequence=next_sequence+1 WHERE id=$1 RETURNING next_sequence-1",session);
        await f.Sql("INSERT INTO case_analysis_messages(id,session_id,role,content,sequence_number,response_status,model_name,prompt_version,created_at,failure_code) VALUES($1,$2,'user','合成舊版失敗作答',$3,'failed','mock-model',$4,$5,'provider_failed')",failed,session,sequence!,CaseChatPrompts.Version,DateTimeOffset.UtcNow);
        var calls=f.Provider.Requests.Count;
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Retry(f.Course,f.Student,"illnessCourse",failed,default))).StatusCode);Assert.Equal(calls,f.Provider.Requests.Count);
        var retained=await f.Workflow.History(f.Course,f.Student,"illnessCourse",default);Assert.Equal("合成舊版失敗作答",retained.Sessions.Single().Messages.Last().Content);
        Assert.All(retained.Sessions.Single().Messages.Take(2),m=>Assert.Equal(CaseChatPrompts.VersionFor(AnalysisStage.IllnessCourse),m.PromptVersion));
        Assert.Equal(CaseChatPrompts.Version,retained.Sessions.Single().Messages.Last().PromptVersion);
    }
    [FormativeDatabaseFact] public async Task OnlyCurrentCourseStaffReceiveOwnActualRequestsAndHistoryNeverContainsThem()
    {
        await using var f=await Fixture.Create();
        foreach(var (actor,role) in new[]{(f.Teacher,"teacher"),(f.Ta,"ta"),(f.Student,"student")})
        {
            using var h=new InspectionHarness(f,actor,role);
            var request=new CaseChatInput{RequestId=Guid.NewGuid(),Content="合成階段作答"};
            var sent=CaseJson(await h.Controller.Send(f.Course,"illnessCourse",request,default));
            Assert.Equal(role!="student",sent.TryGetProperty("requestInspection",out var inspection));
            if(role!="student")
            {
                Assert.Equal(CaseChatPrompts.VersionFor(AnalysisStage.IllnessCourse),inspection.GetProperty("promptVersion").GetString());
                var captured=Assert.Single(inspection.GetProperty("requests").EnumerateArray());
                using var actual=JsonDocument.Parse(h.Transport.Body!);Assert.Equal(actual.RootElement.GetRawText(),captured.GetProperty("payload").GetRawText());
                using var answer=JsonDocument.Parse(captured.GetProperty("payload").GetProperty("messages")[1].GetProperty("content").GetString()!);
                Assert.Equal("B",answer.RootElement.GetProperty("currentPhase").GetString());Assert.Equal(JsonValueKind.Null,captured.GetProperty("rawResponse").ValueKind);
            }
            Assert.False(CaseJson(await h.Controller.History(f.Course,"illnessCourse",default)).TryGetProperty("requestInspection",out _));
            var replay=CaseJson(await h.Controller.Send(f.Course,"illnessCourse",request,default));
            Assert.False(replay.TryGetProperty("requestInspection",out _));Assert.Equal(1,h.Transport.Calls);
            var rawId=h.Raw.Create(f.Course,actor);
            var rawAction=Assert.IsAssignableFrom<ObjectResult>(await h.Controller.SendRaw(f.Course,rawId,new(){Content="合成Raw作答"},default));
            Assert.Equal(role=="student"?403:200,rawAction.StatusCode);var raw=CaseJson(rawAction);
            Assert.Equal(role!="student",raw.TryGetProperty("requestInspection",out inspection));Assert.Equal(role=="student"?1:2,h.Transport.Calls);
            if(role!="student")
            {
                var payload=inspection.GetProperty("requests")[0].GetProperty("payload");
                Assert.Single(payload.GetProperty("messages").EnumerateArray());Assert.False(payload.TryGetProperty("format",out _));
            }
            await h.Raw.ClearAll(actor,default);
            var stored=await f.Sql("SELECT string_agg(content,'') FROM case_analysis_messages WHERE session_id IN (SELECT id FROM case_analysis_sessions WHERE course_id=$1 AND student_id=$2)",f.Course,actor);
            Assert.DoesNotContain("currentPhase",stored!.ToString());Assert.DoesNotContain("outputRequirements",stored.ToString());
        }
    }
    [FormativeDatabaseFact] public async Task StudentPreviewReceivesNoInspectionAndDoesNotCallTheModel()
    {
        await using var f=await Fixture.Create();using var h=new InspectionHarness(f,f.Teacher,"teacher",true);
        Assert.False(CaseJson(await h.Controller.Configuration(f.Course,default)).GetProperty("canInspectRequests").GetBoolean());
        var result=Assert.IsAssignableFrom<ObjectResult>(await h.Controller.Send(f.Course,"premorbid",new(){RequestId=Guid.NewGuid(),Content="不得送出"},default));
        Assert.Equal(403,result.StatusCode);Assert.Equal(0,h.Transport.Calls);Assert.Null(h.Inspection.Current);
    }
    [FormativeDatabaseFact] public async Task CourseRoleDemotionAfterInferencePreventsReturningCapturedRequest()
    {
        await using var f=await Fixture.Create();using var h=new InspectionHarness(f,f.Teacher,"teacher");
        h.Transport.BeforeReply=async()=>{await f.Sql("UPDATE course_members SET course_role='student' WHERE course_id=$1 AND user_id=$2",f.Course,f.Teacher);};
        var result=CaseJson(await h.Controller.Send(f.Course,"premorbid",new(){RequestId=Guid.NewGuid(),Content="合成變更權限情境"},default));
        Assert.False(result.TryGetProperty("requestInspection",out _));Assert.Null(h.Inspection.Current);
    }
    private static JsonElement CaseJson(IActionResult result)=>JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<ObjectResult>(result).Value,new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private sealed class InspectionHarness:IDisposable
    {
        public InspectionTransport Transport=new();public RawChatSessions Raw;public OllamaRequestInspection Inspection;public CaseAnalysisChatController Controller;private HttpClient http;
        public InspectionHarness(Fixture fixture,Guid actor,string courseRole,bool preview=false)
        {
            // A platform Professor enrolled only as a student must not receive a snapshot;
            // a course TA with ordinary platform membership may inspect their own practice.
            var platformStaff=courseRole=="student"||preview;
            var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,actor.ToString()),new Claim(ClaimTypes.Role,platformStaff?"Professor":"Member"),new Claim("can_teach",platformStaff.ToString())},"test"))};
            if(preview)context.Request.Headers[LearnerExperience.Header]="student";
            var accessor=new HttpContextAccessor{HttpContext=context};var experience=new LearnerExperience(accessor){CourseRole=courseRole};Inspection=new(accessor,experience);
            var model=Microsoft.Extensions.Options.Options.Create(new OllamaOptions{Model="mock-model"});var options=Microsoft.Extensions.Options.Options.Create(new CaseChatOptions());
            http=new(Transport){BaseAddress=new Uri("http://localhost:11434/")};
            var provider=new OllamaClient(http,model,Microsoft.Extensions.Logging.Abstractions.NullLogger<OllamaClient>.Instance,inspection:Inspection);
            var workflow=new CaseAnalysisChat(fixture.Db,provider,model,options,TimeProvider.System);Raw=new(TimeProvider.System,options,model);
            Controller=new(workflow,Raw,provider,model,options,experience,Inspection){ControllerContext=new(){HttpContext=context}};
        }
        public void Dispose(){Raw.Dispose();http.Dispose();}
    }
    private sealed class InspectionTransport:HttpMessageHandler
    {
        public string? Body;public int Calls;public int Status=200;public Func<Task>? BeforeReply;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Body=await request.Content!.ReadAsStringAsync(ct);Calls++;if(BeforeReply is {} action)await action();
            if(Status!=200)return new((System.Net.HttpStatusCode)Status){Content=new StringContent("{\"error\":\"SENSITIVE_UPSTREAM_BODY\"}")};
            return new(System.Net.HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{message=new{role="assistant",content="{\"feedback\":\"合成回饋\",\"annotations\":[]}",thinking="private thinking must not be returned"},done_reason="stop",eval_count=3,eval_duration=100000000}))};
        }
    }
    [FormativeDatabaseFact] public async Task SelectedStageReachesProviderDespiteAnotherPhaseInTheAnswer()
    {
        await using var f=await Fixture.Create();
        await f.Workflow.Send(f.Course,f.Student,"illnessCourse",new(){RequestId=Guid.NewGuid(),Content="病前能獨立搭車及管理財務。"},default);
        Assert.Equal(AnalysisStage.IllnessCourse,f.Provider.FeedbackStage);
        Assert.StartsWith("本次作答階段：B 疾病病程",f.Provider.Requests.Single()[0].Content);
        Assert.Equal("病前能獨立搭車及管理財務。",f.Provider.Requests.Single().Last().Content);
    }
    [FormativeDatabaseFact] public async Task AnnotationMetadataIsImmutableAndAnchoredToTheExactAnswerRevision()
    {
        await using var f=await Fixture.Create();var first=Guid.NewGuid();
        f.Provider.Feedback="""{"feedback":"辨識到人口學資料，仍需核實案例。","annotations":[{"start":2,"end":5,"text":"55歲","codes":["A1a"],"explanation":"年齡","status":"unverified"}]}""";
        var history=await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=first,Content="😀55歲"},default);
        var reply=history.Sessions.Single().Messages.Last();Assert.Equal(first,reply.ReplyTo);Assert.Single(reply.Annotations!);Assert.Equal(2,reply.Annotations![0].Start);
        f.Provider.Feedback="""{"feedback":"第二版回饋。","annotations":[{"start":0,"end":3,"text":"56歲","codes":["A1a"],"explanation":"年齡","status":"unverified"}]}""";
        await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content="56歲",RevisesMessageId=first},default);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.ReviewHistory(f.Course,f.Teacher,f.Student,"premorbid",default))).StatusCode);
        var persisted=await f.Workflow.History(f.Course,f.Student,"premorbid",default);
        Assert.Equal(new[]{"55歲","56歲"},persisted.Sessions.Single().Messages.Where(m=>m.Role=="assistant").Select(m=>m.Annotations!.Single().Text));
        await Assert.ThrowsAsync<PostgresException>(()=>f.Sql("UPDATE case_analysis_messages SET annotations='[]'::jsonb WHERE id=$1",reply.Id));
        Assert.Empty((await f.Workflow.History(f.Course,f.Peer,"premorbid",default)).Sessions);
    }
    [FormativeDatabaseFact] public async Task InvalidEnvelopeKeepsFailedQuestionAndSuccessfulRetryHasOnePair()
    {
        await using var f=await Fixture.Create();f.Provider.Feedback="SENSITIVE broken json";var id=Guid.NewGuid();
        var error=await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=id,Content="55歲"},default));
        Assert.DoesNotContain("SENSITIVE",error.ToString());
        var before=(await f.Workflow.History(f.Course,f.Student,"premorbid",default)).Sessions.Single();Assert.Equal("failed",Assert.Single(before.Messages).Status);Assert.Equal(0,before.Statistics.RoundTrips);
        f.Provider.Feedback=null;var after=await f.Workflow.Retry(f.Course,f.Student,"premorbid",id,default);
        Assert.Equal(1,after.Sessions.Single().Statistics.RoundTrips);Assert.Empty(after.Sessions.Single().Messages.Last().Annotations!);
    }
    [FormativeDatabaseFact] public async Task AnswerRevisionsKeepOriginalsAndCountsWhileMetricsStayWithTheirOwnReply()
    {
        await using var f=await Fixture.Create();var original=Guid.NewGuid();var revised=Guid.NewGuid();
        f.Provider.Metrics=new(2000000000,100000000,300000000,1500000000,321,17,9,6,2345,"stop");
        await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=original,Content="第一版原始作答"},default);
        f.Provider.Metrics=new(1000000000,0,200000000,800000000,444,10,8,10,1234,"stop");
        var result=await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=revised,Content="第二版修改作答",RevisesMessageId=original},default);
        var session=result.Sessions.Single();var root=session.Messages.Single(m=>m.Id==original);var child=session.Messages.Single(m=>m.Id==revised);
        Assert.Equal("第一版原始作答",root.Content);Assert.Null(root.RevisesMessageId);Assert.Equal(original,child.RevisesMessageId);Assert.Equal(1,session.Statistics.RevisionCount);
        Assert.Equal(321,session.Messages.Single(m=>m.ReplyTo==original).Metrics!.PromptTokens);Assert.Equal(444,session.Messages.Single(m=>m.ReplyTo==revised).Metrics!.PromptTokens);
        Assert.Null(child.Metrics);Assert.Equal(2,session.Statistics.RoundTrips);
        await Assert.ThrowsAsync<PostgresException>(()=>f.Sql("UPDATE case_analysis_messages SET revises_message_id=NULL WHERE id=$1",revised));
        await Assert.ThrowsAsync<PostgresException>(()=>f.Sql("UPDATE case_analysis_messages SET performance_metrics='{}'::jsonb WHERE in_reply_to=$1",original));
        await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=revised,Content="第二版修改作答",RevisesMessageId=original},default);
        Assert.Equal(2,f.Provider.Requests.Count);
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=revised,Content="第二版修改作答"},default))).StatusCode);
        await f.Workflow.Restart(f.Course,f.Student,"premorbid",default);
        await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content="新工作階段延伸修訂",RevisesMessageId=revised},default);
        var totals=(await f.Workflow.Records(f.Course,f.Teacher,default)).Single(s=>s.Id==f.Student).Statistics;Assert.Equal(2,totals.RevisionCount);Assert.Equal(3,totals.RoundTrips);
    }

    [FormativeDatabaseFact] public async Task RevisionParentsMustBeCompletedAnswersFromSameOwnerCourseStageAndPractice()
    {
        await using var f=await Fixture.Create();var parent=Guid.NewGuid();
        await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=parent,Content="我的初始作答"},default);
        foreach(var (actor,stage) in new[]{(f.Peer,"premorbid"),(f.Student,"illnessCourse"),(f.Teacher,"premorbid")})
            Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,actor,stage,new(){RequestId=Guid.NewGuid(),Content="不合法的修改來源",RevisesMessageId=parent},default))).StatusCode);
        var assistant=(await f.Workflow.History(f.Course,f.Student,"premorbid",default)).Sessions.Single().Messages.Last().Id;
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content="不得把AI回覆當成原作答",RevisesMessageId=assistant},default))).StatusCode);
        Assert.Equal(1L,await f.Sql("SELECT count(*) FROM case_analysis_messages m JOIN case_analysis_sessions s ON s.id=m.session_id WHERE s.course_id=$1 AND m.role='user'",f.Course));
    }
    [Fact] public void BrowserCannotSupplyAnotherStudentsIdentityOrSystemPrompt()
        =>Assert.Throws<System.Text.Json.JsonException>(()=>System.Text.Json.JsonSerializer.Deserialize<CaseChatInput>("""{"content":"test","studentId":"other","systemPrompt":"inject"}""",new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));

    [FormativeDatabaseFact] public async Task StageChatsPersistSeparateHistoriesAndDerivedCountsAndRejectOtherStudents()
    {
        await using var f=await Fixture.Create();
        var request=new CaseChatInput{RequestId=Guid.NewGuid(),Content="合成案例第一輪"};
        var first=await f.Workflow.Send(f.Course,f.Student,"premorbid",request,default);
        Assert.Equal(1,first.Sessions.Single().Statistics.RoundTrips);
        Assert.Equal(new[]{"user","assistant"},first.Sessions.Single().Messages.Select(m=>m.Role));
        var answer=first.Sessions.Single().Messages.Last();Assert.Equal("mock-model",answer.ModelName);Assert.Equal(CaseChatPrompts.Version,answer.PromptVersion);
        await f.Workflow.Send(f.Course,f.Student,"premorbid",request,default);
        Assert.Single(f.Provider.Requests);
        await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content="第二輪沿用先前分析"},default);
        Assert.Equal(new[]{"system","user"},f.Provider.Requests.Last().Select(m=>m.Role));
        Assert.DoesNotContain("合成案例第一輪",string.Join("",f.Provider.Requests.Last().Select(m=>m.Content)));
        await f.Workflow.Send(f.Course,f.Student,"illnessCourse",new(){RequestId=Guid.NewGuid(),Content="另一階段"},default);
        Assert.Equal(2,f.Provider.Requests.Last().Count);Assert.Contains("疾病病程",f.Provider.Requests.Last()[0].Content);
        Assert.DoesNotContain("合成案例第一輪",string.Join("",f.Provider.Requests.Last().Select(m=>m.Content)));
        var persisted=await f.Workflow.History(f.Course,f.Student,"premorbid",default);
        Assert.Equal(4,persisted.Sessions.Single().Messages.Count);
        Assert.Empty((await f.Workflow.History(f.Course,f.Peer,"premorbid",default)).Sessions);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Records(f.Course,f.Student,default))).StatusCode);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.History(f.Other,f.Student,"premorbid",default))).StatusCode);
        var totals=(await f.Workflow.Records(f.Course,f.Teacher,default)).Single(x=>x.Id==f.Student).Statistics;
        Assert.Equal(3,totals.StudentMessages);Assert.Equal(3,totals.AssistantReplies);Assert.Equal(3,totals.RoundTrips);
    }

    [FormativeDatabaseFact] public async Task FailedRepliesAreRetriedWithoutExtraStudentsOrFakeRoundTrips()
    {
        await using var f=await Fixture.Create();f.Provider.Fail=true;
        var request=new CaseChatInput{RequestId=Guid.NewGuid(),Content="  合成模型失敗情境  "};
        await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"functionalAssessment",request,default));
        var failed=(await f.Workflow.History(f.Course,f.Student,"functionalAssessment",default)).Sessions.Single();
        var user=Assert.Single(failed.Messages);Assert.Equal(request.Content,user.Content);Assert.Equal("failed",user.Status);Assert.Equal(0,failed.Statistics.RoundTrips);
        f.Provider.Fail=false;
        await f.Workflow.Retry(f.Course,f.Student,"functionalAssessment",user.Id,default);
        var done=(await f.Workflow.History(f.Course,f.Student,"functionalAssessment",default)).Sessions.Single();
        Assert.Equal(1,done.Statistics.StudentMessages);Assert.Equal(1,done.Statistics.RoundTrips);
        await f.Workflow.Retry(f.Course,f.Student,"functionalAssessment",user.Id,default);
        Assert.Equal(2,f.Provider.Requests.Count);
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"functionalAssessment",new(){RequestId=request.RequestId,Content="不能以相同ID換內容"},default))).StatusCode);
    }

    [FormativeDatabaseFact] public async Task TeacherPracticeAndTaRestrictionsNeverChangeStudentStatisticsAndArchiveIsReadOnly()
    {
        await using var f=await Fixture.Create();
        await f.Workflow.Send(f.Course,f.Teacher,"psychosocial",new(){RequestId=Guid.NewGuid(),Content="教師試聊"},default);
        Assert.All(await f.Workflow.Records(f.Course,f.Teacher,default),row=>Assert.Equal(0,row.Statistics.RoundTrips));
        await f.Sql("UPDATE course_members SET ta_restrictions=ARRAY['statistics'] WHERE course_id=$1 AND user_id=$2",f.Course,f.Ta);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Records(f.Course,f.Ta,default))).StatusCode);
        await f.Sql("UPDATE courses SET status='archived' WHERE id=$1",f.Course);
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content="封存不可送出"},default))).StatusCode);
        Assert.Single((await f.Workflow.History(f.Course,f.Teacher,"psychosocial",default)).Sessions);
        await f.Sql("UPDATE course_members SET status='inactive' WHERE course_id=$1 AND user_id=$2",f.Course,f.Teacher);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.History(f.Course,f.Teacher,"psychosocial",default))).StatusCode);
    }

    [FormativeDatabaseFact] public async Task ParallelTurnsConflictAndRestartKeepsPriorSessionWithoutReusingItsContext()
    {
        await using var f=await Fixture.Create();var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider.Wait=async ct=>{started.TrySetResult();await release.Task.WaitAsync(ct);};
        var first=f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content="第一個工作階段的問題"},default);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content="同時送出"},default))).StatusCode);
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Restart(f.Course,f.Student,"premorbid",default))).StatusCode);
        release.TrySetResult();await first;f.Provider.Wait=null;
        var closed=await f.Workflow.Restart(f.Course,f.Student,"premorbid",default);Assert.Null(closed.ActiveSessionId);Assert.Equal("closed",closed.Sessions.Single().Status);
        var next=await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content="全新的問題"},default);
        Assert.Equal(2,next.Sessions.Count);Assert.Equal(2,f.Provider.Requests.Last().Count);Assert.DoesNotContain("第一個工作階段的問題",string.Join("",f.Provider.Requests.Last().Select(m=>m.Content)));
    }

    [FormativeDatabaseFact] public async Task WithdrawalDuringInferenceKeepsQuestionButDoesNotPersistAnUnauthorizedReply()
    {
        await using var f=await Fixture.Create();var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider.Wait=async ct=>{started.TrySetResult();await release.Task.WaitAsync(ct);};
        var send=f.Workflow.Send(f.Course,f.Student,"psychosocial",new(){RequestId=Guid.NewGuid(),Content="待回覆時退選"},default);await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await f.Sql("UPDATE course_members SET status='inactive' WHERE course_id=$1 AND user_id=$2",f.Course,f.Student);release.TrySetResult();
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>send)).StatusCode);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.ReviewHistory(f.Course,f.Teacher,f.Student,"psychosocial",default))).StatusCode);
        Assert.Equal("failed",await f.Sql("SELECT response_status FROM case_analysis_messages m JOIN case_analysis_sessions s ON s.id=m.session_id WHERE s.course_id=$1 AND s.student_id=$2 AND m.role='user'",f.Course,f.Student));
        Assert.Equal(0L,await f.Sql("SELECT count(*) FROM case_analysis_messages m JOIN case_analysis_sessions s ON s.id=m.session_id WHERE s.course_id=$1 AND s.student_id=$2 AND m.role='assistant'",f.Course,f.Student));
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.History(f.Course,f.Student,"psychosocial",default))).StatusCode);
        Assert.Equal(0,(await f.Workflow.Records(f.Course,f.Teacher,default)).Single(row=>row.Id==f.Student).Statistics.RoundTrips);
    }

    [FormativeDatabaseFact] public async Task LongStageHistoryStaysInDatabaseWithoutConsumingLaterRoundContext()
    {
        await using var f=await Fixture.Create();
        for(var i=0;i<8;i++)
        {
            var answer=$"round-{i}:"+new string('x',2000);
            await f.Workflow.Send(f.Course,f.Student,"illnessCourse",new(){RequestId=Guid.NewGuid(),Content=answer},default);
            var request=f.Provider.Requests.Last();Assert.Equal(new[]{"system","user"},request.Select(m=>m.Role));Assert.Equal(answer,request.Last().Content);
        }
        var history=(await f.Workflow.History(f.Course,f.Student,"illnessCourse",default)).Sessions.Single();
        Assert.Equal(16,history.Messages.Count);Assert.Equal(8,history.Statistics.RoundTrips);
    }
    [FormativeDatabaseFact] public async Task TruncatedReplyDoesNotPublishPartialFeedbackAndSameSavedAnswerCanRetry()
    {
        await using var f=await Fixture.Create();var id=Guid.NewGuid();f.Provider.Metrics=f.Provider.Metrics with{DoneReason="length"};
        var failure=await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"illnessCourse",new(){RequestId=id,Content="合成輸出上限測試"},default));
        Assert.Equal(502,failure.StatusCode);Assert.Contains("輸出上限",failure.Message);
        var failed=(await f.Workflow.History(f.Course,f.Student,"illnessCourse",default)).Sessions.Single();Assert.Equal("failed",Assert.Single(failed.Messages).Status);Assert.Equal(0,failed.Statistics.RoundTrips);
        f.Provider.Metrics=f.Provider.Metrics with{DoneReason="stop"};var retried=(await f.Workflow.Retry(f.Course,f.Student,"illnessCourse",id,default)).Sessions.Single();
        Assert.Equal(2,retried.Messages.Count);Assert.Equal(1,retried.Statistics.RoundTrips);Assert.Equal(id,retried.Messages.First().Id);Assert.All(f.Provider.Requests,request=>Assert.Equal(2,request.Count));
    }
    [FormativeDatabaseFact] public async Task ContextRejectionRetainsOriginalQuestionWithoutCallingTheModel()
    {
        await using var f=await Fixture.Create();
        var limited=new CaseAnalysisChat(f.Db,f.Provider,Microsoft.Extensions.Options.Options.Create(new OllamaOptions{Model="mock-model"}),Microsoft.Extensions.Options.Options.Create(new CaseChatOptions{MaxContextBytes=512}),TimeProvider.System);
        Assert.Equal(413,(await Assert.ThrowsAsync<CaseChatException>(()=>limited.Send(f.Course,f.Student,"illnessCourse",new(){RequestId=Guid.NewGuid(),Content="保留這份原始提問"},default))).StatusCode);
        Assert.Empty(f.Provider.Requests);var record=Assert.Single((await limited.History(f.Course,f.Student,"illnessCourse",default)).Sessions.Single().Messages);Assert.Equal("保留這份原始提問",record.Content);Assert.Equal("failed",record.Status);
        var retried=await f.Workflow.Retry(f.Course,f.Student,"illnessCourse",record.Id,default);
        Assert.Equal(new[]{"system","user"},f.Provider.Requests.Single().Select(m=>m.Role));Assert.Equal(record.Content,f.Provider.Requests.Single().Last().Content);
        Assert.Equal(1,retried.Sessions.Single().Statistics.RoundTrips);
    }

    [FormativeDatabaseFact] public async Task RawControllerNeverCreatesDatabaseHistoryOrCountsAndCannotLeakIntoStageContext()
    {
        await using var f=await Fixture.Create();var settings=Microsoft.Extensions.Options.Options.Create(new CaseChatOptions());var model=Microsoft.Extensions.Options.Options.Create(new OllamaOptions{Model="mock-model"});
        using var raw=new RawChatSessions(TimeProvider.System,settings,model);
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,f.Teacher.ToString())],"test"))};
        var controller=new CaseAnalysisChatController(f.Workflow,raw,f.Provider,model,settings,new LearnerExperience(new HttpContextAccessor{HttpContext=context})){ControllerContext=new(){HttpContext=context}};
        var created=JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.CreateRaw(f.Course,default)).Value);var id=created.GetProperty("id").GetGuid();
        for(var turn=0;turn<3;turn++)Assert.IsType<OkObjectResult>(await controller.SendRaw(f.Course,id,new(){Content="Raw隔離問題"+turn},default));
        Assert.All(f.Provider.Requests,request=>{Assert.Single(request.Where(m=>m.Role=="system"));Assert.Contains("zh-TW",request[0].Content);});
        Assert.Equal(6,f.Provider.Requests.Last().Count);
        Assert.IsType<OkObjectResult>(await controller.ClearRaw(f.Course,id,default));
        Assert.Equal(0L,await f.Sql("SELECT count(*) FROM case_analysis_sessions WHERE course_id=$1",f.Course));
        Assert.All(await f.Workflow.Records(f.Course,f.Teacher,default),row=>Assert.Equal(0,row.Statistics.RoundTrips));
        await f.Workflow.Send(f.Course,f.Teacher,"premorbid",new(){RequestId=Guid.NewGuid(),Content="正式階段的問題"},default);
        Assert.Equal(new[]{"system","user"},f.Provider.Requests.Last().Select(m=>m.Role));Assert.DoesNotContain("Raw隔離問題",string.Join("",f.Provider.Requests.Last().Select(m=>m.Content)));Assert.DoesNotContain("language of the user's current message",f.Provider.Requests.Last()[0].Content);
    }
    [FormativeDatabaseFact] public async Task RawReplyRechecksAuthorizationBeforeCommittingMemoryOrReturningContent()
    {
        await using var f=await Fixture.Create();var settings=Microsoft.Extensions.Options.Options.Create(new CaseChatOptions());var model=Microsoft.Extensions.Options.Options.Create(new OllamaOptions{Model="mock-model"});
        using var raw=new RawChatSessions(TimeProvider.System,settings,model);
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,f.Teacher.ToString())],"test"))};
        var controller=new CaseAnalysisChatController(f.Workflow,raw,f.Provider,model,settings,new LearnerExperience(new HttpContextAccessor{HttpContext=context})){ControllerContext=new(){HttpContext=context}};
        var created=JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.CreateRaw(f.Course,default)).Value);var id=created.GetProperty("id").GetGuid();
        var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider.Wait=async ct=>{started.TrySetResult();await release.Task.WaitAsync(ct);};
        var send=controller.SendRaw(f.Course,id,new(){Content="等待回覆時退選"},default);await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await f.Sql("UPDATE course_members SET status='inactive' WHERE course_id=$1 AND user_id=$2",f.Course,f.Teacher);release.TrySetResult();
        Assert.Equal(404,Assert.IsType<ObjectResult>(await send).StatusCode);
        await f.Sql("UPDATE course_members SET status='active' WHERE course_id=$1 AND user_id=$2",f.Course,f.Teacher);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>raw.Send(f.Course,f.Teacher,id,"舊記憶不可復活",f.Provider,default))).StatusCode);
    }
    [FormativeDatabaseFact] public async Task ExpiredLeaseIsRetryableAndItsLateOriginalReplyCannotOverwriteTheSuccessfulRetry()
    {
        await using var f=await Fixture.Create();var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider.Wait=async ct=>{started.TrySetResult();await release.Task.WaitAsync(ct);};var id=Guid.NewGuid();
        var original=f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=id,Content="逾時但原文保留"},default);await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await f.Sql("UPDATE case_analysis_messages SET lease_until=now()-interval '1 second' WHERE id=$1",id);
        var history=await f.Workflow.History(f.Course,f.Student,"premorbid",default);Assert.Equal("failed",history.Sessions.Single().Messages.Single().Status);
        f.Provider.Wait=null;await f.Workflow.Retry(f.Course,f.Student,"premorbid",id,default);
        release.TrySetResult();Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>original)).StatusCode);
        var final=(await f.Workflow.History(f.Course,f.Student,"premorbid",default)).Sessions.Single();Assert.Equal(2,final.Messages.Count);Assert.Equal(1,final.Statistics.RoundTrips);Assert.Equal(1,final.Statistics.StudentMessages);
    }
    [FormativeDatabaseFact] public async Task ConcurrentCourseSettingsSerializeWithoutSharedLockUpgradeDeadlocks()
    {
        await using var f=await Fixture.Create();var name="case-setting-"+Guid.NewGuid().ToString("N");
        var connection=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")){ApplicationName=name};
        await using var db=NpgsqlDataSource.Create(connection.ConnectionString);
        var workflow=new CaseAnalysisChat(db,f.Provider,Microsoft.Extensions.Options.Options.Create(new OllamaOptions()),Microsoft.Extensions.Options.Options.Create(new CaseChatOptions()),TimeProvider.System);
        await using var holder=await f.Db.OpenConnectionAsync();await using var held=await holder.BeginTransactionAsync();
        await using(var gate=new NpgsqlCommand("SELECT id FROM courses WHERE id=$1 FOR SHARE",holder)){gate.Parameters.AddWithValue(f.Course);await gate.ExecuteScalarAsync();}
        var first=workflow.SetEnabled(f.Course,f.Teacher,false,default);var second=workflow.SetEnabled(f.Course,f.Teacher,true,default);
        try
        {
            for(var poll=0;poll<100;poll++)
            {
                if(Equals(await f.Sql("SELECT count(*) FROM pg_stat_activity WHERE application_name=$1 AND wait_event_type='Lock'",name),2L))break;
                await Task.Delay(20);
            }
        }
        finally{await held.CommitAsync();}
        await Task.WhenAll(first,second);
        Assert.Equal(2L,await f.Sql("SELECT count(*) FROM audit_events WHERE entity_id=$1 AND action='course.case_analysis_setting'",f.Course));
    }

    private sealed class Fixture:IAsyncDisposable
    {
        public NpgsqlDataSource Db=null!;public Guid Course=Guid.NewGuid(),Other=Guid.NewGuid(),Teacher=Guid.NewGuid(),Student=Guid.NewGuid(),Peer=Guid.NewGuid(),Ta=Guid.NewGuid();
        public Provider Provider=new();public CaseAnalysisChat Workflow=null!;
        public async Task<object?> Sql(string sql,params object[] values){await using var cmd=Db.CreateCommand(sql);foreach(var value in values)cmd.Parameters.AddWithValue(value);return await cmd.ExecuteScalarAsync();}
        public static async Task<Fixture> Create()
        {
            var f=new Fixture{Db=NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")!)};
            foreach(var id in new[]{f.Teacher,f.Student,f.Peer,f.Ta})await f.Sql("INSERT INTO app_users(id,display_name) VALUES($1,'合成個案聊天帳號')",id);
            foreach(var id in new[]{f.Course,f.Other})await f.Sql("INSERT INTO courses(id,code,title,term_code,owner_user_id,status,case_analysis_enabled) VALUES($1,$2,'合成個案分析','115-1',$3,'active',true)",id,"CHAT-"+id.ToString("N"),f.Teacher);
            foreach(var (id,role) in new[]{(f.Teacher,"teacher"),(f.Student,"student"),(f.Peer,"student"),(f.Ta,"ta")})await f.Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,$3)",f.Course,id,role);
            f.Workflow=new(f.Db,f.Provider,Microsoft.Extensions.Options.Options.Create(new OllamaOptions{Model="mock-model"}),Microsoft.Extensions.Options.Options.Create(new CaseChatOptions()),TimeProvider.System);
            return f;
        }
        public async ValueTask DisposeAsync()
        {
            await Sql("DELETE FROM audit_events WHERE entity_id=$1",Course);
            await Sql("DELETE FROM case_formal_submissions WHERE course_id=$1",Course);
            await Sql("DELETE FROM case_classification_rule_versions WHERE course_id=$1",Course);
            await Sql("DELETE FROM assignments WHERE course_id=$1",Course);
            await Sql("DELETE FROM case_analysis_messages WHERE session_id IN (SELECT id FROM case_analysis_sessions WHERE course_id=$1)",Course);
            await Sql("DELETE FROM case_analysis_sessions WHERE course_id=$1",Course);
            await Sql("DELETE FROM course_members WHERE course_id=$1",Course);await Sql("DELETE FROM courses WHERE id IN ($1,$2)",Course,Other);
            await Sql("DELETE FROM app_users WHERE id=ANY($1)",new[]{Teacher,Student,Peer,Ta});await Db.DisposeAsync();
        }
    }
    private sealed class Provider:IOllamaClient
    {
        public AnalysisStage? FeedbackStage;
        public Task<(string Content,string? Thinking,RequestMetrics Metrics)> CaseFeedbackAsync(IReadOnlyList<OllamaMessage> messages,AnalysisStage stage,CancellationToken ct){FeedbackStage=stage;return CaseFeedbackAsync(messages,ct);}
        public string? Feedback;
        public async Task<(string Content,string? Thinking,RequestMetrics Metrics)> CaseFeedbackAsync(IReadOnlyList<OllamaMessage> messages,CancellationToken ct){var reply=await ConversationAsync(messages,ct);return (Feedback??JsonSerializer.Serialize(new{feedback=reply.Content,annotations=Array.Empty<object>()}),null,reply.Metrics);}
        public RequestMetrics Metrics=new(0,0,0,0,0,0,0,0,0,"stop");
        public bool Fail;public Func<CancellationToken,Task>? Wait;public List<IReadOnlyList<OllamaMessage>> Requests=[];
        public async Task<(string Content,string? Thinking,RequestMetrics Metrics)> ConversationAsync(IReadOnlyList<OllamaMessage> messages,CancellationToken ct){Requests.Add(messages.ToArray());if(Wait is {} wait)await wait(ct);if(Fail)throw new HttpRequestException("private upstream diagnostic must not be stored");return ("合成教學回覆",null,Metrics);}
        public Task<(string Content,string? Thinking,RequestMetrics Metrics)> ChatAsync(IReadOnlyList<OllamaMessage> m,CancellationToken ct)=>throw new Exception("Legacy system/memory path forbidden");
        public Task<(string Content,string? Thinking,RequestMetrics Metrics)> AssessAsync(IReadOnlyList<OllamaMessage> m,bool compare,CancellationToken ct)=>throw new Exception("Grading forbidden");
        public Task<string> SummarizeAsync(string s,IReadOnlyList<ChatMessage> m,int max,CancellationToken ct)=>throw new Exception("Summary forbidden");
        public Task<OllamaHealthResult> CheckHealthAsync(CancellationToken ct)=>throw new NotSupportedException();
    }
}
