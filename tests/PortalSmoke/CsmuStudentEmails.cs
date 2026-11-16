using System.Data;
using System.Text.Json;
using AiTeachingAssistant.Services;
using Npgsql;

internal static class CsmuStudentEmails
{
    private const string Scope="""
        p.platform_role='Member' AND NOT p.can_create_courses
        AND (u.student_number IS NOT NULL OR EXISTS(SELECT 1 FROM course_members m WHERE m.user_id=u.id AND m.course_role='student'))
        AND NOT EXISTS(SELECT 1 FROM course_members m WHERE m.user_id=u.id AND m.course_role IN ('teacher','ta'))
        AND NOT EXISTS(SELECT 1 FROM courses c WHERE c.owner_user_id=u.id)
        """;
    public static async Task Run(bool preview,bool audit)
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development")throw new InvalidOperationException("Student email maintenance requires Development.");
        var connection=Environment.GetEnvironmentVariable("ConnectionStrings__Portal");
        if(string.IsNullOrWhiteSpace(connection))throw new InvalidOperationException("Configure ConnectionStrings__Portal.");
        await using var db=new NpgsqlConnection(connection);await db.OpenAsync();await using var tx=await db.BeginTransactionAsync(IsolationLevel.Serializable);
        await using(var gate=new NpgsqlCommand("SELECT pg_advisory_xact_lock(741026111)",db,tx))await gate.ExecuteNonQueryAsync();
        var students=new List<(Guid Id,string Before,string After,string Normalized)>();
        await using(var cmd=new NpgsqlCommand("SELECT u.id,u.email,p.normalized_email FROM app_users u JOIN portal_credentials p ON p.user_id=u.id WHERE "+Scope+" ORDER BY u.id FOR UPDATE OF u,p",db,tx))
        await using(var reader=await cmd.ExecuteReaderAsync())while(await reader.ReadAsync())students.Add((reader.GetGuid(0),reader.GetString(1),StudentEmailPolicy.InstitutionalAddress(reader.GetString(1)),reader.GetString(2)));
        async Task<string> Protected(){await using var cmd=new NpgsqlCommand("SELECT md5(COALESCE(string_agg((to_jsonb(u)-'email')::text||(to_jsonb(p)-'normalized_email')::text,'' ORDER BY u.id),'')) FROM app_users u JOIN portal_credentials p ON p.user_id=u.id WHERE "+Scope,db,tx);return (string)(await cmd.ExecuteScalarAsync())!;}
        var before=await Protected();
        if(students.Select(row=>row.After).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=students.Count)throw new InvalidOperationException("Student local-parts collide; no account was changed.");
        foreach(var row in students)
        {
            await using var conflict=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM app_users WHERE id<>$1 AND lower(email)=lower($2)) OR EXISTS(SELECT 1 FROM portal_credentials WHERE user_id<>$1 AND normalized_email=upper($2))",db,tx);
            conflict.Parameters.AddWithValue(row.Id);conflict.Parameters.AddWithValue(row.After);
            if(await conflict.ExecuteScalarAsync() is true)throw new InvalidOperationException("Institutional address is already used; no account was changed.");
        }
        var pending=students.Where(row=>row.Before!=row.After||row.Normalized!=row.After.ToUpperInvariant()).ToArray();
        if(audit&&pending.Length!=0)throw new InvalidOperationException($"{pending.Length} student accounts still need the institutional domain.");
        if(!preview&&!audit)foreach(var row in pending)
        {
            foreach(var statement in new[]{"UPDATE app_users SET email=$2 WHERE id=$1","UPDATE portal_credentials SET normalized_email=upper($2) WHERE user_id=$1"})
            {
                await using var update=new NpgsqlCommand(statement,db,tx);
                update.Parameters.AddWithValue(row.Id);update.Parameters.AddWithValue(row.After);
                if(await update.ExecuteNonQueryAsync()!=1)throw new InvalidOperationException("Login account changed concurrently.");
            }
            await using var log=new NpgsqlCommand("INSERT INTO audit_events(entity_type,entity_id,action,metadata) VALUES('app_users',$1,'account.student_email.csmu',$2::jsonb)",db,tx);
            log.Parameters.AddWithValue(row.Id);log.Parameters.AddWithValue(JsonSerializer.Serialize(new{previousEmail=row.Before,email=row.After}));await log.ExecuteNonQueryAsync();
        }
        if(before!=await Protected())throw new InvalidOperationException("Protected account fields changed; rolling back.");
        if(preview||audit)await tx.RollbackAsync();else await tx.CommitAsync();
        Console.WriteLine($"CSMU student emails: {students.Count} verified student accounts; {pending.Length} {(preview?"would change":audit?"remaining":"updated")}. IDs, password hashes, roles and learning records retained.");
    }
}
