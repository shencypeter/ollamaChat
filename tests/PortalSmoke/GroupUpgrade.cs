using AiTeachingAssistant.Services;
using Npgsql;

internal static class GroupUpgrade
{
    private static (NpgsqlConnectionStringBuilder Connection,string Schema) TestConnection()
    {
        var cs=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__Portal"));
        var schema=cs.SearchPath;
        if(schema==null || !System.Text.RegularExpressions.Regex.IsMatch(schema,"^group_test_[a-f0-9]{32}$"))
            throw new InvalidOperationException("A dedicated group_test schema is required.");
        return(cs,schema);
    }
    // Caller supplies a fresh, isolated search path and synthetic demo credentials.
    internal static async Task Run(string root)
    {
        var (cs,schema)=TestConnection();
        await using var db=NpgsqlDataSource.Create(cs.ConnectionString);
        await using var create=db.CreateCommand("CREATE SCHEMA "+schema);await create.ExecuteNonQueryAsync();
        var directory=Path.Combine(root,"Database");var temp=Directory.CreateTempSubdirectory();
        try{
            foreach(var path in Directory.GetFiles(directory,"*.sql").Where(p=>string.CompareOrdinal(Path.GetFileName(p),"029")<0))
                File.Copy(path,Path.Combine(temp.FullName,Path.GetFileName(path)));
            await DatabaseMigrator.RunAsync(db,temp.FullName,true);
        }finally{temp.Delete(true);}
        await CourseDemo.Seed(false);
        await using var grade=db.CreateCommand("""
            UPDATE report_submissions s SET score=84,published_score=80,feedback='draft',published_feedback='released',
             graded_by=a.created_by,graded_at=now()
            FROM report_assignments a WHERE a.id=s.report_id
            """);
        await grade.ExecuteNonQueryAsync();
        await using var legacy=db.CreateCommand("SELECT md5(string_agg(id::text||md5(content)||student_id::text||score::text||published_score::text||feedback||published_feedback,',' ORDER BY id)) FROM report_submissions");
        var before=(string?)await legacy.ExecuteScalarAsync()??throw new Exception("Missing 028 submission fixtures.");
        await DatabaseMigrator.RunAsync(db,directory,true);
        if(!Equals(before,await legacy.ExecuteScalarAsync()))throw new Exception("Legacy submissions changed.");
        await using var check=db.CreateCommand("SELECT bool_and(a.submission_mode='individual' AND s.revision=1 AND s.group_id IS NULL) FROM report_submissions s JOIN report_assignments a ON a.id=s.report_id");
        if(await check.ExecuteScalarAsync() is not true)throw new Exception("Invalid migrated defaults.");
        await using var audit=db.CreateCommand("SELECT count(*)=(SELECT count(*) FROM report_submissions) FROM report_grade_events WHERE details->>'migration'='29'");
        if(await audit.ExecuteScalarAsync() is not true)throw new Exception("Legacy grading history missing.");
        await CourseDemo.Seed();await CourseDemo.Seed();
        Console.WriteLine("Populated 028 upgrade and idempotent group seed passed.");
    }
    internal static async Task Drop()
    {
        var (cs,schema)=TestConnection();
        await using var db=NpgsqlDataSource.Create(cs.ConnectionString);
        await using var drop=db.CreateCommand("DROP SCHEMA "+schema+" CASCADE");
        await drop.ExecuteNonQueryAsync();
        Console.WriteLine("Isolated test schema removed.");
    }
}
