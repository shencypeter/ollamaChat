using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiTeachingAssistant.Tests;

public partial class CaseAnalysisChatTests
{
    [FormativeDatabaseFact] public async Task FormalSubmissionSourcesCannotBeTrashedEvenAfterReturn()
    {
        await using var f=await Fixture.Create();var id=await Analyze(f);
        var saved=Assert.Single((await f.Workflow.SubmitFormal(f.Course,f.Student,new(){RequestId=Guid.NewGuid(),MessageId=id},default)).Submissions);
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",id,true,default))).StatusCode);
        await f.Workflow.ReturnFormal(f.Course,f.Teacher,saved.Id,"合成退回理由",default);
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",id,true,default))).StatusCode);
        Assert.Null((await f.Workflow.History(f.Course,f.Student,"premorbid",default)).Sessions.Single().Messages.Single(m=>m.Id==id).TrashedAt);
        Assert.Equal(0L,await f.Sql("SELECT count(*) FROM audit_events WHERE entity_id=$1 AND action='case_chat.attempt_trashed'",f.Course));
    }

    [FormativeDatabaseFact] public async Task LegacyTrashedFormalSourceCanStillBeRestored()
    {
        await using var f=await Fixture.Create();var id=await Analyze(f);
        await f.Workflow.SubmitFormal(f.Course,f.Student,new(){RequestId=Guid.NewGuid(),MessageId=id},default);
        await f.Sql("UPDATE case_analysis_messages SET trashed_at=now() WHERE id=$1",id);
        var restored=await f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",id,false,default);
        Assert.Null(restored.Sessions.Single().Messages.Single(m=>m.Id==id).TrashedAt);
        Assert.Single((await f.Workflow.FormalSubmissions(f.Course,f.Teacher,default)).Submissions);
    }

    [FormativeDatabaseFact] public async Task FormalSubmitAndTrashCannotBothCommit()
    {
        await using var f=await Fixture.Create();var id=await Analyze(f);
        async Task<int> Attempt(Func<Task> action){try{await action();return 200;}catch(CaseChatException error){return error.StatusCode;}}
        var results=await Task.WhenAll(
            Attempt(async()=>{await f.Workflow.SubmitFormal(f.Course,f.Student,new(){RequestId=Guid.NewGuid(),MessageId=id},default);}),
            Attempt(async()=>{await f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",id,true,default);}));
        Assert.Equal(new[]{200,409},results.OrderBy(value=>value));
        var formal=await f.Sql("SELECT count(*) FROM case_formal_submissions WHERE analysis_message_id=$1",id);
        var trashed=await f.Sql("SELECT count(*) FROM case_analysis_messages WHERE id=$1 AND trashed_at IS NOT NULL",id);
        Assert.Equal(1L,Convert.ToInt64(formal)+Convert.ToInt64(trashed));
    }

    [FormativeDatabaseFact]
    public async Task OwnAttemptTrashAndRestoreKeepPairedEvidenceRevisionLinksAndStatistics()
    {
        foreach(var role in new[]{"student","teacher","ta"}){
        await using var f=await Fixture.Create();var actor=role=="teacher"?f.Teacher:role=="ta"?f.Ta:f.Student;
        var first=Guid.NewGuid();var second=Guid.NewGuid();
        await f.Workflow.Send(f.Course,actor,"premorbid",new(){RequestId=first,Content="合成原始作答"},default);
        var before=await f.Workflow.Send(f.Course,actor,"premorbid",new(){RequestId=second,Content="合成修訂作答",RevisesMessageId=first},default);
        var trashed=await f.Workflow.SetAttemptTrashed(f.Course,actor,"premorbid",first,true,default);
        var messages=trashed.Sessions.Single().Messages;
        Assert.All(messages.Where(m=>m.Id==first||m.ReplyTo==first),m=>Assert.NotNull(m.TrashedAt));
        Assert.All(messages.Where(m=>m.Id==second||m.ReplyTo==second),m=>Assert.Null(m.TrashedAt));
        Assert.Equal(before.Sessions.Single().Statistics,trashed.Sessions.Single().Statistics);
        Assert.Equal(first,messages.Single(m=>m.Id==second).RevisesMessageId);
        Assert.Equal(before.Sessions.Single().Messages.Select(m=>m.Content),messages.Select(m=>m.Content));
        if(role=="student"){
            Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.ReviewHistory(f.Course,f.Teacher,actor,"premorbid",default))).StatusCode);
            var own=await f.Workflow.History(f.Course,actor,"premorbid",default);
            Assert.Equal(2,own.Sessions.Single().Messages.Count(m=>m.TrashedAt.HasValue));
            Assert.Equal(2,(await f.Workflow.Records(f.Course,f.Teacher,default)).Single(s=>s.Id==actor).Statistics.RoundTrips);
        }
        await f.Workflow.SetAttemptTrashed(f.Course,actor,"premorbid",first,true,default);
        var restored=await f.Workflow.SetAttemptTrashed(f.Course,actor,"premorbid",first,false,default);
        await f.Workflow.SetAttemptTrashed(f.Course,actor,"premorbid",first,false,default);
        Assert.All(restored.Sessions.Single().Messages,m=>Assert.Null(m.TrashedAt));
        Assert.Equal(2L,await f.Sql("SELECT count(*) FROM audit_events WHERE entity_id=$1 AND action IN ('case_chat.attempt_trashed','case_chat.attempt_restored')",f.Course));
        if(role=="student"){
            var record=(await f.Workflow.Records(f.Course,f.Teacher,default)).Single(s=>s.Id==actor);
            Assert.Equal(2,record.Statistics.RoundTrips);
        }
        }
    }

