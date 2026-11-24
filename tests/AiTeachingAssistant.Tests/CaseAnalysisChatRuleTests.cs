using System.Text.Json;
using System.Security.Claims;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using AiTeachingAssistant.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AiTeachingAssistant.Tests;

public partial class CaseAnalysisChatTests
{
    [FormativeDatabaseFact] public async Task CourseRulesRequireActualMembershipAndTeacherWritesWithOptimisticConcurrency()
    {
        await using var f=await Fixture.Create();var initial=await f.Workflow.Rules(f.Course,f.Teacher,default);
        Assert.Equal(0,initial.Version);Assert.Null(initial.UpdatedAt);Assert.Null(initial.UpdatedBy);
        Assert.Equal(0,(await f.Workflow.Rules(f.Course,f.Ta,default)).Version);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Rules(f.Course,f.Student,default))).StatusCode);
        var input=new CaseClassificationRulesInput{ExpectedVersion=0,Definitions=initial.Definitions};
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SaveRules(f.Course,f.Ta,input,default))).StatusCode);
        Assert.Equal(403,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SaveRules(f.Course,f.Student,input,default))).StatusCode);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SaveRules(f.Other,f.Teacher,input,default))).StatusCode);
        var writes=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>{
            try{return (await f.Workflow.SaveRules(f.Course,f.Teacher,input,default)).Version;}
            catch(CaseChatException e){return e.StatusCode;}
        }));
        Assert.Equal(new[]{1,409},writes.Order().ToArray());
        var saved=await f.Workflow.Rules(f.Course,f.Teacher,default);Assert.Equal(f.Teacher,saved.UpdatedBy);Assert.NotNull(saved.UpdatedAt);
        Assert.Equal(1L,await f.Sql("SELECT count(*) FROM audit_events WHERE entity_id=$1 AND action='course.case_classification_rules'",f.Course));
        var audit=(string)(await f.Sql("SELECT metadata::text FROM audit_events WHERE entity_id=$1 AND action='course.case_classification_rules'",f.Course))!;
        Assert.DoesNotContain("definitions",audit);Assert.DoesNotContain("人口學",audit);
        Assert.Equal(0L,await f.Sql("SELECT count(*) FROM case_classification_rule_versions WHERE course_id=$1",f.Other));
    }
    [FormativeDatabaseFact] public async Task NewFormalTurnsUseSavedVersionAndRetryKeepsItsImmutablePromptAndCatalog()
    {
        await using var f=await Fixture.Create();var definitions=CaseClassificationRules.Defaults.Concat([new CaseClassificationRuleDefinition("C7","合成新增功能","C","自訂合成描述",["合成詞"],true)]).ToArray();
        await f.Workflow.SaveRules(f.Course,f.Teacher,new(){ExpectedVersion=0,Definitions=definitions},default);
        var id=Guid.NewGuid();f.Provider.Fail=true;
        await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Send(f.Course,f.Student,"functionalAssessment",new(){RequestId=id,Content="合成功能"},default));
        var first=f.Provider.Requests.Single();Assert.Contains("C7",first[0].Content);Assert.Contains("合成詞",first[0].Content);
        Assert.Contains(first.Last().CaseRules!,d=>d.Code=="C7");
        definitions=definitions.Select(d=>d.Code=="C7"?d with{Enabled=false,Label="第二版名稱"}:d).ToArray();
        await f.Workflow.SaveRules(f.Course,f.Teacher,new(){ExpectedVersion=1,Definitions=definitions},default);
        f.Provider.Fail=false;f.Provider.Feedback="""{"feedback":"合成評語","annotations":[{"occurrence":0,"text":"合成功能","codes":["C7"],"explanation":"合成","status":"unverified"}]}""";
        var history=await f.Workflow.Retry(f.Course,f.Student,"functionalAssessment",id,default);
        Assert.Equal(first[0].Content,f.Provider.Requests.Last()[0].Content);
        var assistant=history.Sessions.Single().Messages.Last();Assert.Equal(new[]{"C7"},Assert.Single(assistant.Annotations!).Codes);
        Assert.Equal(1,assistant.RuleVersion);Assert.Equal("合成新增功能",assistant.RuleSnapshot!.Single(d=>d.Code=="C7").Label);
        Assert.Equal(1,await f.Sql("SELECT rule_version FROM case_analysis_messages WHERE id=$1",id));
        var second=await f.Workflow.Send(f.Course,f.Student,"functionalAssessment",new(){RequestId=Guid.NewGuid(),Content="合成功能"},default);
        Assert.Empty(second.Sessions.Single().Messages.Last().Annotations!);Assert.Equal(2,second.Sessions.Single().Messages.Last().RuleVersion);
        Assert.DoesNotContain("C7 ",f.Provider.Requests.Last()[0].Content);
        Assert.Contains(second.Sessions.Single().Messages.Last().RuleSnapshot!,d=>d.Code=="C7"&&!d.Enabled);
    }
    [FormativeDatabaseFact] public async Task LegacyNullRuleMetadataKeepsStaticCatalogAfterCourseChanges()
    {
        await using var f=await Fixture.Create();var id=Guid.NewGuid();var session=Guid.NewGuid();
        await f.Sql("INSERT INTO case_analysis_sessions(id,course_id,student_id,stage,is_practice,started_at,last_activity_at,next_sequence) VALUES($1,$2,$3,'premorbid',false,now(),now(),2)",session,f.Course,f.Student);
        await f.Sql("INSERT INTO case_analysis_messages(id,session_id,role,content,sequence_number,response_status,model_name,prompt_version,created_at) VALUES($1,$2,'user','合成舊文',1,'failed','mock-model',$3,now())",id,session,CaseChatPrompts.VersionFor(AnalysisStage.Premorbid));
        var definitions=CaseClassificationRules.Defaults.Select(d=>d.Code=="A1"?d with{Enabled=false}:d).ToArray();
        await f.Workflow.SaveRules(f.Course,f.Teacher,new(){ExpectedVersion=0,Definitions=definitions},default);
        f.Provider.Feedback="""{"feedback":"合成舊版回覆","annotations":[{"occurrence":0,"text":"合成舊文","codes":["A1a"],"explanation":"","status":"unverified"}]}""";
        var history=await f.Workflow.Retry(f.Course,f.Student,"premorbid",id,default);
        Assert.Equal(CaseChatPrompts.Get(AnalysisStage.Premorbid),f.Provider.Requests.Single().First().Content);
        Assert.Null(f.Provider.Requests.Single().Last().CaseRules);Assert.Equal("A1a",Assert.Single(Assert.Single(history.Sessions.Single().Messages.Last().Annotations!).Codes));
        Assert.Null(history.Sessions.Single().Messages.Last().RuleVersion);
    }
    [FormativeDatabaseFact] public async Task PreviewAndInactiveMembershipCannotReadOrWriteRulesAndSnapshotsCannotBeRewritten()
    {
        await using var f=await Fixture.Create();var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,f.Teacher.ToString()),new Claim("can_teach",bool.TrueString)],"test"))};
        context.Request.Headers[LearnerExperience.Header]="student";var experience=new LearnerExperience(new HttpContextAccessor{HttpContext=context});
        var controller=new CaseClassificationRulesController(f.Workflow,experience){ControllerContext=new(){HttpContext=context}};
        Assert.Equal(403,Assert.IsType<ObjectResult>(await controller.Read(f.Course,default)).StatusCode);
        Assert.Equal(403,Assert.IsType<ObjectResult>(await controller.Save(f.Course,new(){Definitions=CaseClassificationRules.Defaults},default)).StatusCode);
        await f.Workflow.SaveRules(f.Course,f.Teacher,new(){Definitions=CaseClassificationRules.Defaults},default);
        var history=await f.Workflow.Send(f.Course,f.Student,"premorbid",new(){RequestId=Guid.NewGuid(),Content="合成不變測試"},default);var id=history.Sessions.Single().Messages.First().Id;
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>f.Sql("UPDATE case_analysis_messages SET rule_version=3 WHERE id=$1",id));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>f.Sql("UPDATE case_classification_rule_versions SET version=3 WHERE course_id=$1",f.Course));
        await f.Sql("UPDATE course_members SET status='inactive' WHERE course_id=$1 AND user_id=$2",f.Course,f.Teacher);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.Rules(f.Course,f.Teacher,default))).StatusCode);
        Assert.Equal(404,(await Assert.ThrowsAsync<CaseChatException>(()=>f.Workflow.SaveRules(f.Course,f.Teacher,new(){Definitions=CaseClassificationRules.Defaults},default))).StatusCode);
    }
}
