using AiTeachingAssistant.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Npgsql;

internal static class DemoClassmateAccounts
{
    public static async Task Activate(string root)
    {
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
            throw new InvalidOperationException("Demo classmate activation requires Development.");

        var password = Environment.GetEnvironmentVariable("PORTAL_DEMO_PASSWORD");
        if (string.IsNullOrEmpty(password) || password.Length < 12)
            throw new InvalidOperationException("Set PORTAL_DEMO_PASSWORD to at least 12 characters.");

        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Portal");
        if (string.IsNullOrWhiteSpace(connection))
        {
            using var config = System.Text.Json.JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(root, "appsettings.Local.json")));
            connection = config.RootElement.GetProperty("ConnectionStrings").GetProperty("Portal").GetString();
        }
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Portal connection string is missing.");

        await using var db = NpgsqlDataSource.Create(connection);
        await using var conn = await db.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(741026111)", conn, tx))
            await gate.ExecuteNonQueryAsync();

        var classmates = new List<(Guid Id, string Name, string? Email, string? StudentNumber, bool HasCredential)>();
        await using (var read = new NpgsqlCommand("""
            SELECT u.id,u.display_name,u.email,u.student_number,(c.user_id IS NOT NULL)
            FROM app_users u
            LEFT JOIN portal_credentials c ON c.user_id=u.id
            WHERE u.status='active'
              AND u.display_name LIKE '教學展示%'
              AND EXISTS (
                SELECT 1 FROM course_members m
                WHERE m.user_id=u.id AND m.status='active' AND m.course_role='student'
              )
            ORDER BY u.display_name,u.id
            FOR UPDATE OF u
            """, conn, tx))
        await using (var reader = await read.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                classmates.Add((reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetBoolean(4)));
        }

        if (classmates.Count == 0)
            throw new InvalidOperationException("No enrolled 教學展示 classmates were found.");

        var hasher = new PasswordHasher<PortalUser>(Options.Create(new PasswordHasherOptions { IterationCount = 210000 }));
        var activated = 0;
        var verified = 0;
        var loginRows = new List<string>();
        for (var index = 0; index < classmates.Count; index++)
        {
            var row = classmates[index];
            var ordinal = index + 1;
            var email = string.IsNullOrWhiteSpace(row.Email)
                ? $"demo.classmate{ordinal:00}@portal-demo.example.invalid"
                : row.Email.Trim().ToLowerInvariant();
            var studentNumber = string.IsNullOrWhiteSpace(row.StudentNumber)
                ? $"DEMO115{ordinal:000}"
                : row.StudentNumber.Trim();

            await using (var conflicts = new NpgsqlCommand("""
                SELECT EXISTS(
                    SELECT 1 FROM app_users
                    WHERE id<>$1 AND (lower(email)=lower($2) OR student_number=$3)
                ) OR EXISTS(
                    SELECT 1 FROM portal_credentials
                    WHERE user_id<>$1 AND normalized_email=upper($2)
                )
                """, conn, tx))
            {
                conflicts.Parameters.AddWithValue(row.Id);
                conflicts.Parameters.AddWithValue(email);
                conflicts.Parameters.AddWithValue(studentNumber);
                if (await conflicts.ExecuteScalarAsync() is true)
                    throw new InvalidOperationException($"Demo login identity conflict for {row.Name}.");
            }

            await using (var update = new NpgsqlCommand("""
                UPDATE app_users
                SET email=$2,student_number=$3,identity_provider='local',external_subject=$1::text
                WHERE id=$1
                """, conn, tx))
            {
                update.Parameters.AddWithValue(row.Id);
                update.Parameters.AddWithValue(email);
                update.Parameters.AddWithValue(studentNumber);
                await update.ExecuteNonQueryAsync();
            }

            if (!row.HasCredential)
            {
                var user = new PortalUser(row.Id, row.Name, email, PortalRoles.Member, studentNumber);
                await using var credential = new NpgsqlCommand("""
                    INSERT INTO portal_credentials(user_id,normalized_email,password_hash,platform_role,can_create_courses)
                    VALUES($1,upper($2),$3,'Member',false)
                    """, conn, tx);
                credential.Parameters.AddWithValue(row.Id);
                credential.Parameters.AddWithValue(email);
                credential.Parameters.AddWithValue(hasher.HashPassword(user, password));
                await credential.ExecuteNonQueryAsync();

                await using var audit = new NpgsqlCommand("""
                    INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata)
                    VALUES(NULL,'app_users',$1,'demo.student_account.activated',jsonb_build_object('source','existing-demo-activity'))
                    """, conn, tx);
                audit.Parameters.AddWithValue(row.Id);
                await audit.ExecuteNonQueryAsync();
                activated++;
            }
            else
            {
                verified++;
            }

            await using (var credentialCheck = new NpgsqlCommand(
                "SELECT password_hash,platform_role,can_create_courses FROM portal_credentials WHERE user_id=$1", conn, tx))
            {
                credentialCheck.Parameters.AddWithValue(row.Id);
                await using var credentialReader = await credentialCheck.ExecuteReaderAsync();
                if (!await credentialReader.ReadAsync())
                    throw new InvalidOperationException($"Credential creation failed for {row.Name}.");
                var user = new PortalUser(row.Id, row.Name, email, PortalRoles.Member, studentNumber);
                if (credentialReader.GetString(1) != PortalRoles.Member || credentialReader.GetBoolean(2) ||
                    hasher.VerifyHashedPassword(user, credentialReader.GetString(0), password) == PasswordVerificationResult.Failed)
                    throw new InvalidOperationException($"Demo credential verification failed for {row.Name}.");
            }

            await using (var profile = new NpgsqlCommand("""
                INSERT INTO studio_profiles(user_id,department_year)
                VALUES($1,'教學展示班') ON CONFLICT(user_id) DO NOTHING
                """, conn, tx))
            {
                profile.Parameters.AddWithValue(row.Id);
                await profile.ExecuteNonQueryAsync();
            }
            loginRows.Add($"{studentNumber}\t{row.Name}\t{email}");
        }

        await tx.CommitAsync();
        Console.WriteLine($"Activated {activated} existing demo classmates; verified {verified} existing accounts.");
        foreach (var row in loginRows)
            Console.WriteLine(row);
    }
}
