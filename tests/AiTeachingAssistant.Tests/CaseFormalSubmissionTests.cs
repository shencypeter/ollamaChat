using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Npgsql;
using Xunit;

namespace AiTeachingAssistant.Tests;

public sealed partial class CaseAnalysisChatTests
{
    private static async Task<Guid> Analyze(Fixture f,Guid? actor=null,string stage="premorbid",string answer="合成正式作答")
    {var id=Guid.NewGuid();await f.Workflow.Send(f.Course,actor??f.Student,stage,new(){RequestId=id,Content=answer},default);return id;}

    [FormativeDatabaseFact] public async Task FormalSubmissionIsExplicitIdempotentAndIndependentOfAiCounts()
    {
        await using var f=await Fixture.Create();Guid message=Guid.Empty;
        for(var i=0;i<10;i++)message=await Analyze(f,answer:$"合成分析版本 {i}");
        Assert.Empty((await f.Workflow.FormalSubmissions(f.Course,f.Teacher,default)).Submissions);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.ReviewHistory(f.Course,f.Teacher,f.Student,"premorbid",default))).StatusCode);
        var input=new CaseFormalSubmissionInput{RequestId=Guid.NewGuid(),MessageId=message};
        var all=await Task.WhenAll(f.Workflow.SubmitFormal(f.Course,f.Student,input,default),f.Workflow.SubmitFormal(f.Course,f.Student,input,default));
        Assert.All(all,envelope=>Assert.Single(envelope.Submissions));
        var saved=Assert.Single((await f.Workflow.FormalSubmissions(f.Course,f.Teacher,default)).Submissions);
        Assert.Equal(1,saved.Version);Assert.Equal(input.RequestId,saved.Id);Assert.Equal("合成分析版本 9",saved.Answer);Assert.Equal("submitted",saved.Status);
        Assert.Equal(10,(await f.Workflow.Records(f.Course,f.Teacher,default)).Single(s=>s.Id==f.Student).Statistics.RoundTrips);
        Assert.Equal(1L,await f.Sql("SELECT count(*) FROM audit_events WHERE entity_id=$1 AND action='case_formal.submitted'",f.Course));
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SubmitFormal(f.Course,f.Student,new(){RequestId=Guid.NewGuid(),MessageId=message,ExpectedPreviousId=saved.Id},default))).StatusCode);
    }

    [FormativeDatabaseFact] public async Task ReturnedSubmissionCreatesANewImmutableVersionAndRetainsOriginalSnapshot()
    {
        await using var f=await Fixture.Create();var first=await Analyze(f,answer:"合成正式第一版");
        var initial=Assert.Single((await f.Workflow.SubmitFormal(f.Course,f.Student,new(){RequestId=Guid.NewGuid(),MessageId=first},default)).Submissions);
        await f.Workflow.ReturnFormal(f.Course,f.Teacher,initial.Id,"請補充時點依據",default);
        await f.Workflow.ReturnFormal(f.Course,f.Teacher,initial.Id,"請補充時點依據",default);
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.ReturnFormal(f.Course,f.Teacher,initial.Id,"覆寫意見",default))).StatusCode);
        var revised=await Analyze(f,answer:"合成正式第二版");
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SubmitFormal(f.Course,f.Student,new(){RequestId=Guid.NewGuid(),MessageId=revised},default))).StatusCode);
        var envelope=await f.Workflow.SubmitFormal(f.Course,f.Student,new(){RequestId=Guid.NewGuid(),MessageId=revised,ExpectedPreviousId=initial.Id},default);
        Assert.Equal(new[]{2,1},envelope.Submissions.Select(s=>s.Version));Assert.Equal("合成正式第一版",envelope.Submissions[1].Answer);Assert.Equal("returned",envelope.Submissions[1].Status);Assert.Equal("請補充時點依據",envelope.Submissions[1].ReturnReason);
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",first,true,default))).StatusCode);
        Assert.Equal("合成正式第一版",(await f.Workflow.FormalSubmissions(f.Course,f.Teacher,default)).Submissions[1].Answer);
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.ReturnFormal(f.Course,f.Teacher,initial.Id,"請補充時點依據",default))).StatusCode);
        await Assert.ThrowsAsync<PostgresException>(()=>f.Sql("UPDATE case_formal_submissions SET answer_text='overwrite' WHERE id=$1",initial.Id));
    }

    [FormativeDatabaseFact] public async Task FormalSnapshotsEnforceOwnerCourseRoleAndTaGradingPermissions()
    {
        await using var f=await Fixture.Create();var mine=await Analyze(f);var peer=await Analyze(f,f.Peer);var practice=await Analyze(f,f.Teacher);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SubmitFormal(f.Course,f.Student,new(){RequestId=Guid.NewGuid(),MessageId=peer},default))).StatusCode);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SubmitFormal(f.Course,f.Teacher,new(){RequestId=Guid.NewGuid(),MessageId=practice},default))).StatusCode);
        var saved=Assert.Single((await f.Workflow.SubmitFormal(f.Course,f.Student,new(){RequestId=Guid.NewGuid(),MessageId=mine},default)).Submissions);
        Assert.Empty((await f.Workflow.FormalSubmissions(f.Course,f.Peer,default)).Submissions);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.ReturnFormal(f.Course,f.Student,saved.Id,"不可代為退回",default))).StatusCode);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.ReturnFormal(f.Other,f.Teacher,saved.Id,"跨課",default))).StatusCode);
        await f.Sql("UPDATE course_members SET ta_restrictions=ARRAY['grading'] WHERE course_id=$1 AND user_id=$2",f.Course,f.Ta);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.FormalSubmissions(f.Course,f.Ta,default))).StatusCode);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.ReturnFormal(f.Course,f.Ta,saved.Id,"受限制",default))).StatusCode);
        await f.Sql("UPDATE course_members SET ta_restrictions=ARRAY[]::text[] WHERE course_id=$1 AND user_id=$2",f.Course,f.Ta);
        Assert.Equal("returned",Assert.Single((await f.Workflow.ReturnFormal(f.Course,f.Ta,saved.Id,"授權助教退回",default)).Submissions).Status);
    }

    [FormativeDatabaseFact] public async Task LinkedFormalAssignmentMustBeOpenPublishedIndividualAndInSameCourse()
    {
        await using var f=await Fixture.Create();var message=await Analyze(f);var assignment=Guid.NewGuid();
        await f.Sql("INSERT INTO assignments(id,course_id,title,status) VALUES($1,$2,'合成個案作業','draft')",assignment,f.Course);
        var input=new CaseFormalSubmissionInput{RequestId=Guid.NewGuid(),MessageId=message,AssignmentId=assignment};
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SubmitFormal(f.Course,f.Student,input,default))).StatusCode);
        await f.Sql("UPDATE assignments SET status='published',due_at=now()-interval '1 minute' WHERE id=$1",assignment);
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SubmitFormal(f.Course,f.Student,input,default))).StatusCode);
        await f.Sql("UPDATE assignments SET due_at=NULL WHERE id=$1",assignment);
        Assert.Equal(assignment,Assert.Single((await f.Workflow.SubmitFormal(f.Course,f.Student,input,default)).Submissions).AssignmentId);
    }

    [FormativeDatabaseFact] public async Task FailedOrTrashedAnalysisCannotBeSubmittedAndDraftContentNeverLeaksToTeacher()
    {
        await using var f=await Fixture.Create();f.Provider.Fail=true;var failed=Guid.NewGuid();
        await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=failed,Content="合成未完成分析"},default));
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SubmitFormal(f.Course,f.Student,new(){RequestId=Guid.NewGuid(),MessageId=failed},default))).StatusCode);
        f.Provider.Fail=false;var message=await Analyze(f);await f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",message,true,default);
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SubmitFormal(f.Course,f.Student,new(){RequestId=Guid.NewGuid(),MessageId=message},default))).StatusCode);
        Assert.Empty((await f.Workflow.FormalSubmissions(f.Course,f.Teacher,default)).Submissions);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.ReviewHistory(f.Course,f.Ta,f.Student,"premorbid",default))).StatusCode);
    }

    [FormativeDatabaseFact] public async Task ReusedGlobalRequestIdHasBoundedConflictAndReplayNeedsOnlyOnePoolSlot()
    {
        await using var f=await Fixture.Create();var first=await Analyze(f);var second=await Analyze(f,stage:"illnessCourse");var id=Guid.NewGuid();
        async Task<int> Submit(Guid message)
        {try{await f.Workflow.SubmitFormal(f.Course,f.Student,new(){RequestId=id,MessageId=message},default);return 200;}catch(CaseChatException e){return e.StatusCode;}}
        var outcomes=await Task.WhenAll(Submit(first),Submit(second));Assert.Equal(new[]{200,409},outcomes.Order());
        var saved=Assert.Single((await f.Workflow.FormalSubmissions(f.Course,f.Student,default)).Submissions);
        var connection=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")){MaxPoolSize=1};
        await using var db=NpgsqlDataSource.Create(connection.ConnectionString);
        var workflow=new CaseAnalysisChat(db,f.Provider,Microsoft.Extensions.Options.Options.Create(new AiTeachingAssistant.Options.OllamaOptions{Model="mock-model"}),Microsoft.Extensions.Options.Options.Create(new AiTeachingAssistant.Options.CaseChatOptions()),TimeProvider.System);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Single((await workflow.SubmitFormal(f.Course,f.Student,new(){RequestId=id,MessageId=saved.AnalysisMessageId},timeout.Token)).Submissions);
    }
}
