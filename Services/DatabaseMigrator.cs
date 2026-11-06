using System.Security.Cryptography;
using System.Text;
using Npgsql;
namespace AiTeachingAssistant.Services;

public static class DatabaseMigrator
{
    public static async Task RunAsync(NpgsqlDataSource source, string directory, bool apply)
    {
        await using var connection = await source.OpenConnectionAsync();
        await using (var version = new NpgsqlCommand("SELECT version()", connection))
            Console.WriteLine(await version.ExecuteScalarAsync());
        await using (var tables = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename", connection))
        await using (var reader = await tables.ExecuteReaderAsync())
            while (await reader.ReadAsync()) Console.WriteLine("Existing table: " + reader.GetString(0));
        if (!apply) return;
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(741026001)", connection, transaction)) await gate.ExecuteNonQueryAsync();
        await using (var history = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS portal_schema_migrations (version text PRIMARY KEY, checksum text NOT NULL, applied_at timestamptz NOT NULL DEFAULT now())", connection, transaction)) await history.ExecuteNonQueryAsync();
        foreach (var path in Directory.GetFiles(directory, "*.sql").Order())
        {
            var version = Path.GetFileName(path); var sql = await File.ReadAllTextAsync(path);
            var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
            await using var existing = new NpgsqlCommand("SELECT checksum FROM portal_schema_migrations WHERE version=$1", connection, transaction);
            existing.Parameters.AddWithValue(version);
            var recorded = await existing.ExecuteScalarAsync();
            if (recorded is string known)
            {
                if (known != checksum) throw new InvalidOperationException("Applied migration checksum differs: " + version);
                Console.WriteLine("Already applied: " + version); continue;
            }
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync();
            await using var record = new NpgsqlCommand("INSERT INTO portal_schema_migrations(version,checksum) VALUES($1,$2)", connection, transaction);
            record.Parameters.AddWithValue(version); record.Parameters.AddWithValue(checksum); await record.ExecuteNonQueryAsync();
            Console.WriteLine("Applied: " + version);
        }
        await transaction.CommitAsync();
        Console.WriteLine("Migration transaction COMMITTED.");
    }
}
