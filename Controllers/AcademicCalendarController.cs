using System.Security.Claims;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AiTeachingAssistant.Controllers;

[ApiController, Route("api/academic-calendar"), Authorize]
public sealed class AcademicCalendarController(NpgsqlDataSource source, LearnerExperience experience, IOptions<AcademicCalendarOptions> options, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct, [FromQuery] bool studentPortal = false)
    {
        await using var cmd = source.CreateCommand("""
            SELECT DISTINCT c.term_code FROM courses c JOIN course_members m ON m.course_id=c.id
            WHERE m.user_id=$1 AND m.status='active'
              AND (NOT $3 OR m.course_role IN ('student','teacher'))
              AND (c.status<>'draft' OR ($3 AND m.course_role='teacher') OR (NOT $2 AND m.course_role IN ('teacher','ta')))
            ORDER BY c.term_code DESC
            """);
        cmd.Parameters.AddWithValue(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
        cmd.Parameters.AddWithValue(experience.IsPractice || studentPortal);
        cmd.Parameters.AddWithValue(studentPortal);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var terms = new List<string>();
        while (await reader.ReadAsync(ct)) terms.Add(reader.GetString(0));
        return Ok(new { currentTerm = options.Value.ResolveCurrentTerm(clock.GetUtcNow()), semesters = new[] { new { code = "1", label = "上學期" }, new { code = "2", label = "下學期" }, new { code = "S", label = "暑修" } }, terms });
    }
}
