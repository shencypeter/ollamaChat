using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AiTeachingAssistant.Controllers;

public sealed partial class CourseToolsController
{
    public sealed class ResourceMetadataInput
    {
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings=true),System.Text.Json.Serialization.JsonRequired]
        [System.ComponentModel.DataAnnotations.StringLength(10000)]
        public string Summary {get;set;}="";
        [System.Text.Json.Serialization.JsonRequired]
        public string? TeachingDate {get;set;}
        [System.Text.Json.Serialization.JsonRequired]
        public int ExpectedVersion {get;set;}
    }

    [HttpGet("resources/{id:guid}/metadata")]
    public async Task<IActionResult> Metadata(Guid course,Guid id,CancellationToken ct)
    {
        if(await Access(course,false,ct)==null)return Forbid();
        await using var cmd=source.CreateCommand("SELECT summary,metadata_version,teaching_date FROM course_resources WHERE course_id=$1 AND id=$2");
        cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(id);
        await using var r=await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct)?Ok(new{summary=r.GetString(0),metadataVersion=r.GetInt32(1),teachingDate=r.IsDBNull(2)?(DateOnly?)null:r.GetFieldValue<DateOnly>(2)}):NotFound();
    }

    [HttpPut("resources/{id:guid}/metadata")]
    public async Task<IActionResult> SaveMetadata(Guid course,Guid id,ResourceMetadataInput input,CancellationToken ct)
    {
        if(await Access(course,true,ct)==null)return Forbid();
        DateOnly? date;string summary;
        try{date=Services.ResourceMetadataPolicy.Date(input.TeachingDate);summary=Services.ResourceMetadataPolicy.Summary(input.Summary);}
        catch(ArgumentException e){return BadRequest(new{error=e.Message});}
        if(input.ExpectedVersion<1)return BadRequest(new{error="請提供有效的授課日期／摘要版本。"});
        await using var conn=await source.OpenConnectionAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(ct);
        await using(var gate=new NpgsqlCommand("SELECT status FROM courses WHERE id=$1 FOR SHARE",conn))
        {
            gate.Parameters.AddWithValue(course);
            if(await gate.ExecuteScalarAsync(ct) is not string status || status=="archived")return Forbid();
        }
        int version;
        await using(var update=new NpgsqlCommand("UPDATE course_resources SET summary=$3,metadata_version=metadata_version+1,teaching_date=$5 WHERE course_id=$1 AND id=$2 AND metadata_version=$4 RETURNING metadata_version",conn))
        {
            update.Parameters.AddWithValue(course);update.Parameters.AddWithValue(id);
            update.Parameters.AddWithValue(summary);update.Parameters.AddWithValue(input.ExpectedVersion);
            update.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Date,Value=(object?)date??DBNull.Value});
            if(await update.ExecuteScalarAsync(ct) is not int saved)
            {
                await using var check=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM course_resources WHERE course_id=$1 AND id=$2)",conn);
                check.Parameters.AddWithValue(course);check.Parameters.AddWithValue(id);
                return await check.ExecuteScalarAsync(ct) is true?Conflict(new{error="授課日期／摘要已被其他操作更新，請取消編輯後重新開啟。"}):NotFound();
            }
            version=saved;
        }
        await using(var audit=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata) VALUES($1,'course_resources',$2,'resource.metadata_updated',jsonb_build_object('version',$3::int))",conn))
        {
            audit.Parameters.AddWithValue(Actor);audit.Parameters.AddWithValue(id);audit.Parameters.AddWithValue(version);await audit.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return Ok(new{summary,teachingDate=date,metadataVersion=version});
    }
}
