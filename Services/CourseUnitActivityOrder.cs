using Npgsql;

namespace AiTeachingAssistant.Services;

public readonly record struct ActivityKey(string Kind, Guid Id);

public static class CourseUnitActivityOrder
{
    private static readonly IReadOnlyDictionary<string, string> Tables = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["material"] = "course_resources",
        ["quiz"] = "assignments",
        ["assignment"] = "assignments",
        ["report"] = "report_assignments"
    };

    public static IReadOnlyList<ActivityKey> Normalize(IEnumerable<ActivityKey> items)
    {
        var result = items.ToArray();
        if (result.Any(item => !Tables.ContainsKey(item.Kind)))
            throw new ArgumentException("不支援的單元活動類型。");
        if (result.Select(item => (item.Kind is "quiz" or "assignment" ? "assignment" : item.Kind, item.Id)).Distinct().Count() != result.Length)
            throw new ArgumentException("單元活動排序不可重複。");
        return result;
    }

    public static async Task<int> NextAsync(NpgsqlConnection connection, Guid courseId, Guid unitId, CancellationToken ct)
    {
        await LockAsync(connection,courseId,[unitId],ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT COALESCE(max(unit_sort_order),0)+1 FROM (
              SELECT unit_sort_order FROM course_resources WHERE course_id=$1 AND unit_id=$2
              UNION ALL SELECT unit_sort_order FROM assignments WHERE course_id=$1 AND unit_id=$2
              UNION ALL SELECT unit_sort_order FROM report_assignments WHERE course_id=$1 AND unit_id=$2
            ) ordered
            """, connection);
        cmd.Parameters.AddWithValue(courseId);
        cmd.Parameters.AddWithValue(unitId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public static async Task ReorderAsync(NpgsqlConnection connection, Guid courseId, Guid unitId, IReadOnlyList<ActivityKey> items, CancellationToken ct)
    {
        var normalized = Normalize(items);
        for (var index = 0; index < normalized.Count; index++)
        {
            var item = normalized[index];
            await using var cmd = new NpgsqlCommand($"UPDATE {Tables[item.Kind]} SET unit_sort_order=$4 WHERE id=$1 AND course_id=$2 AND unit_id=$3", connection);
            cmd.Parameters.AddWithValue(item.Id);
            cmd.Parameters.AddWithValue(courseId);
            cmd.Parameters.AddWithValue(unitId);
            cmd.Parameters.AddWithValue(index + 1);
            if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new ArgumentException("排序包含不屬於此單元的活動。");
        }
    }
    public static async Task LockAsync(NpgsqlConnection connection, Guid courseId, IEnumerable<Guid> unitIds, CancellationToken ct)
    {
        foreach (var unitId in unitIds.Distinct().Order())
        {
            await using var gate = new NpgsqlCommand("SELECT id FROM course_units WHERE id=$1 AND course_id=$2 FOR UPDATE", connection);
            gate.Parameters.AddWithValue(unitId);
            gate.Parameters.AddWithValue(courseId);
            if (await gate.ExecuteScalarAsync(ct) is not Guid) throw new ArgumentException("單元不屬於本課程。");
        }
    }
}


public static class ReportUnitOrderPolicy
{
    public static int? OrderToWrite(Guid? previousUnit,Guid? nextUnit,int nextOrder)=>previousUnit==nextUnit?null:nextOrder;
}
