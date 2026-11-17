using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AiTeachingAssistant.Tests;

public sealed class CaseChatModelSelectionTests
{
    [FormativeDatabaseFact] public async Task SelectedModelsAreStoredPerRoundAndStudentsCannotSelectOrChangeAnExistingRequestModel()
    {
        await using var f=await SelectionFixture.Create();var first=Guid.NewGuid();
        var history=await f.Workflow.Send(f.Course,f.Teacher,"illnessCourse",new(){RequestId=first,Content="相同的合成模型比較作答",ModelProfile="llama3"},default);
        Assert.All(history.Sessions.Single().Messages,m=>Assert.Equal("selected-llama",m.ModelName));
        history=await f.Workflow.Send(f.Course,f.Teacher,"illnessCourse",new(){RequestId=Guid.NewGuid(),Content="相同的合成模型比較作答",ModelProfile="qwen"},default);
        Assert.Equal(new[]{"selected-llama","selected-llama","selected-qwen","selected-qwen"},history.Sessions.Single().Messages.Select(m=>m.ModelName));
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Teacher,"illnessCourse",new(){RequestId=first,Content="相同的合成模型比較作答",ModelProfile="qwen"},default))).StatusCode);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"illnessCourse",new(){RequestId=Guid.NewGuid(),Content="不可選模",ModelProfile="llama3"},default))).StatusCode);
        var learner=await f.Workflow.Send(f.Course,f.Student,"illnessCourse",new(){RequestId=Guid.NewGuid(),Content="預設模型學生作答"},default);
        Assert.All(learner.Sessions.Single().Messages,m=>Assert.Equal("environment-default",m.ModelName));
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
        public NpgsqlDataSource Db=null!;public CaseAnalysisChat Workflow=null!;public ChoiceModels Models=new();public Guid Course=Guid.NewGuid(),Teacher=Guid.NewGuid(),Ta=Guid.NewGuid(),Student=Guid.NewGuid();
        public async Task<object?> Sql(string sql,params object[] values){await using var cmd=Db.CreateCommand(sql);foreach(var value in values)cmd.Parameters.AddWithValue(value);return await cmd.ExecuteScalarAsync();}
        public static async Task<SelectionFixture> Create(){
            var f=new SelectionFixture{Db=NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")!)};
            foreach(var id in new[]{f.Teacher,f.Ta,f.Student})await f.Sql("INSERT INTO app_users(id,display_name) VALUES($1,'合成模型比較帳號')",id);
            await f.Sql("INSERT INTO courses(id,code,title,term_code,owner_user_id,status,case_analysis_enabled) VALUES($1,$2,'合成模型比較','115-1',$3,'active',true)",f.Course,"MODEL-"+f.Course.ToString("N"),f.Teacher);
            foreach(var (id,role) in new[]{(f.Teacher,"teacher"),(f.Ta,"ta"),(f.Student,"student")})await f.Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,$3)",f.Course,id,role);
            f.Workflow=new(f.Db,new Reply(),Microsoft.Extensions.Options.Options.Create(new OllamaOptions{Model="environment-default"}),Microsoft.Extensions.Options.Options.Create(new CaseChatOptions()),TimeProvider.System,f.Models);return f;
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
        public bool Fail;public Func<Task<object?>>? Before;
        public async Task<(string Content,string? Thinking,RequestMetrics Metrics)> CaseFeedbackAsync(IReadOnlyList<OllamaMessage> messages,AnalysisStage stage,CancellationToken ct){if(Before is not null)await Before();if(Fail)throw new HttpRequestException("private failure");return ("{\"feedback\":\"合成模型回饋\",\"annotations\":[]}",null,new(0,0,0,0,0,0,0,0,0,"stop"));}
        public Task<(string Content,string? Thinking,RequestMetrics Metrics)> ChatAsync(IReadOnlyList<OllamaMessage> m,CancellationToken ct)=>throw new NotSupportedException();
        public Task<(string Content,string? Thinking,RequestMetrics Metrics)> AssessAsync(IReadOnlyList<OllamaMessage> m,bool compare,CancellationToken ct)=>throw new NotSupportedException();
        public Task<string> SummarizeAsync(string s,IReadOnlyList<ChatMessage> m,int max,CancellationToken ct)=>throw new NotSupportedException();
        public Task<OllamaHealthResult> CheckHealthAsync(CancellationToken ct)=>throw new NotSupportedException();
    }
}
