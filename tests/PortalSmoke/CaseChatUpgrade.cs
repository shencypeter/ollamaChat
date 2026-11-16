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
        const string chatFingerprint="SELECT md5(string_agg((to_jsonb(m)-'performance_metrics'-'revises_message_id'-'annotations')::text,'' ORDER BY sequence_number)) FROM case_analysis_messages m WHERE session_id=$1";
        var beforeChat=await Sql(chatFingerprint,session);
        var revision=Directory.CreateTempSubdirectory("case-038-");
        try
        {
            foreach(var file in Directory.GetFiles(Path.Combine(root,"Database"),"*.sql").Where(file=>string.CompareOrdinal(Path.GetFileName(file),"039")<0))File.Copy(file,Path.Combine(revision.FullName,Path.GetFileName(file)));
            await DatabaseMigrator.RunAsync(db,revision.FullName,true);
        }
        finally{revision.Delete(true);}
        var before038=await Sql("SELECT md5(string_agg(to_jsonb(m)::text,'' ORDER BY sequence_number)) FROM case_analysis_messages m WHERE session_id=$1",session);
        await DatabaseMigrator.RunAsync(db,Path.Combine(root,"Database"),true);
        if(!Equals(before038,await Sql("SELECT md5(string_agg((to_jsonb(m)-'annotations')::text,'' ORDER BY sequence_number)) FROM case_analysis_messages m WHERE session_id=$1",session)))throw new Exception("039 changed existing 038 message metadata.");
        if(await Sql("SELECT bool_and(annotations IS NULL) FROM case_analysis_messages WHERE session_id=$1",session) is not true)throw new Exception("Legacy annotations were fabricated.");
        if(!Equals(before,await Sql(fingerprint,course,item))||!Equals(beforeChat,await Sql(chatFingerprint,session)))throw new Exception("Upgrade changed legacy course, enrollment, grade or chat content.");
        if(await Sql("SELECT bool_and(performance_metrics IS NULL AND revises_message_id IS NULL) FROM case_analysis_messages WHERE session_id=$1",session) is not true)throw new Exception("Legacy telemetry or revision data was fabricated.");
        if(await Sql("SELECT case_analysis_enabled FROM courses WHERE id=$1",course) is not false)throw new Exception("Legacy course should remain opt-in.");
        await DatabaseMigrator.RunAsync(db,Path.Combine(root,"Database"),true);
        Console.WriteLine("036 → 037 → 038 → 039 upgrade and rerun passed; course/enrollment/grade/chat fingerprints retained; old optional metadata remain NULL.");
        await CaseAnalysisDemo.Run(true,false);await CaseAnalysisDemo.Run(false,false);await CaseAnalysisDemo.Run(false,true);await CaseAnalysisDemo.Run(false,false);
        if(await Sql("SELECT case_analysis_enabled AND term_code='115-1' FROM courses WHERE id=$1",course) is not true||!Equals(await Sql("SELECT published_score FROM course_grade_scores WHERE item_id=$1",item),83m)||!Equals(await Sql("SELECT count(*) FROM course_members WHERE course_id=$1",course),2L))throw new Exception("Demo activation changed enrollment/grades or did not normalize equivalent term.");
        await Sql("DELETE FROM audit_events WHERE entity_id=$1",course);
        await Sql("DELETE FROM case_analysis_messages WHERE session_id=$1",session);await Sql("DELETE FROM case_analysis_sessions WHERE id=$1",session);
        await Sql("DELETE FROM course_grade_scores WHERE item_id=$1",item);await Sql("DELETE FROM course_grade_items WHERE id=$1",item);await Sql("DELETE FROM course_members WHERE course_id=$1",course);await Sql("DELETE FROM courses WHERE id=$1",course);await Sql("DELETE FROM app_users WHERE id IN ($1,$2)",teacher,student);
    }
}
