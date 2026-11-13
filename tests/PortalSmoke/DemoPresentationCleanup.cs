using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiTeachingAssistant.Services;
using Npgsql;

internal static class DemoPresentationCleanup
{
    private sealed record Target(string Table,string[] Keys,string[] Fields,string Scope,bool Versioned=false);
    private sealed record Row(object[] Keys,string?[] Values);
    private const string ReportScope="EXISTS(SELECT 1 FROM report_grade_events e WHERE e.submission_id=t.submission_id AND e.details->>'codexDemo'='true')";
    private static readonly Target[] Targets=[
        new("grades",["id"],["feedback"],"EXISTS(SELECT 1 FROM submission_revisions r JOIN submissions s ON s.id=r.submission_id JOIN audit_events e ON e.entity_id=s.assignment_id AND e.action='demo.submitted_assignment.20261112' WHERE r.id=t.submission_revision_id)"),
        new("report_submissions",["id"],["feedback","published_feedback","file_name"],"EXISTS(SELECT 1 FROM report_grade_events e WHERE e.submission_id=t.id AND e.details->>'codexDemo'='true')",true),
        new("report_previous_files",["submission_id","revision"],["file_name"],ReportScope),
        new("report_peer_reviews",["submission_id","reviewer","subject"],["comment"],ReportScope),
        new("report_votes",["submission_id","voter"],["explanation"],ReportScope)
    ];
    public static async Task Run(bool preview,bool audit)
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development")throw new InvalidOperationException("Demo presentation maintenance requires Development.");
        var connection=Environment.GetEnvironmentVariable("ConnectionStrings__Portal");
        if(string.IsNullOrWhiteSpace(connection))throw new InvalidOperationException("Specify ConnectionStrings__Portal.");
        await using var source=NpgsqlDataSource.Create(connection);
        await using var db=await source.OpenConnectionAsync();await using var tx=await db.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        if(!preview&&!audit){await using var gate=new NpgsqlCommand("SELECT pg_advisory_xact_lock(741026112)",db,tx);await gate.ExecuteNonQueryAsync();}
        var total=0;
        foreach(var target in Targets)
        {
            var before=await Fingerprint(db,tx,target);var changed=0;
            var rows=new List<Row>();
            await using(var cmd=new NpgsqlCommand($"SELECT {string.Join(',',target.Keys.Concat(target.Fields).Select(field=>"t."+field))} FROM {target.Table} t WHERE {target.Scope}"+(!preview&&!audit?" FOR UPDATE OF t":""),db,tx))
            await using(var reader=await cmd.ExecuteReaderAsync())
                while(await reader.ReadAsync())rows.Add(new(Enumerable.Range(0,target.Keys.Length).Select(reader.GetValue).ToArray(),Enumerable.Range(target.Keys.Length,target.Fields.Length).Select(i=>reader.IsDBNull(i)?null:reader.GetString(i)).ToArray()));
            foreach(var row in rows)
            {
                var cleaned=row.Values.Select((value,i)=>value is null?null:DemoPresentationText.Clean(target.Fields[i],value)).ToArray();
                var fields=target.Fields.Where((_,i)=>cleaned[i]!=row.Values[i]).ToArray();if(fields.Length==0)continue;
                changed++;
                if(preview||audit)continue;
                var set=target.Fields.Select((field,i)=>$"{field}=${i+1}");
                var where=target.Keys.Select((key,i)=>$"{key}=${target.Fields.Length+i+1}");
                await using(var update=new NpgsqlCommand($"UPDATE {target.Table} SET {string.Join(',',set)}{(target.Versioned?",version=version+1":"")} WHERE {string.Join(" AND ",where)}",db,tx))
                {
                    foreach(var value in cleaned)update.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Text,Value=(object?)value??DBNull.Value});
                    foreach(var key in row.Keys)update.Parameters.AddWithValue(key);
                    if(await update.ExecuteNonQueryAsync()!=1)throw new InvalidOperationException("A demo record changed during presentation maintenance.");
                }
                // Keep original seed/audit events. This is a text correction, never a new teacher grade.
                await using var log=new NpgsqlCommand("INSERT INTO audit_events(entity_type,entity_id,action,metadata) VALUES($1,$2,'demo.presentation_text.normalized',$3::jsonb)",db,tx);
                log.Parameters.AddWithValue(target.Table);log.Parameters.AddWithValue((Guid)row.Keys[0]);
                log.Parameters.AddWithValue(JsonSerializer.Serialize(new{fields,beforeHash=Hash(row.Values),afterHash=Hash(cleaned)}));await log.ExecuteNonQueryAsync();
            }
            if(before!=await Fingerprint(db,tx,target))throw new InvalidOperationException($"Presentation maintenance changed protected {target.Table} data; rolling back.");
            Console.WriteLine($"{target.Table}: {changed} {(preview?"would change":audit?"remaining":"updated")} rows; protected fields unchanged.");total+=changed;
        }
        if(audit&&total!=0)throw new InvalidOperationException("Demo presentation text still requires normalization.");
        if(!preview&&!audit)await tx.CommitAsync();else await tx.RollbackAsync();
        Console.WriteLine($"Demo presentation {(preview?"preview":audit?"audit":"update")} complete: {total} rows. Scores, releases, file bytes, authors and immutable answers preserved.");
    }
    private static string Hash(string?[] values)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values))));
    private static async Task<string> Fingerprint(NpgsqlConnection db,NpgsqlTransaction tx,Target target)
    {
        var excluded=target.Fields.Concat(target.Versioned?["version"]:Array.Empty<string>()).Select(field=>"-'"+field+"'");
        await using var cmd=new NpgsqlCommand($"SELECT md5(COALESCE(string_agg((to_jsonb(t){string.Concat(excluded)})::text,'' ORDER BY {string.Join(',',target.Keys.Select(key=>"t."+key))}),'')) FROM {target.Table} t WHERE {target.Scope}",db,tx);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }
}
