using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Security.Claims;
using System.Text.Json;
using AiTeachingAssistant.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AiTeachingAssistant.Tests;

public sealed class CaseChatModelSelectionTests
{
    [FormativeDatabaseFact] public async Task StaffSelectionsAreStoredPerRoundWhileStudentsUseDefaultAndCannotOverrideModels()
    {
        await using var f=await SelectionFixture.Create();var first=Guid.NewGuid();
        var history=await f.Workflow.Send(f.Course,f.Teacher,"illnessCourse",new(){RequestId=first,Content="相同的合成模型比較作答",ModelProfile="llama3"},default);
        Assert.All(history.Sessions.Single().Messages,m=>Assert.Equal("selected-llama",m.ModelName));
        history=await f.Workflow.Send(f.Course,f.Teacher,"illnessCourse",new(){RequestId=Guid.NewGuid(),Content="相同的合成模型比較作答",ModelProfile="qwen"},default);
        Assert.Equal(new[]{"selected-llama","selected-llama","selected-qwen","selected-qwen"},history.Sessions.Single().Messages.Select(m=>m.ModelName));
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Teacher,"illnessCourse",new(){RequestId=first,Content="相同的合成模型比較作答",ModelProfile="qwen"},default))).StatusCode);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"illnessCourse",new(){RequestId=Guid.NewGuid(),Content="學生選模作答",ModelProfile="llama3"},default))).StatusCode);
        var learner=await f.Workflow.Send(f.Course,f.Student,"illnessCourse",new(){RequestId=Guid.NewGuid(),Content="預設模型學生作答"},default);
        Assert.All(learner.Sessions.Single().Messages.TakeLast(2),m=>Assert.Equal("environment-default",m.ModelName));
        Assert.Equal(1,(await f.Workflow.Records(f.Course,f.Teacher,default)).Single(s=>s.Id==f.Student).Statistics.RoundTrips);
    }

    [FormativeDatabaseFact] public async Task StudentsCannotOverrideModelsAndLegacyNullRetryRespectsWhitelistOwnershipAndEnrollment()
    {
        await using var f=await SelectionFixture.Create();
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content="合成未知模型",ModelProfile="../../host"},default))).StatusCode);
        Assert.Equal(400,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Teacher,"premorbid",new(){RequestId=Guid.NewGuid(),Content="合成未知模型",ModelProfile="../../host"},default))).StatusCode);
        // Seed an immutable failed turn created before students lost model selection.
        var request=Guid.NewGuid();var session=Guid.NewGuid();
        await f.Sql("INSERT INTO case_analysis_sessions(id,course_id,student_id,stage,next_sequence) VALUES($1,$2,$3,'premorbid',2)",session,f.Course,f.Student);
        await f.Sql("INSERT INTO case_analysis_messages(id,session_id,role,content,sequence_number,response_status,model_name,prompt_version,created_at,failure_code) VALUES($1,$2,'user','合成舊版學生重試',1,'failed','selected-qwen',$3,$4,'provider_failed')",request,session,CaseChatPrompts.VersionFor(AnalysisStage.Premorbid),DateTimeOffset.UtcNow);
        foreach(var profile in new[]{"llama3","qwen"})Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Retry(f.Course,f.Student,"premorbid",request,default,profile))).StatusCode);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Retry(f.Course,f.Ta,"premorbid",request,default,"qwen"))).StatusCode);
        var retried=await f.Workflow.Retry(f.Course,f.Student,"premorbid",request,default);
        Assert.All(retried.Sessions.Single().Messages,m=>Assert.Equal("selected-qwen",m.ModelName));Assert.Equal(1,retried.Sessions.Single().Statistics.RoundTrips);
        f.Default.Before=()=>f.Sql("UPDATE course_members SET status='inactive' WHERE course_id=$1 AND user_id=$2",f.Course,f.Student);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"functionalAssessment",new(){RequestId=Guid.NewGuid(),Content="合成退選情境"},default))).StatusCode);
        Assert.Equal(0L,await f.Sql("SELECT count(*) FROM case_analysis_messages m JOIN case_analysis_sessions s ON s.id=m.session_id WHERE s.course_id=$1 AND s.student_id=$2 AND s.stage='functionalAssessment' AND m.role='assistant'",f.Course,f.Student));
    }

    [FormativeDatabaseFact] public async Task StaffControllerOffersModelsAndRoutesRawPerMessageWithoutDatabaseWrites()
    {
        await using var f=await SelectionFixture.Create();var limits=Microsoft.Extensions.Options.Options.Create(new CaseChatOptions());var model=Microsoft.Extensions.Options.Options.Create(new OllamaOptions{Model="environment-default"});
        using var raw=new RawChatSessions(TimeProvider.System,limits,model);
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,f.Teacher.ToString())],"test"))};
        var accessor=new HttpContextAccessor{HttpContext=context};var experience=new LearnerExperience(accessor);var inspection=new OllamaRequestInspection(accessor,experience);
        var controller=new CaseAnalysisChatController(f.Workflow,raw,new Reply(),model,limits,experience,inspection,f.Models){ControllerContext=new(){HttpContext=context}};
        var config=JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.Configuration(f.Course,default)).Value);
        Assert.True(config.GetProperty("canChooseModel").GetBoolean());Assert.True(config.GetProperty("canInspectRequests").GetBoolean());Assert.Equal(2,config.GetProperty("modelProfiles").GetArrayLength());
        var created=JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.CreateRaw(f.Course,default)).Value);var id=created.GetProperty("id").GetGuid();
        Assert.Equal("selected-llama",Assert.IsType<RawChatReply>(Assert.IsType<OkObjectResult>(await controller.SendRaw(f.Course,id,new(){Content="first synthetic raw",ModelProfile="llama3"},default)).Value).ModelName);
        var reply=Assert.IsType<RawChatReply>(Assert.IsType<OkObjectResult>(await controller.SendRaw(f.Course,id,new(){Content="second synthetic raw",ModelProfile="qwen"},default)).Value);
        Assert.Equal(new[]{"selected-llama","selected-llama","selected-qwen","selected-qwen"},reply.Messages.Select(m=>m.ModelName));
        Assert.Equal(4,f.Models.Qwen.Inputs.Single().Count);Assert.Single(f.Models.Qwen.Inputs.Single().Where(m=>m.Role=="system"));Assert.Contains("zh-TW",f.Models.Qwen.Inputs.Single()[0].Content);Assert.Null(inspection.Current);
        Assert.Equal(0L,await f.Sql("SELECT count(*) FROM case_analysis_sessions WHERE course_id=$1",f.Course));
        var invalid=Assert.IsType<ObjectResult>(await controller.SendRaw(f.Course,id,new(){Content="invalid model",ModelProfile="http://other-host"},default));Assert.Equal(400,invalid.StatusCode);
        await f.Sql("UPDATE courses SET status='archived' WHERE id=$1",f.Course);
        config=JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.Configuration(f.Course,default)).Value);Assert.False(config.GetProperty("canChooseModel").GetBoolean());Assert.False(config.GetProperty("canMaintainRules").GetBoolean());
    }
    [FormativeDatabaseFact] public async Task StudentControllerHidesModelChoicesAndRejectsRawEvenForLegacyMemory()
    {
        await using var f=await SelectionFixture.Create();var limits=Microsoft.Extensions.Options.Options.Create(new CaseChatOptions());var model=Microsoft.Extensions.Options.Options.Create(new OllamaOptions{Model="environment-default"});
        using var raw=new RawChatSessions(TimeProvider.System,limits,model);
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,f.Student.ToString())],"test"))};
        var accessor=new HttpContextAccessor{HttpContext=context};var experience=new LearnerExperience(accessor);var inspection=new OllamaRequestInspection(accessor,experience);
        var controller=new CaseAnalysisChatController(f.Workflow,raw,f.Default,model,limits,experience,inspection,f.Models){ControllerContext=new(){HttpContext=context}};
        var config=JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.Configuration(f.Course,default)).Value);
        Assert.False(config.GetProperty("canChooseModel").GetBoolean());Assert.False(config.GetProperty("canUseTesting").GetBoolean());Assert.False(config.GetProperty("canInspectRequests").GetBoolean());Assert.Equal(JsonValueKind.Null,config.GetProperty("modelProfiles").ValueKind);
        Assert.Equal(403,Assert.IsType<ObjectResult>(await controller.CreateRaw(f.Course,default)).StatusCode);
        var legacy=raw.Create(f.Course,f.Student);
        Assert.Equal(403,Assert.IsType<ObjectResult>(await controller.SendRaw(f.Course,legacy,new(){Content="合成舊記憶拒絕",ModelProfile="qwen"},default)).StatusCode);
        Assert.Empty(f.Models.Qwen.Inputs);Assert.Empty(f.Default.Inputs);Assert.Null(inspection.Current);Assert.Equal(0L,await f.Sql("SELECT count(*) FROM case_analysis_sessions WHERE course_id=$1",f.Course));
    }

    [FormativeDatabaseFact] public async Task PromotionDuringStudentModelCallCannotMoveTheReplyIntoPracticeHistory()
    {
        await using var f=await SelectionFixture.Create();
        f.Default.Before=()=>f.Sql("UPDATE course_members SET course_role='ta' WHERE course_id=$1 AND user_id=$2",f.Course,f.Student);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content="合成角色變更作答"},default))).StatusCode);
        Assert.Equal(0L,await f.Sql("SELECT count(*) FROM case_analysis_messages m JOIN case_analysis_sessions s ON s.id=m.session_id WHERE s.course_id=$1 AND m.role='assistant'",f.Course));
    }
    [FormativeDatabaseFact] public async Task ConcurrentStaffChoicesAndRetryRemainIsolatedAndDemotionPreventsSelectedReplySaving()
    {
        await using var f=await SelectionFixture.Create();
        await Task.WhenAll(f.Workflow.Send(f.Course,f.Teacher,"illnessCourse",new(){RequestId=Guid.NewGuid(),Content="教師合成作答",ModelProfile="llama3"},default),f.Workflow.Send(f.Course,f.Ta,"illnessCourse",new(){RequestId=Guid.NewGuid(),Content="助教合成作答",ModelProfile="qwen"},default));
        Assert.All((await f.Workflow.History(f.Course,f.Teacher,"illnessCourse",default)).Sessions.Single().Messages,m=>Assert.Equal("selected-llama",m.ModelName));
        Assert.All((await f.Workflow.History(f.Course,f.Ta,"illnessCourse",default)).Sessions.Single().Messages,m=>Assert.Equal("selected-qwen",m.ModelName));
        f.Models.Llama.Fail=true;var failed=Guid.NewGuid();await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Teacher,"illnessCourse",new(){RequestId=failed,Content="合成可重試作答",ModelProfile="llama3"},default));f.Models.Llama.Fail=false;
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Retry(f.Course,f.Teacher,"illnessCourse",failed,default,"qwen"))).StatusCode);
        var retried=await f.Workflow.Retry(f.Course,f.Teacher,"illnessCourse",failed,default,"llama3");Assert.Equal("selected-llama",retried.Sessions.Single().Messages.Last().ModelName);
        f.Models.Llama.Before=()=>f.Sql("UPDATE course_members SET course_role='student' WHERE course_id=$1 AND user_id=$2",f.Course,f.Teacher);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Teacher,"premorbid",new(){RequestId=Guid.NewGuid(),Content="合成降權情境",ModelProfile="llama3"},default))).StatusCode);
        Assert.Equal(0L,await f.Sql("SELECT count(*) FROM case_analysis_messages m JOIN case_analysis_sessions s ON s.id=m.session_id WHERE s.course_id=$1 AND s.student_id=$2 AND s.stage='premorbid' AND m.role='assistant'",f.Course,f.Teacher));
    }

    private sealed class SelectionFixture:IAsyncDisposable
    {
        public NpgsqlDataSource Db=null!;public CaseAnalysisChat Workflow=null!;public ChoiceModels Models=new();public Reply Default=new();public Guid Course=Guid.NewGuid(),Teacher=Guid.NewGuid(),Ta=Guid.NewGuid(),Student=Guid.NewGuid();
        public async Task<object?> Sql(string sql,params object[] values){await using var cmd=Db.CreateCommand(sql);foreach(var value in values)cmd.Parameters.AddWithValue(value);return await cmd.ExecuteScalarAsync();}
        public static async Task<SelectionFixture> Create(){
            var f=new SelectionFixture{Db=NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")!)};
            foreach(var id in new[]{f.Teacher,f.Ta,f.Student})await f.Sql("INSERT INTO app_users(id,display_name) VALUES($1,'合成模型比較帳號')",id);
            await f.Sql("INSERT INTO courses(id,code,title,term_code,owner_user_id,status,case_analysis_enabled) VALUES($1,$2,'合成模型比較','115-1',$3,'active',true)",f.Course,"MODEL-"+f.Course.ToString("N"),f.Teacher);
            foreach(var (id,role) in new[]{(f.Teacher,"teacher"),(f.Ta,"ta"),(f.Student,"student")})await f.Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,$3)",f.Course,id,role);
            f.Workflow=new(f.Db,f.Default,Microsoft.Extensions.Options.Options.Create(new OllamaOptions{Model="environment-default"}),Microsoft.Extensions.Options.Options.Create(new CaseChatOptions()),TimeProvider.System,f.Models);return f;
        }
        public async ValueTask DisposeAsync(){await Sql("DELETE FROM case_analysis_messages WHERE session_id IN (SELECT id FROM case_analysis_sessions WHERE course_id=$1)",Course);await Sql("DELETE FROM case_analysis_sessions WHERE course_id=$1",Course);await Sql("DELETE FROM course_members WHERE course_id=$1",Course);await Sql("DELETE FROM courses WHERE id=$1",Course);await Sql("DELETE FROM app_users WHERE id=ANY($1)",new[]{Teacher,Ta,Student});await Db.DisposeAsync();}
    }
    private sealed class ChoiceModels:ICaseChatModels
    {
        public Reply Llama=new(),Qwen=new();public IReadOnlyList<CaseChatModelProfile> Profiles=>[new("llama3","Llama","selected-llama"),new("qwen","Qwen","selected-qwen")];
        public CaseChatModelRuntime Select(string key)=>key switch{"llama3"=>new(new(key,new(){Model="selected-llama"},new()),Llama),"qwen"=>new(new(key,new(){Model="selected-qwen"},new()),Qwen),_=>throw new CaseChatException(400,"unknown profile")};
        public CaseChatModelRuntime FromChoice(CaseChatModelChoice choice)=>new(choice,choice.Profile=="llama3"?Llama:Qwen);
    }
    private sealed class Reply:IOllamaClient
    {
        public bool Fail;public Func<Task<object?>>? Before;public List<IReadOnlyList<OllamaMessage>> Inputs=[];
        public async Task<(string Content,string? Thinking,RequestMetrics Metrics)> ConversationAsync(IReadOnlyList<OllamaMessage> messages,CancellationToken ct){Inputs.Add(messages.ToArray());if(Before is not null)await Before();return ("synthetic raw reply",null,new(0,0,0,0,0,0,0,0,0,"stop"));}
        public async Task<(string Content,string? Thinking,RequestMetrics Metrics)> CaseFeedbackAsync(IReadOnlyList<OllamaMessage> messages,AnalysisStage stage,CancellationToken ct){if(Before is not null)await Before();if(Fail)throw new HttpRequestException("private failure");return ("{\"feedback\":\"合成模型回饋\",\"annotations\":[]}",null,new(0,0,0,0,0,0,0,0,0,"stop"));}
        public Task<(string Content,string? Thinking,RequestMetrics Metrics)> ChatAsync(IReadOnlyList<OllamaMessage> m,CancellationToken ct)=>throw new NotSupportedException();
        public Task<(string Content,string? Thinking,RequestMetrics Metrics)> AssessAsync(IReadOnlyList<OllamaMessage> m,bool compare,CancellationToken ct)=>throw new NotSupportedException();
        public Task<string> SummarizeAsync(string s,IReadOnlyList<ChatMessage> m,int max,CancellationToken ct)=>throw new NotSupportedException();
        public Task<OllamaHealthResult> CheckHealthAsync(CancellationToken ct)=>throw new NotSupportedException();
    }
}
