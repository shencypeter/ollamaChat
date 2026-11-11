using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AiTeachingAssistant.Controllers;

public sealed partial class CourseToolsController
{
    public sealed class ResourceTranscriptInput
    {
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings=true),System.Text.Json.Serialization.JsonRequired]
        [System.ComponentModel.DataAnnotations.StringLength(100000)]
        public string Transcript {get;set;}="";
        [System.Text.Json.Serialization.JsonRequired]
        public int ExpectedVersion {get;set;}
    }

    [HttpGet("resources/{id:guid}/transcript")]
    public async Task<IActionResult> Transcript(Guid course,Guid id,CancellationToken ct)
    {
        if(await Access(course,false,ct)==null)return Forbid();
        await using var cmd=source.CreateCommand("SELECT transcript,transcript_version FROM course_resources WHERE course_id=$1 AND id=$2 AND resource_type='youtube'");
        cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(id);
        await using var r=await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct)?Ok(new{transcript=r.GetString(0),transcriptVersion=r.GetInt32(1)}):NotFound();
    }

    [AiTeachingAssistant.Services.CourseCapability("materials")]
    [HttpPut("resources/{id:guid}/transcript")]
    public async Task<IActionResult> SaveTranscript(Guid course,Guid id,ResourceTranscriptInput input,CancellationToken ct)
    {
        if(await Access(course,true,ct)==null)return Forbid();
        var transcript=input.Transcript.Trim();
        if(input.ExpectedVersion<1)return BadRequest(new{error="請提供有效的逐字稿版本。"});
        await using var conn=await source.OpenConnectionAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(ct);
        await using(var gate=new NpgsqlCommand("SELECT status FROM courses WHERE id=$1 FOR SHARE",conn))
        {
            gate.Parameters.AddWithValue(course);
            if(await gate.ExecuteScalarAsync(ct) is not string status || status=="archived")return Forbid();
        }
        int version;
        await using(var update=new NpgsqlCommand("UPDATE course_resources SET transcript=$3,transcript_version=transcript_version+1 WHERE course_id=$1 AND id=$2 AND resource_type='youtube' AND transcript_version=$4 RETURNING transcript_version",conn))
        {
            update.Parameters.AddWithValue(course);update.Parameters.AddWithValue(id);
            update.Parameters.AddWithValue(transcript);update.Parameters.AddWithValue(input.ExpectedVersion);
            if(await update.ExecuteScalarAsync(ct) is not int saved)
            {
                await using var check=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM course_resources WHERE course_id=$1 AND id=$2 AND resource_type='youtube')",conn);
                check.Parameters.AddWithValue(course);check.Parameters.AddWithValue(id);
                return await check.ExecuteScalarAsync(ct) is true?Conflict(new{error="逐字稿已被其他操作更新，請取消編輯後重新開啟。"}):NotFound();
            }
            version=saved;
        }
        await using(var audit=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata) VALUES($1,'course_resources',$2,'resource.transcript_updated',jsonb_build_object('version',$3::int))",conn))
        {
            audit.Parameters.AddWithValue(Actor);audit.Parameters.AddWithValue(id);audit.Parameters.AddWithValue(version);await audit.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return Ok(new{transcript,transcriptVersion=version});
    }
}
