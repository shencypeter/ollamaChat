using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AiTeachingAssistant.Controllers;

public sealed partial class CourseToolsController
{
    public sealed class ResourceTagsInput
    {
        [System.ComponentModel.DataAnnotations.Required,System.Text.Json.Serialization.JsonRequired]
        public List<string?> Tags {get;set;}=[];
        [System.Text.Json.Serialization.JsonRequired]
        public int ExpectedVersion {get;set;}
    }

    [AiTeachingAssistant.Services.CourseCapability("materials")]
    [HttpPut("resources/{id:guid}/tags")]
    public async Task<IActionResult> SaveTags(Guid course,Guid id,ResourceTagsInput input,CancellationToken ct)
    {
        if(await Access(course,true,ct)==null)return Forbid();
        string[] tags;
        try {tags=Services.ResourceTagPolicy.Normalize(input.Tags);}
        catch(ArgumentException error){return BadRequest(new{error=error.Message});}
        if(input.ExpectedVersion<1)return BadRequest(new{error="請提供有效的標籤版本。"});
        await using var conn=await source.OpenConnectionAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(ct);
        await using(var gate=new NpgsqlCommand("SELECT status FROM courses WHERE id=$1 FOR SHARE",conn))
        {
            gate.Parameters.AddWithValue(course);
            if(await gate.ExecuteScalarAsync(ct) is not string status || status=="archived")return Forbid();
        }
        int version;
        await using(var update=new NpgsqlCommand("UPDATE course_resources SET tags=$3,tags_version=tags_version+1 WHERE course_id=$1 AND id=$2 AND tags_version=$4 RETURNING tags_version",conn))
        {
            update.Parameters.AddWithValue(course);update.Parameters.AddWithValue(id);
            update.Parameters.AddWithValue(tags);update.Parameters.AddWithValue(input.ExpectedVersion);
            if(await update.ExecuteScalarAsync(ct) is not int saved)
            {
                await using var check=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM course_resources WHERE course_id=$1 AND id=$2)",conn);
                check.Parameters.AddWithValue(course);check.Parameters.AddWithValue(id);
                return await check.ExecuteScalarAsync(ct) is true?Conflict(new{error="標籤已被其他操作更新，請取消編輯後重新開啟。"}):NotFound();
            }
            version=saved;
        }
        await using(var audit=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata) VALUES($1,'course_resources',$2,'resource.tags_updated',jsonb_build_object('version',$3::int))",conn))
        {
            audit.Parameters.AddWithValue(Actor);audit.Parameters.AddWithValue(id);audit.Parameters.AddWithValue(version);await audit.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return Ok(new{tags,tagsVersion=version});
    }
}
