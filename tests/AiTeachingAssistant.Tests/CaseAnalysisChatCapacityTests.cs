using System.Text.Json;
using Npgsql;

namespace AiTeachingAssistant.Tests;

public partial class CaseAnalysisChatTests
{
    [FormativeDatabaseFact]
    public async Task ExpandedAnnotationCapacityPersistsUniqueEvidenceThroughTheLastOriginalClause()
    {
        await using var f=await Fixture.Create();
        foreach(var count in new[]{49,128})
        {
            var facts=Enumerable.Range(1,count).Select(i=>$"合成容量片段{i:D3}可評估").ToArray();
            var original=string.Join("，",facts);
            f.Provider.Feedback=JsonSerializer.Serialize(new{
                feedback="合成容量回饋，請核對原始資料。",
                annotations=facts.Select(text=>new{text,occurrence=0,codes=new[]{"C2"},explanation="合成分類參考",status="unverified"})
            });
            var request=Guid.NewGuid();
            await f.Workflow.Send(f.Course,f.Student,"functionalAssessment",new(){RequestId=request,Content=original},default);
            var persisted=await f.Workflow.History(f.Course,f.Student,"functionalAssessment",default);
            var reply=persisted.Sessions.Single().Messages.Single(m=>m.ReplyTo==request);
            Assert.Equal(count,reply.Annotations!.Count);
            Assert.Equal(count,reply.Annotations.Select(a=>(a.Start,a.End)).Distinct().Count());
            Assert.Equal(facts,reply.Annotations.Select(a=>a.Text));
            Assert.Equal(original.Length,reply.Annotations[^1].End);
            Assert.All(reply.Annotations,a=>Assert.Equal(a.Text,original[a.Start..a.End]));
            Assert.Equal(count,await f.Sql("SELECT jsonb_array_length(annotations) FROM case_analysis_messages WHERE id=$1",reply.Id));
            var immutable=await Assert.ThrowsAsync<PostgresException>(()=>f.Sql("UPDATE case_analysis_messages SET annotations='[]'::jsonb WHERE id=$1",reply.Id));
            Assert.Equal(PostgresErrorCodes.RaiseException,immutable.SqlState);
        }
    }

    [FormativeDatabaseFact]
    public async Task ExpandedAnnotationDatabaseConstraintRetainsRoleShapeNullAndCountRestrictions()
    {
        await using var f=await Fixture.Create();var session=Guid.NewGuid();var question=Guid.NewGuid();
        await f.Sql("INSERT INTO case_analysis_sessions(id,course_id,student_id,stage,next_sequence) VALUES($1,$2,$3,'premorbid',3)",session,f.Course,f.Student);
        await f.Sql("INSERT INTO case_analysis_messages(id,session_id,role,content,sequence_number,response_status,model_name,prompt_version) VALUES($1,$2,'user','合成容量原文',1,'completed','mock-model','capacity-test')",question,session);
        async Task<object?> Assistant(string json)=>await f.Sql("INSERT INTO case_analysis_messages(id,session_id,role,content,sequence_number,response_status,in_reply_to,model_name,prompt_version,annotations) VALUES($1,$2,'assistant','合成容量回饋',2,'completed',$3,'mock-model','capacity-test',$4::jsonb)",Guid.NewGuid(),session,question,json);
        var excess=JsonSerializer.Serialize(Enumerable.Range(0,129).Select(i=>new{synthetic=i}));
        var countError=await Assert.ThrowsAsync<PostgresException>(()=>Assistant(excess));
        Assert.Equal(PostgresErrorCodes.CheckViolation,countError.SqlState);
        Assert.Equal("case_chat_annotations_role",countError.ConstraintName);
        await Assert.ThrowsAsync<PostgresException>(()=>Assistant("{}"));
        var roleError=await Assert.ThrowsAsync<PostgresException>(()=>f.Sql("INSERT INTO case_analysis_messages(id,session_id,role,content,sequence_number,response_status,model_name,prompt_version,annotations) VALUES($1,$2,'user','合成不得標註使用者列',2,'completed','mock-model','capacity-test','[]'::jsonb)",Guid.NewGuid(),session));
        Assert.Equal(PostgresErrorCodes.CheckViolation,roleError.SqlState);
        Assert.Equal("case_chat_annotations_role",roleError.ConstraintName);
        await f.Sql("INSERT INTO case_analysis_messages(id,session_id,role,content,sequence_number,response_status,in_reply_to,model_name,prompt_version) VALUES($1,$2,'assistant','合成舊版無標註回饋',2,'completed',$3,'mock-model','capacity-test')",Guid.NewGuid(),session,question);
        Assert.Equal(true,await f.Sql("SELECT bool_and(annotations IS NULL) FROM case_analysis_messages WHERE session_id=$1",session));
    }
}
