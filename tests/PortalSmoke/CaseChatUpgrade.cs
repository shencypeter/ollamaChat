using AiTeachingAssistant.Services;
using Npgsql;

internal static class CaseChatUpgrade
{
    public static async Task Run(string root)
    {
        var connection=Environment.GetEnvironmentVariable("ConnectionStrings__Portal")!;
        if(!System.Text.RegularExpressions.Regex.IsMatch(new NpgsqlConnectionStringBuilder(connection).SearchPath??"","^group_test_[0-9a-f]{32}$"))throw new InvalidOperationException("Use an isolated test schema.");
        await using var db=NpgsqlDataSource.Create(connection);var legacy=Directory.CreateTempSubdirectory("case-upgrade-");
        try
        {
            foreach(var file in Directory.GetFiles(Path.Combine(root,"Database"),"*.sql").Where(file=>string.CompareOrdinal(Path.GetFileName(file),"037")<0))File.Copy(file,Path.Combine(legacy.FullName,Path.GetFileName(file)));
            await DatabaseMigrator.RunAsync(db,legacy.FullName,true);
        }
        finally{legacy.Delete(true);}
        async Task<object?> Sql(string sql,params object[] values){await using var cmd=db.CreateCommand(sql);foreach(var value in values)cmd.Parameters.AddWithValue(value);return await cmd.ExecuteScalarAsync();}
        var teacher=Guid.NewGuid();var student=Guid.NewGuid();var course=Guid.NewGuid();var item=Guid.NewGuid();
        await Sql("INSERT INTO app_users(id,display_name) VALUES($1,'合成升級教師'),($2,'合成升級學生')",teacher,student);
        await Sql("INSERT INTO courses(id,code,title,term_code,owner_user_id,status) VALUES($1,'CHAT-UPGRADE','合成個案分析升級','115上',$2,'active')",course,teacher);
        await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,'teacher'),($1,$3,'student')",course,teacher,student);
        await Sql("INSERT INTO course_grade_items(id,course_id,title,category,max_score,weight,created_by) VALUES($1,$2,'既有評量','other',100,1,$3)",item,course,teacher);
        await Sql("INSERT INTO course_grade_scores(item_id,course_id,student_id,score,published_score,updated_by) VALUES($1,$2,$3,83,83,$4)",item,course,student,teacher);
        const string fingerprint="""
            SELECT md5((SELECT (to_jsonb(c)-'case_analysis_enabled')::text FROM courses c WHERE id=$1)
              ||(SELECT string_agg(to_jsonb(m)::text,'' ORDER BY user_id) FROM course_members m WHERE course_id=$1)
              ||(SELECT to_jsonb(i)::text FROM course_grade_items i WHERE id=$2)
              ||(SELECT to_jsonb(s)::text FROM course_grade_scores s WHERE item_id=$2))
            """;
        var before=(string)(await Sql(fingerprint,course,item))!;
        var previous=Directory.CreateTempSubdirectory("case-037-");
        try
        {
            foreach(var file in Directory.GetFiles(Path.Combine(root,"Database"),"*.sql").Where(file=>string.CompareOrdinal(Path.GetFileName(file),"038")<0))File.Copy(file,Path.Combine(previous.FullName,Path.GetFileName(file)));
            await DatabaseMigrator.RunAsync(db,previous.FullName,true);
        }
        finally{previous.Delete(true);}
        var session=Guid.NewGuid();var question=Guid.NewGuid();var answer=Guid.NewGuid();
        await Sql("INSERT INTO case_analysis_sessions(id,course_id,student_id,stage,next_sequence) VALUES($1,$2,$3,'premorbid',3)",session,course,student);
        await Sql("INSERT INTO case_analysis_messages(id,session_id,role,content,sequence_number,response_status,model_name,prompt_version) VALUES($1,$2,'user','合成舊版原始作答',1,'completed','upgrade-model','legacy-upgrade')",question,session);
        await Sql("INSERT INTO case_analysis_messages(id,session_id,role,content,sequence_number,response_status,in_reply_to,model_name,prompt_version) VALUES($1,$2,'assistant','合成舊版回饋',2,'completed',$3,'upgrade-model','legacy-upgrade')",answer,session,question);
        const string chatFingerprint="SELECT md5(string_agg((to_jsonb(m)-'performance_metrics'-'revises_message_id'-'annotations'-'trashed_at'-'rule_version'-'rule_snapshot'-'prompt_snapshot')::text,'' ORDER BY sequence_number)) FROM case_analysis_messages m WHERE session_id=$1";
        var beforeChat=await Sql(chatFingerprint,session);
        var revision=Directory.CreateTempSubdirectory("case-038-");
        try
        {
            foreach(var file in Directory.GetFiles(Path.Combine(root,"Database"),"*.sql").Where(file=>string.CompareOrdinal(Path.GetFileName(file),"039")<0))File.Copy(file,Path.Combine(revision.FullName,Path.GetFileName(file)));
            await DatabaseMigrator.RunAsync(db,revision.FullName,true);
        }
        finally{revision.Delete(true);}
        var before038=await Sql("SELECT md5(string_agg(to_jsonb(m)::text,'' ORDER BY sequence_number)) FROM case_analysis_messages m WHERE session_id=$1",session);
        var annotated=Directory.CreateTempSubdirectory("case-039-");
        try
        {
            foreach(var file in Directory.GetFiles(Path.Combine(root,"Database"),"*.sql").Where(file=>string.CompareOrdinal(Path.GetFileName(file),"040")<0))File.Copy(file,Path.Combine(annotated.FullName,Path.GetFileName(file)));
            await DatabaseMigrator.RunAsync(db,annotated.FullName,true);
        }
        finally{annotated.Delete(true);}
        if(!Equals(before038,await Sql("SELECT md5(string_agg((to_jsonb(m)-'annotations')::text,'' ORDER BY sequence_number)) FROM case_analysis_messages m WHERE session_id=$1",session)))throw new Exception("039 changed existing 038 message metadata.");
        if(await Sql("SELECT bool_and(annotations IS NULL) FROM case_analysis_messages WHERE session_id=$1",session) is not true)throw new Exception("Legacy annotations were fabricated.");
        var before039=await Sql("SELECT md5(string_agg(to_jsonb(m)::text,'' ORDER BY sequence_number)) FROM case_analysis_messages m WHERE session_id=$1",session);
        var trash=Directory.CreateTempSubdirectory("case-040-");
        try
        {
            foreach(var file in Directory.GetFiles(Path.Combine(root,"Database"),"*.sql").Where(file=>string.CompareOrdinal(Path.GetFileName(file),"041")<0))File.Copy(file,Path.Combine(trash.FullName,Path.GetFileName(file)));
            await DatabaseMigrator.RunAsync(db,trash.FullName,true);
        }
        finally{trash.Delete(true);}
        if(!Equals(before039,await Sql("SELECT md5(string_agg((to_jsonb(m)-'trashed_at')::text,'' ORDER BY sequence_number)) FROM case_analysis_messages m WHERE session_id=$1",session)))throw new Exception("040 changed existing 039 message metadata.");
        if(await Sql("SELECT bool_and(trashed_at IS NULL) FROM case_analysis_messages WHERE session_id=$1",session) is not true)throw new Exception("Legacy attempts were incorrectly trashed.");
        var before040=await Sql("SELECT md5(string_agg(to_jsonb(m)::text,'' ORDER BY sequence_number)) FROM case_analysis_messages m WHERE session_id=$1",session);
        var rules=Directory.CreateTempSubdirectory("case-042-");
        try
        {
            foreach(var file in Directory.GetFiles(Path.Combine(root,"Database"),"*.sql").Where(file=>string.CompareOrdinal(Path.GetFileName(file),"043")<0))File.Copy(file,Path.Combine(rules.FullName,Path.GetFileName(file)));
            await DatabaseMigrator.RunAsync(db,rules.FullName,true);
        }
        finally{rules.Delete(true);}
        if(!Equals(before040,await Sql("SELECT md5(string_agg((to_jsonb(m)-'rule_version'-'rule_snapshot'-'prompt_snapshot')::text,'' ORDER BY sequence_number)) FROM case_analysis_messages m WHERE session_id=$1",session)))throw new Exception("041/042 changed existing message content or metadata.");
        if(await Sql("SELECT bool_and(rule_version IS NULL AND rule_snapshot IS NULL AND prompt_snapshot IS NULL) FROM case_analysis_messages WHERE session_id=$1",session) is not true)throw new Exception("Legacy rules or prompts were fabricated.");
        if(!Equals(0L,await Sql("SELECT count(*) FROM case_formal_submissions"))||!Equals(0L,await Sql("SELECT count(*) FROM case_classification_rule_versions")))throw new Exception("Upgrade unexpectedly submitted legacy drafts or fabricated course rules.");
        if(!Equals(before,await Sql(fingerprint,course,item))||!Equals(beforeChat,await Sql(chatFingerprint,session)))throw new Exception("Upgrade changed legacy course, enrollment, grade or chat content.");
        if(await Sql("SELECT bool_and(performance_metrics IS NULL AND revises_message_id IS NULL) FROM case_analysis_messages WHERE session_id=$1",session) is not true)throw new Exception("Legacy telemetry or revision data was fabricated.");
        if(await Sql("SELECT case_analysis_enabled FROM courses WHERE id=$1",course) is not false)throw new Exception("Legacy course should remain opt-in.");
        await CheckAnnotationCapacityUpgrade(db,root,session);
        if(!Equals(before,await Sql(fingerprint,course,item)))throw new Exception("043 changed legacy course, enrollment or grade data.");
        var beforeRerun=await Sql("SELECT md5(string_agg(to_jsonb(m)::text,'' ORDER BY sequence_number)) FROM case_analysis_messages m WHERE session_id=$1",session);
        await DatabaseMigrator.RunAsync(db,Path.Combine(root,"Database"),true);
        if(!Equals(beforeRerun,await Sql("SELECT md5(string_agg(to_jsonb(m)::text,'' ORDER BY sequence_number)) FROM case_analysis_messages m WHERE session_id=$1",session)))throw new Exception("Final migration rerun changed existing expanded-capacity history.");
        Console.WriteLine("036 → 037 → 038 → 039 → 040 → 041/042 → 043 upgrade and rerun passed; course/enrollment/grade/chat fingerprints retained; legacy optional metadata remain NULL; 128 exact annotations persist and historical metadata stay immutable.");
        await CaseAnalysisDemo.Run(true,false);await CaseAnalysisDemo.Run(false,false);await CaseAnalysisDemo.Run(false,true);await CaseAnalysisDemo.Run(false,false);
        if(await Sql("SELECT case_analysis_enabled AND term_code='115-1' FROM courses WHERE id=$1",course) is not true||!Equals(await Sql("SELECT published_score FROM course_grade_scores WHERE item_id=$1",item),83m)||!Equals(await Sql("SELECT count(*) FROM course_members WHERE course_id=$1",course),2L))throw new Exception("Demo activation changed enrollment/grades or did not normalize equivalent term.");
        await Sql("DELETE FROM audit_events WHERE entity_id=$1",course);
        await Sql("DELETE FROM case_analysis_messages WHERE session_id=$1",session);await Sql("DELETE FROM case_analysis_sessions WHERE id=$1",session);
        await Sql("DELETE FROM course_grade_scores WHERE item_id=$1",item);await Sql("DELETE FROM course_grade_items WHERE id=$1",item);await Sql("DELETE FROM course_members WHERE course_id=$1",course);await Sql("DELETE FROM courses WHERE id=$1",course);await Sql("DELETE FROM app_users WHERE id IN ($1,$2)",teacher,student);
    }

    private static async Task CheckAnnotationCapacityUpgrade(NpgsqlDataSource db,string root,Guid session)
    {
        async Task<object?> Sql(string sql,params object[] values){await using var cmd=db.CreateCommand(sql);foreach(var value in values)cmd.Parameters.AddWithValue(value);return await cmd.ExecuteScalarAsync();}
        var clauses=Enumerable.Range(1,128).Select(i=>$"合成升級片段{i:D3}可評估").ToArray();
        var original=string.Join("，",clauses);var position=0;
        var entries=clauses.Select((text,index)=>{
            var start=position;position+=text.Length+1;
            return new{annotationId=$"ann-{index+1}",start,end=start+text.Length,text,codes=new[]{"A1a"},explanation="合成容量資料",status="unverified"};
        }).ToArray();
        var oldQuestion=Guid.NewGuid();var oldReply=Guid.NewGuid();var expandedQuestion=Guid.NewGuid();var expandedReply=Guid.NewGuid();
        const string ruleSnapshot="""[{"code":"A1a","label":"合成容量分類","phase":"A","description":"合成測試","keywords":[],"enabled":true}]""";
        foreach(var (id,sequence) in new[]{(oldQuestion,3),(expandedQuestion,5)})
            await Sql("INSERT INTO case_analysis_messages(id,session_id,role,content,sequence_number,response_status,model_name,prompt_version,rule_version,rule_snapshot,prompt_snapshot) VALUES($1,$2,'user',$3,$4,'completed','upgrade-model','capacity-upgrade',0,$5::jsonb,'合成不變提示')",id,session,original,sequence,ruleSnapshot);
        const string insertReply="INSERT INTO case_analysis_messages(id,session_id,role,content,sequence_number,response_status,in_reply_to,model_name,prompt_version,annotations,rule_version,rule_snapshot,prompt_snapshot) VALUES($1,$2,'assistant','合成容量升級回饋',$3,'completed',$4,'upgrade-model','capacity-upgrade',$5::jsonb,0,$6::jsonb,'合成不變提示')";
        await Sql(insertReply,oldReply,session,4,oldQuestion,System.Text.Json.JsonSerializer.Serialize(entries.Take(48)),ruleSnapshot);
        try
        {
            await Sql(insertReply,expandedReply,session,6,expandedQuestion,System.Text.Json.JsonSerializer.Serialize(entries.Take(49)),ruleSnapshot);
            throw new Exception("042 unexpectedly accepted more than 48 annotations; upgrade precondition was not exercised.");
        }
        catch(PostgresException error)when(error.SqlState==PostgresErrorCodes.CheckViolation&&error.ConstraintName=="case_chat_annotations_role"){}
        const string fingerprint="SELECT md5(string_agg(to_jsonb(m)::text,'' ORDER BY sequence_number)) FROM case_analysis_messages m WHERE session_id=$1";
        var before=await Sql(fingerprint,session);
        const string functionDefinition="SELECT pg_get_functiondef(oid) FROM pg_proc WHERE pronamespace=current_schema()::regnamespace AND proname='protect_case_chat_content'";
        var beforeTrigger=await Sql(functionDefinition);
        await DatabaseMigrator.RunAsync(db,Path.Combine(root,"Database"),true);
        if(!Equals(before,await Sql(fingerprint,session)))throw new Exception("043 changed existing 042 chat content or metadata.");
        if(!Equals(beforeTrigger,await Sql(functionDefinition)))throw new Exception("043 changed the historical content protection function.");
        await DatabaseMigrator.RunAsync(db,Path.Combine(root,"Database"),true);
        if(!Equals(before,await Sql(fingerprint,session)))throw new Exception("043 rerun changed existing 042 chat content or metadata.");
        await Sql(insertReply,expandedReply,session,6,expandedQuestion,System.Text.Json.JsonSerializer.Serialize(entries),ruleSnapshot);
        if(!Equals(128,await Sql("SELECT jsonb_array_length(annotations) FROM case_analysis_messages WHERE id=$1",expandedReply))
            ||!Equals(original.Length,await Sql("SELECT (annotations->127->>'end')::int FROM case_analysis_messages WHERE id=$1",expandedReply)))throw new Exception("043 did not retain 128 annotations through the final original clause.");
        foreach(var change in new[]{"annotations='[]'::jsonb","content='合成禁止重寫'","rule_snapshot='[]'::jsonb","prompt_snapshot='合成禁止改提示'"})
        {
            try{await Sql("UPDATE case_analysis_messages SET "+change+" WHERE id=$1",oldReply);throw new Exception("043 allowed historical content or metadata to be rewritten.");}
            catch(PostgresException error)when(error.SqlState==PostgresErrorCodes.RaiseException){}
        }
    }
}