    [FormativeDatabaseFact] public async Task TrashIsBoundToOwnerCourseStageRoleAndUserMessage()
    {
        await using var f=await Fixture.Create();var id=Guid.NewGuid();
        var history=await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=id,Content="合成權限作答"},default);
        var assistant=history.Sessions.Single().Messages.Single(m=>m.Role=="assistant").Id;
        foreach(var actor in new[]{f.Peer,f.Teacher,f.Ta})Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SetAttemptTrashed(f.Course,actor,"premorbid",id,true,default))).StatusCode);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SetAttemptTrashed(f.Other,f.Student,"premorbid",id,true,default))).StatusCode);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SetAttemptTrashed(f.Course,f.Student,"illnessCourse",id,true,default))).StatusCode);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",assistant,true,default))).StatusCode);
        await f.Sql("UPDATE course_members SET course_role='teacher' WHERE course_id=$1 AND user_id=$2",f.Course,f.Student);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",id,true,default))).StatusCode);
        Assert.Equal(0L,await f.Sql("SELECT count(*) FROM audit_events WHERE entity_id=$1 AND action='case_chat.attempt_trashed'",f.Course));
    }

    [FormativeDatabaseFact] public async Task TrashedSourcesCannotBeReplayedRetriedOrUsedForNewRevisions()
    {
        await using var f=await Fixture.Create();var id=Guid.NewGuid();var input=new CaseChatInput{RequestId=id,Content="合成完成作答"};
        await f.Workflow.Send(f.Course,f.Student,"premorbid",input,default);
        await f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",id,true,default);var calls=f.Provider.Requests.Count;
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"premorbid",input,default))).StatusCode);
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Retry(f.Course,f.Student,"premorbid",id,default))).StatusCode);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content="合成新修訂",RevisesMessageId=id},default))).StatusCode);
        Assert.Equal(calls,f.Provider.Requests.Count);
        await f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",id,false,default);
        await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content="合成還原後修訂",RevisesMessageId=id},default);
    }

    [FormativeDatabaseFact] public async Task LivePendingCannotBeTrashedButFailedAndClosedAttemptsCan()
    {
        await using var f=await Fixture.Create();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider.Wait=async ct=>{entered.TrySetResult();await release.Task.WaitAsync(ct);};var id=Guid.NewGuid();
        var sending=f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=id,Content="合成等待作答"},default);await entered.Task;
        try{Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",id,true,default))).StatusCode);}
        finally{release.TrySetResult();}
        await sending;f.Provider.Wait=null;f.Provider.Fail=true;var failed=Guid.NewGuid();
        await Assert.ThrowsAnyAsync<Exception>(()=>f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=failed,Content="合成失敗作答"},default));
        await f.Workflow.Restart(f.Course,f.Student,"premorbid",default);
        var trashed=await f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",failed,true,default);
        Assert.Equal("closed",trashed.Sessions.Single().Status);Assert.NotNull(trashed.Sessions.Single().Messages.Single(m=>m.Id==failed).TrashedAt);
        await f.Sql("UPDATE courses SET status='archived' WHERE id=$1",f.Course);
        Assert.Equal(409,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",failed,false,default))).StatusCode);
    }

    [FormativeDatabaseFact] public async Task StudentPreviewCannotTrashOrRestoreTeacherPractice()
    {
        await using var f=await Fixture.Create();var id=Guid.NewGuid();
        await f.Workflow.Send(f.Course,f.Teacher,"premorbid",new(){RequestId=id,Content="合成教師練習"},default);
        using var preview=new InspectionHarness(f,f.Teacher,"teacher",true);
        Assert.Equal(403,Assert.IsAssignableFrom<ObjectResult>(await preview.Controller.TrashAttempt(f.Course,"premorbid",id,default)).StatusCode);
        Assert.Equal(403,Assert.IsAssignableFrom<ObjectResult>(await preview.Controller.RestoreAttempt(f.Course,"premorbid",id,default)).StatusCode);
    }
    [FormativeDatabaseFact] public async Task ExpiredAttemptCanBeTrashedWithoutAcceptingItsLateAssistantReply()
    {
        await using var f=await Fixture.Create();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider.Wait=async ct=>{entered.TrySetResult();await release.Task.WaitAsync(ct);};var id=Guid.NewGuid();
        var sending=f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=id,Content="合成逾時作答"},default);await entered.Task;
        try{
            await f.Sql("UPDATE case_analysis_messages SET lease_until=now()-interval '1 minute' WHERE id=$1",id);
            var history=await f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",id,true,default);
            var message=Assert.Single(history.Sessions.Single().Messages);Assert.Equal("failed",message.Status);Assert.NotNull(message.TrashedAt);
        }finally{release.TrySetResult();}
        await Assert.ThrowsAsync<CaseChatException>(()=>sending);
        var final=await f.Workflow.History(f.Course,f.Student,"premorbid",default);Assert.Single(final.Sessions.Single().Messages);Assert.Equal(0,final.Sessions.Single().Statistics.RoundTrips);
    }
}
