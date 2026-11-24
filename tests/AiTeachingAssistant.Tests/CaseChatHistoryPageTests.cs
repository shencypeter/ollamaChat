using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Options;

namespace AiTeachingAssistant.Tests;

public partial class CaseAnalysisChatTests
{
    private static CaseHistoryQuery Page(int? before=null,string? model=null,bool trashed=false)=>new(){Paged=true,Before=before,Model=model,Trashed=trashed};
    private static CaseChatMessage[] Questions(CaseStageHistory page)=>page.Sessions.SelectMany(s=>s.Messages).Where(m=>m.Role=="user").OrderByDescending(m=>m.HistoryNumber).ToArray();
    private sealed class HistoryClock:TimeProvider{private readonly DateTimeOffset now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>now;}

    [FormativeDatabaseFact]
    public async Task EqualTimestampsUseSequenceOrderingWithoutDuplicateOrMissingPages()
    {
        await using var f=await Fixture.Create();var workflow=new CaseAnalysisChat(f.Db,f.Provider,Microsoft.Extensions.Options.Options.Create(new OllamaOptions{Model="mock-model"}),Microsoft.Extensions.Options.Options.Create(new CaseChatOptions()),new HistoryClock());
        for(var i=0;i<6;i++)await workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content=$"合成相同時間{i}"},default);
        var first=await workflow.History(f.Course,f.Student,"premorbid",default,Page());var last=await workflow.History(f.Course,f.Student,"premorbid",default,Page(first.Pagination!.NextCursor));
        Assert.Equal(new int?[]{6,5,4,3,2},Questions(first).Select(m=>m.HistoryNumber));Assert.Equal(1,Questions(last).Single().HistoryNumber);Assert.Null(last.Pagination!.NextCursor);
    }

    [FormativeDatabaseFact]
    public async Task HistoryLoadsFiveOwnedPairsPerClickWithStableNumbersAfterNewAttempts()
    {
        await using var f=await Fixture.Create();
        for(var i=1;i<=12;i++)await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content=$"合成分頁作答{i}"},default);
        var first=await f.Workflow.History(f.Course,f.Student,"premorbid",default,Page());
        Assert.Equal(new int?[]{12,11,10,9,8},Questions(first).Select(m=>m.HistoryNumber));Assert.Equal(12,first.Pagination!.TotalCount);Assert.Equal(8,first.Pagination.NextCursor);
        Assert.Equal(10,first.Sessions.Sum(s=>s.Messages.Count));Assert.All(Questions(first),q=>Assert.Single(first.Sessions.SelectMany(s=>s.Messages).Where(m=>m.ReplyTo==q.Id)));
        await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content="合成後來新增的作答"},default);
        var second=await f.Workflow.History(f.Course,f.Student,"premorbid",default,Page(first.Pagination.NextCursor));
        Assert.Equal(new int?[]{7,6,5,4,3},Questions(second).Select(m=>m.HistoryNumber));Assert.Empty(Questions(first).Select(m=>m.Id).Intersect(Questions(second).Select(m=>m.Id)));
        var last=await f.Workflow.History(f.Course,f.Student,"premorbid",default,Page(second.Pagination!.NextCursor));
        Assert.Equal(new int?[]{2,1},Questions(last).Select(m=>m.HistoryNumber));Assert.Null(last.Pagination!.NextCursor);
        var target=Questions(first).Last().Id;var focused=await f.Workflow.History(f.Course,f.Student,"premorbid",default,new(){Paged=true,Target=target});Assert.Equal(target,Questions(focused)[0].Id);Assert.True(focused.Pagination!.Focused);Assert.True(Questions(focused).Length<=5);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.History(f.Course,f.Peer,"premorbid",default,new(){Paged=true,Target=target}))).StatusCode);
        var legacy=await f.Workflow.History(f.Course,f.Student,"premorbid",default);Assert.Null(legacy.Pagination);Assert.Equal(13,Questions(legacy).Length);
    }

    [FormativeDatabaseFact]
    public async Task ModelTrashAndCourseFiltersKeepGlobalNumbersAndExposeOnlyOwnerMetadata()
    {
        await using var f=await Fixture.Create();var ids=new List<Guid>();
        foreach(var model in new[]{"retired-model:7b","mock-model"," ","retired-model:7b","mock-model","retired-model:7b"}){
            var workflow=new CaseAnalysisChat(f.Db,f.Provider,Microsoft.Extensions.Options.Options.Create(new OllamaOptions{Model=model}),Microsoft.Extensions.Options.Options.Create(new CaseChatOptions()),TimeProvider.System);
            var id=Guid.NewGuid();ids.Add(id);await workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=id,Content="合成模型分頁"},default);
        }
        await f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",ids[3],true,default);
        var normal=await f.Workflow.History(f.Course,f.Student,"premorbid",default,Page(model:"model:retired-model:7b"));
        Assert.Equal(new int?[]{6,1},Questions(normal).Select(m=>m.HistoryNumber));Assert.Equal(2,normal.Pagination!.TotalCount);Assert.Equal(1,normal.Pagination.TrashCount);Assert.Equal(3,normal.Pagination.Models.Count);
        var trash=await f.Workflow.History(f.Course,f.Student,"premorbid",default,Page(model:"model:retired-model:7b",trashed:true));Assert.Equal(ids[3],Questions(trash).Single().Id);Assert.Equal(4,Questions(trash).Single().HistoryNumber);
        var unknown=await f.Workflow.History(f.Course,f.Student,"premorbid",default,Page(model:"unknown"));Assert.Equal(ids[2],Questions(unknown).Single().Id);
        Assert.Empty(Questions(await f.Workflow.History(f.Course,f.Peer,"premorbid",default,Page())));Assert.Empty(Questions(await f.Workflow.History(f.Course,f.Teacher,"premorbid",default,Page())));
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.History(f.Other,f.Student,"premorbid",default,Page()))).StatusCode);
        Assert.Equal(400,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.History(f.Course,f.Student,"premorbid",default,Page(-1)))).StatusCode);
    }

    [FormativeDatabaseFact]
    public async Task PagedMutationsNeverReturnTheWholeHistoryAndKeepRetryTailMetadata()
    {
        await using var f=await Fixture.Create();CaseStageHistory result=null!;
        var firstId=Guid.NewGuid();for(var i=0;i<7;i++)result=await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=i==0?firstId:Guid.NewGuid(),Content="合成有界回傳"},default,Page());
        Assert.Equal(5,Questions(result).Length);Assert.Equal(7,Questions(result)[0].HistoryNumber);Assert.NotNull(result.Pagination!.ActiveLastMessageId);
        var id=Questions(result)[0].Id;var trashed=await f.Workflow.SetAttemptTrashed(f.Course,f.Student,"premorbid",id,true,default,Page());
        Assert.Equal(5,Questions(trashed).Length);Assert.DoesNotContain(Questions(trashed),q=>q.Id==id);Assert.Equal(1,trashed.Pagination!.TrashCount);
        var replay=await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=firstId,Content="合成有界回傳"},default,Page());Assert.Equal(firstId,Questions(replay)[0].Id);Assert.True(replay.Pagination!.Focused);
    }
}
