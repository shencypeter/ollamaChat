using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;

namespace AiTeachingAssistant.Tests;

public partial class CaseAnalysisChatTests
{
    [FormativeDatabaseFact]
    public async Task PendingLeaseCoversClassificationRetryAndOptionalFeedbackRepair()
    {
        await using var f=await Fixture.Create();var clock=new HistoryClock();
        var workflow=new CaseAnalysisChat(f.Db,f.Provider,Microsoft.Extensions.Options.Options.Create(new OllamaOptions{Model="mock-model",TimeoutSeconds=600}),Microsoft.Extensions.Options.Options.Create(new CaseChatOptions()),clock);
        foreach(var (stage,length,seconds) in new[]{("premorbid",20,1230),("illnessCourse",1000,1830)}){
            var id=Guid.NewGuid();
            var measured=0;f.Provider.Wait=async ct=>{measured=(int)(await f.Sql("SELECT extract(epoch from (lease_until-created_at))::int FROM case_analysis_messages WHERE id=$1",id))!;};
            await workflow.Send(f.Course,f.Student,stage,new(){RequestId=id,Content=new string('測',length)},default);
            Assert.Equal(seconds,measured);
        }
    }
}
