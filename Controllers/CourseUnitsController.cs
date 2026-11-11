using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace AiTeachingAssistant.Controllers;

[ApiController, Authorize, Route("api/courses/{course:guid}")]
public sealed class CourseUnitsController(NpgsqlDataSource source, LearnerExperience experience) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public sealed class UnitInput
    {
        [Required, StringLength(160)] public string Name { get; set; } = "";
        [StringLength(8000)] public string Description { get; set; } = "";
        public int ExpectedVersion { get; set; }
    }
    public sealed class ReorderInput { [Required] public Guid[] UnitIds { get; set; } = []; }
    public sealed class MoveInput { public Guid? UnitId { get; set; } }

    private async Task<string?> Role(NpgsqlConnection conn, Guid course, bool write, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT m.course_role,c.status FROM courses c JOIN course_members m ON m.course_id=c.id
            WHERE c.id=$1 AND m.user_id=$2 AND m.status='active' AND (c.status<>'draft' OR m.course_role IN ('teacher','ta'))
            """, conn);
        cmd.Parameters.AddWithValue(course); cmd.Parameters.AddWithValue(Actor);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var role = experience.IsPractice ? "student" : reader.GetString(0);
        if (write && (role != "teacher" || reader.GetString(1) == "archived")) return null;
        return role;
    }

    [HttpGet("curriculum")]
    public async Task<IActionResult> Curriculum(Guid course, CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        var role = await Role(conn, course, false, ct); if (role is null) return NotFound();
        var staff = role is "teacher" or "ta";
        var units = new List<object>();
        await using (var cmd = new NpgsqlCommand("SELECT id,name,description,sort_order,version,created_at,updated_at FROM course_units WHERE course_id=$1 ORDER BY sort_order,id", conn))
        {
            cmd.Parameters.AddWithValue(course); await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) units.Add(new { id=r.GetGuid(0),name=r.GetString(1),description=r.GetString(2),sortOrder=r.GetInt32(3),version=r.GetInt32(4),createdAt=r.GetFieldValue<DateTimeOffset>(5),updatedAt=r.GetFieldValue<DateTimeOffset>(6) });
        }
        var activities = new List<object>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT id,unit_id,'material',COALESCE(NULLIF(display_title,''),file_name),resource_type,created_at,NULL::timestamptz
            FROM course_resources WHERE course_id=$1
            UNION ALL
            SELECT id,unit_id,CASE WHEN EXISTS(SELECT 1 FROM course_grade_items i WHERE i.source_assignment_id=assignments.id) THEN 'quiz' ELSE 'assignment' END,title,status,created_at,due_at FROM assignments
            WHERE course_id=$1 AND ($2 OR status<>'draft')
            UNION ALL
            SELECT id,unit_id,'report',title,status,created_at,due_at FROM report_assignments a
            WHERE course_id=$1 AND ($2 OR status<>'draft') AND ($2 OR submission_mode='individual' OR EXISTS(
              SELECT 1 FROM report_group_audience WHERE report_id=a.id AND student_id=$3))
            UNION ALL
            SELECT id,unit_id,'exam',title,status,created_at,due_at FROM exams
            WHERE course_id=$1 AND ($2 OR status<>'draft')
            ORDER BY 6,1
            """, conn))
        {
            cmd.Parameters.AddWithValue(course); cmd.Parameters.AddWithValue(staff); cmd.Parameters.AddWithValue(Actor);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) activities.Add(new { id=r.GetGuid(0),unitId=r.IsDBNull(1)?(Guid?)null:r.GetGuid(1),kind=r.GetString(2),title=r.GetString(3),status=r.GetString(4),createdAt=r.GetFieldValue<DateTimeOffset>(5),dueAt=r.IsDBNull(6)?(DateTimeOffset?)null:r.GetFieldValue<DateTimeOffset>(6) });
        }
        return Ok(new { units, activities, canManage = role == "teacher" });
    }

    [HttpPost("units")]
    public async Task<IActionResult> Create(Guid course, UnitInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error="請輸入單元名稱。" });
        await using var conn=await source.OpenConnectionAsync(ct); await using var tx=await conn.BeginTransactionAsync(ct);
        if(await Role(conn,course,true,ct) is null)return Forbid();
        await using var gate=new NpgsqlCommand("SELECT id FROM courses WHERE id=$1 FOR UPDATE",conn);gate.Parameters.AddWithValue(course);await gate.ExecuteScalarAsync(ct);
        var id=Guid.NewGuid();await using var cmd=new NpgsqlCommand("INSERT INTO course_units(id,course_id,name,description,sort_order) SELECT $1,$2,$3,$4,COALESCE(max(sort_order),0)+1 FROM course_units WHERE course_id=$2 RETURNING sort_order",conn);
        cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(input.Name.Trim());cmd.Parameters.AddWithValue(input.Description.Trim());var order=(int)(await cmd.ExecuteScalarAsync(ct))!;
        await Audit(conn,id,"course.unit.created",ct);await tx.CommitAsync(ct);return Ok(new{id,name=input.Name.Trim(),description=input.Description.Trim(),sortOrder=order,version=1});
    }

    [HttpPut("units/{id:guid}")]
    public async Task<IActionResult> Update(Guid course,Guid id,UnitInput input,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(input.Name))return BadRequest(new{error="請輸入單元名稱。"});
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);if(await Role(conn,course,true,ct) is null)return Forbid();
        await using var cmd=new NpgsqlCommand("UPDATE course_units SET name=$3,description=$4,version=version+1,updated_at=now() WHERE id=$1 AND course_id=$2 AND version=$5",conn);
        cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(input.Name.Trim());cmd.Parameters.AddWithValue(input.Description.Trim());cmd.Parameters.AddWithValue(input.ExpectedVersion);
        if(await cmd.ExecuteNonQueryAsync(ct)==0)return Conflict(new{error="單元已被其他操作更新，請重新載入。"});await Audit(conn,id,"course.unit.updated",ct);await tx.CommitAsync(ct);return NoContent();
    }

    [HttpPut("units/reorder")]
    public async Task<IActionResult> Reorder(Guid course,ReorderInput input,CancellationToken ct)
    {
        if(input.UnitIds.Distinct().Count()!=input.UnitIds.Length)return BadRequest(new{error="單元排序不可重複。"});
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);if(await Role(conn,course,true,ct) is null)return Forbid();
        await using(var gate=new NpgsqlCommand("SELECT id FROM courses WHERE id=$1 FOR UPDATE",conn)){gate.Parameters.AddWithValue(course);await gate.ExecuteScalarAsync(ct);}
        await using(var count=new NpgsqlCommand("SELECT count(*) FROM course_units WHERE course_id=$1 AND id=ANY($2)",conn)){count.Parameters.AddWithValue(course);count.Parameters.AddWithValue(input.UnitIds);if((long)(await count.ExecuteScalarAsync(ct))! != input.UnitIds.Length || input.UnitIds.Length!=(long)(await Scalar(conn,"SELECT count(*) FROM course_units WHERE course_id=$1",course,ct))!)return BadRequest(new{error="請提交本課程完整的單元排序。"});}
        await using var cmd=new NpgsqlCommand("UPDATE course_units u SET sort_order=x.ord,updated_at=now() FROM unnest($2::uuid[]) WITH ORDINALITY x(id,ord) WHERE u.course_id=$1 AND u.id=x.id",conn);cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(input.UnitIds);await cmd.ExecuteNonQueryAsync(ct);
        await Audit(conn,course,"course.units.reordered",ct);await tx.CommitAsync(ct);return NoContent();
    }

    [HttpDelete("units/{id:guid}")]
    public async Task<IActionResult> Delete(Guid course,Guid id,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);if(await Role(conn,course,true,ct) is null)return Forbid();
        var used=await Scalar(conn,"SELECT EXISTS(SELECT 1 FROM course_resources WHERE course_id=$1 AND unit_id=$2) OR EXISTS(SELECT 1 FROM assignments WHERE course_id=$1 AND unit_id=$2) OR EXISTS(SELECT 1 FROM report_assignments WHERE course_id=$1 AND unit_id=$2) OR EXISTS(SELECT 1 FROM exams WHERE course_id=$1 AND unit_id=$2)",course,ct,id);
        if(used is true)return Conflict(new{error="單元仍包含教材、測驗或作業；請先移動內容後再刪除。"});
        await using var cmd=new NpgsqlCommand("DELETE FROM course_units WHERE id=$1 AND course_id=$2",conn);cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);if(await cmd.ExecuteNonQueryAsync(ct)==0)return NotFound();
        await using var normalize=new NpgsqlCommand("WITH x AS(SELECT id,row_number() OVER(ORDER BY sort_order,id) n FROM course_units WHERE course_id=$1) UPDATE course_units u SET sort_order=x.n FROM x WHERE u.id=x.id",conn);normalize.Parameters.AddWithValue(course);await normalize.ExecuteNonQueryAsync(ct);
        await Audit(conn,id,"course.unit.deleted",ct);await tx.CommitAsync(ct);return NoContent();
    }

    [HttpPut("units/activities/{kind}/{id:guid}")]
    public async Task<IActionResult> Move(Guid course,string kind,Guid id,MoveInput input,CancellationToken ct)
    {
        var table=kind switch{"material"=>"course_resources","quiz" or "assignment"=>"assignments","report"=>"report_assignments","exam"=>"exams",_=>null};if(table is null)return NotFound();
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);if(await Role(conn,course,true,ct) is null)return Forbid();
        if(input.UnitId.HasValue && await Scalar(conn,"SELECT EXISTS(SELECT 1 FROM course_units WHERE id=$1 AND course_id=$2)",input.UnitId.Value,ct,course) is not true)return BadRequest(new{error="單元不屬於本課程。"});
        await using var cmd=new NpgsqlCommand($"UPDATE {table} SET unit_id=$3 WHERE id=$1 AND course_id=$2",conn);cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(course);cmd.Parameters.Add(new NpgsqlParameter{NpgsqlDbType=NpgsqlDbType.Uuid,Value=(object?)input.UnitId??DBNull.Value});if(await cmd.ExecuteNonQueryAsync(ct)==0)return NotFound();
        await Audit(conn,id,"course.activity.unit_changed",ct);await tx.CommitAsync(ct);return NoContent();
    }

    private async Task<object?> Scalar(NpgsqlConnection conn,string sql,object first,CancellationToken ct,object? second=null){await using var cmd=new NpgsqlCommand(sql,conn);cmd.Parameters.AddWithValue(first);if(second is not null)cmd.Parameters.AddWithValue(second);return await cmd.ExecuteScalarAsync(ct);}
    private async Task Audit(NpgsqlConnection conn,Guid entity,string action,CancellationToken ct){await using var cmd=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action) VALUES($1,'course_units',$2,$3)",conn);cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(entity);cmd.Parameters.AddWithValue(action);await cmd.ExecuteNonQueryAsync(ct);}
}
