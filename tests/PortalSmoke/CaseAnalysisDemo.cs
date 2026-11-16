using System.Text.RegularExpressions;
using AiTeachingAssistant.Options;
using Npgsql;

internal static class CaseAnalysisDemo
{
    public static async Task Run(bool preview,bool audit)
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development")throw new InvalidOperationException("Case chat demo requires Development.");
        var connection=Environment.GetEnvironmentVariable("ConnectionStrings__Portal")??throw new InvalidOperationException("Configure the development connection.");
        await using var db=new NpgsqlConnection(connection);await db.OpenAsync();await using var tx=await db.BeginTransactionAsync();
        async Task<object?> Sql(string sql,params object[] values){await using var cmd=new NpgsqlCommand(sql,db,tx);foreach(var value in values)cmd.Parameters.AddWithValue(value);return await cmd.ExecuteScalarAsync();}
        await Sql("SELECT pg_advisory_xact_lock(741026038)");
        var courses=new List<(Guid Id,string Term,bool Enabled)>();
        await using(var cmd=new NpgsqlCommand("SELECT id,term_code,case_analysis_enabled FROM courses WHERE title LIKE '%個案分析%' AND status='active' ORDER BY id",db,tx))
        await using(var r=await cmd.ExecuteReaderAsync())while(await r.ReadAsync())courses.Add((r.GetGuid(0),r.GetString(1),r.GetBoolean(2)));
        var created=0;var enabled=0;var normalized=0;
        if(courses.Count==0)
        {
            if(audit)throw new InvalidOperationException("No active case-analysis course.");
            if(!preview)
            {
                // A fallback demo reuses one actual teaching group's existing membership.
                Guid owner,source;
                await using(var cmd=new NpgsqlCommand("SELECT c.owner_user_id,c.id FROM courses c JOIN course_members m ON m.course_id=c.id AND m.user_id=c.owner_user_id AND m.course_role='teacher' AND m.status='active' WHERE c.status='active' ORDER BY (SELECT count(*) FROM course_members x WHERE x.course_id=c.id AND x.course_role='student' AND x.status='active') DESC,c.id LIMIT 1",db,tx))
                await using(var r=await cmd.ExecuteReaderAsync()){if(!await r.ReadAsync())throw new InvalidOperationException("No existing teaching group is available for a demo.");owner=r.GetGuid(0);source=r.GetGuid(1);}
                var id=Guid.Parse("d7342009-ff99-46c3-8a58-9e229328ada5");var term=new AcademicCalendarOptions().ResolveCurrentTerm(DateTimeOffset.UtcNow);
                await Sql("INSERT INTO courses(id,code,title,term_code,owner_user_id,status,case_analysis_enabled) VALUES($1,'CASE-AI','個案分析',$2,$3,'active',true)",id,term,owner);
                await Sql("INSERT INTO course_members(course_id,user_id,course_role,ta_restrictions) SELECT $1,user_id,course_role,ta_restrictions FROM course_members WHERE course_id=$2 AND status='active'",id,source);
                await Sql("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action) VALUES($1,'courses',$2,'demo.case_analysis_course.created')",owner,id);created=1;
            }
        }
        foreach(var course in courses)
        {
            // Legacy 115上/下/暑 unambiguously represents the same academic term.
            // Keep IDs, enrollment and learning data; record only the metadata normalization.
            var match=Regex.Match(course.Term,@"^([1-9][0-9]{0,2})(上|下|暑)$");
            var canonical=match.Success?match.Groups[1].Value+"-"+(match.Groups[2].Value switch{"上"=>"1","下"=>"2",_=>"S"}):course.Term;
            if(audit&&(!course.Enabled||canonical!=course.Term))throw new InvalidOperationException("Case-analysis demo is not ready.");
            if(!course.Enabled)enabled++;
            if(canonical!=course.Term)normalized++;
            if(preview||audit||course.Enabled&&canonical==course.Term)continue;
            if(canonical!=course.Term&&await Sql("SELECT EXISTS(SELECT 1 FROM courses other JOIN courses original ON original.id=$1 WHERE other.id<>$1 AND other.code=original.code AND other.term_code=$2)",course.Id,canonical) is true)throw new InvalidOperationException("Term normalization conflicts; no course was changed.");
            await Sql("UPDATE courses SET case_analysis_enabled=true,term_code=$2 WHERE id=$1",course.Id,canonical);
            await Sql("INSERT INTO audit_events(entity_type,entity_id,action,metadata) VALUES('courses',$1,'demo.case_analysis_course.enabled',jsonb_build_object('previousTerm',$2::text,'term',$3::text))",course.Id,course.Term,canonical);
        }
        if(preview||audit)await tx.RollbackAsync();else await tx.CommitAsync();
        Console.WriteLine($"Case-analysis demo: existing={courses.Count}; created={created}; enable={enabled}; normalize-equivalent-term={normalized}; mode={(preview?"preview":audit?"audit":"apply")}. No chat messages or student answers were seeded.");
    }
}
