using System.Text.RegularExpressions;
using Npgsql;

internal static partial class DemoClassmateNames
{
    private static readonly string[] Names=["陳怡君","林志明","王美玲","張家豪","李雅婷","黃建宏"];

    private static NpgsqlDataSource Source()
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development")throw new InvalidOperationException("Demo classmate renaming requires Development.");
        var connection=Environment.GetEnvironmentVariable("ConnectionStrings__Portal");
        if(string.IsNullOrWhiteSpace(connection))throw new InvalidOperationException("ConnectionStrings__Portal is required.");
        return NpgsqlDataSource.Create(connection);
    }

    internal static async Task Rename()
    {
        await using var source=Source();await using var conn=await source.OpenConnectionAsync();await using var tx=await conn.BeginTransactionAsync();
        await using(var gate=new NpgsqlCommand("SELECT pg_advisory_xact_lock(741026114)",conn,tx))await gate.ExecuteNonQueryAsync();
        var rows=new List<(Guid Id,string OldName,string Email,string StudentNumber)>();
        await using(var cmd=new NpgsqlCommand("""
            SELECT u.id,u.display_name,u.email,u.student_number
            FROM app_users u JOIN portal_credentials p ON p.user_id=u.id
            WHERE u.status='active' AND u.display_name LIKE '教學展示學員 %'
              AND u.email LIKE 'demo.classmate%@portal-demo.example.invalid'
              AND p.platform_role='Member' AND NOT p.can_create_courses
            ORDER BY u.display_name,u.id FOR UPDATE OF u
            """,conn,tx))
        await using(var reader=await cmd.ExecuteReaderAsync())while(await reader.ReadAsync())rows.Add((reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetString(3)));
        if(rows.Count==0)
        {
            await using var existing=new NpgsqlCommand("SELECT count(*) FROM app_users WHERE email LIKE 'demo.classmate%@portal-demo.example.invalid' AND display_name=ANY($1)",conn,tx);
            existing.Parameters.AddWithValue(Names);
            if(Convert.ToInt64(await existing.ExecuteScalarAsync())!=Names.Length)throw new InvalidOperationException("Numbered demo classmates were not found and the common-name mapping is incomplete.");
            await tx.CommitAsync();Console.WriteLine("Existing demo classmate common names verified; no changes required.");return;
        }
        if(rows.Count!=Names.Length)throw new InvalidOperationException($"Expected {Names.Length} numbered demo classmates, found {rows.Count}.");
        var numbered=rows.Select(row=>(Row:row,Number:ParseNumber(row.OldName))).OrderBy(x=>x.Number).ToArray();
        if(numbered.Select(x=>x.Number).Distinct().Count()!=Names.Length)throw new InvalidOperationException("Demo classmate ordinals are not unique.");
        for(var index=0;index<numbered.Length;index++)
        {
            var row=numbered[index].Row;var name=Names[index];
            await Execute(conn,tx,"UPDATE app_users SET display_name=$2 WHERE id=$1",row.Id,name);
            await Execute(conn,tx,"UPDATE report_snapshot SET display_name=$2 WHERE student_id=$1",row.Id,name);
            await Execute(conn,tx,"INSERT INTO audit_events(entity_type,entity_id,action,metadata) VALUES('app_users',$1,'demo.student_account.renamed.20261112',jsonb_build_object('from',$2,'to',$3))",row.Id,row.OldName,name);
            Console.WriteLine($"{row.StudentNumber}\t{row.OldName} -> {name}\t{row.Email}");
        }
        await tx.CommitAsync();
        Console.WriteLine($"Renamed {numbered.Length} existing demo classmates without changing IDs or login identities.");
    }

    internal static async Task Audit()
    {
        await using var source=Source();await using var conn=await source.OpenConnectionAsync();
        var rows=new List<(string Name,string Email,string StudentNumber)>();
        await using(var cmd=new NpgsqlCommand("""
            SELECT u.display_name,u.email,u.student_number
            FROM app_users u JOIN portal_credentials p ON p.user_id=u.id
            WHERE u.status='active' AND u.email LIKE 'demo.classmate%@portal-demo.example.invalid'
              AND p.platform_role='Member' AND NOT p.can_create_courses
              AND EXISTS(SELECT 1 FROM audit_events e WHERE e.entity_id=u.id AND e.action='demo.student_account.renamed.20261112')
            ORDER BY u.student_number,u.id
            """,conn))
        await using(var reader=await cmd.ExecuteReaderAsync())while(await reader.ReadAsync())rows.Add((reader.GetString(0),reader.GetString(1),reader.GetString(2)));
        if(rows.Count!=Names.Length||Names.Except(rows.Select(x=>x.Name)).Any()||rows.Any(x=>x.Name.StartsWith("教學展示")))throw new InvalidOperationException("Demo classmate name audit failed.");
        await using(var stale=new NpgsqlCommand("SELECT count(*) FROM report_snapshot m JOIN app_users u ON u.id=m.student_id WHERE m.display_name<>u.display_name AND EXISTS(SELECT 1 FROM audit_events e WHERE e.entity_id=u.id AND e.action='demo.student_account.renamed.20261112')",conn))
        if(Convert.ToInt64(await stale.ExecuteScalarAsync())!=0)throw new InvalidOperationException("A report snapshot still has a stale demo classmate name.");
        foreach(var row in rows)Console.WriteLine($"OK {row.StudentNumber}\t{row.Name}\t{row.Email}");
        Console.WriteLine("Demo classmate name audit passed: 6 renamed accounts and report snapshots are synchronized.");
    }

    private static int ParseNumber(string name)
    {
        var match=TrailingNumber().Match(name);if(!match.Success)throw new InvalidOperationException($"Demo classmate name has no ordinal: {name}");return int.Parse(match.Groups[1].Value);
    }
    private static async Task Execute(NpgsqlConnection conn,NpgsqlTransaction tx,string sql,params object[] values){await using var cmd=new NpgsqlCommand(sql,conn,tx);foreach(var value in values)cmd.Parameters.AddWithValue(value);await cmd.ExecuteNonQueryAsync();}
    [GeneratedRegex("(\\d+)$",RegexOptions.CultureInvariant)] private static partial Regex TrailingNumber();
}
