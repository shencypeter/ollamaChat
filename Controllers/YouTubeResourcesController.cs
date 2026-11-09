using System.ComponentModel.DataAnnotations;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AiTeachingAssistant.Controllers;

public sealed partial class CourseToolsController
{
    public sealed class YouTubeResourceInput
    {
        [Required,StringLength(200)]public string Title{get;set;}="";
        [Required,StringLength(2000)]public string Url{get;set;}="";
        [StringLength(100000)]public string? Transcript{get;set;}
        public string? TeachingDate{get;set;}
        [StringLength(10000)]public string? Summary{get;set;}
        public List<string?>? Tags{get;set;}=[];
    }

    [HttpPost("resources/youtube")]
    public async Task<IActionResult> AddYouTube(Guid course,YouTubeResourceInput input,CancellationToken ct)
    {
        if(await Access(course,true,ct)==null)return Forbid();
        if(string.IsNullOrWhiteSpace(input.Title))return BadRequest(new{error="請填寫課程回放名稱。"});
        string videoId;string[] tags;DateOnly? date;string summary;
        try{videoId=YouTubeResourcePolicy.VideoId(input.Url);tags=ResourceTagPolicy.Normalize(input.Tags);date=ResourceMetadataPolicy.Date(input.TeachingDate);summary=ResourceMetadataPolicy.Summary(input.Summary);}
        catch(ArgumentException error){return BadRequest(new{error=error.Message});}
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        await using(var gate=new NpgsqlCommand("SELECT status FROM courses WHERE id=$1 FOR SHARE",conn))
        {gate.Parameters.AddWithValue(course);if(await gate.ExecuteScalarAsync(ct) is not string status || status=="archived")return Forbid();}
        Guid id;
        await using(var cmd=new NpgsqlCommand("INSERT INTO course_resources(course_id,file_name,content,uploaded_by,display_title,tags,resource_type,youtube_video_id,transcript,teaching_date,summary) VALUES($1,'YouTube 課程回放',$2,$3,$4,$5,'youtube',$6,$7,$8,$9) RETURNING id",conn))
        {cmd.Parameters.AddWithValue(course);cmd.Parameters.AddWithValue(Array.Empty<byte>());cmd.Parameters.AddWithValue(Actor);cmd.Parameters.AddWithValue(input.Title.Trim());cmd.Parameters.AddWithValue(tags);cmd.Parameters.AddWithValue(videoId);cmd.Parameters.AddWithValue(input.Transcript?.Trim()??"");cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Date,Value=(object?)date??DBNull.Value});cmd.Parameters.AddWithValue(summary);id=(Guid)(await cmd.ExecuteScalarAsync(ct))!;}
        await using(var audit=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action) VALUES($1,'course_resources',$2,'resource.youtube_added')",conn))
        {audit.Parameters.AddWithValue(Actor);audit.Parameters.AddWithValue(id);await audit.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct);return Ok(new{id});
    }
}
