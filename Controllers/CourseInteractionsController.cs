using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Serialization;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AiTeachingAssistant.Controllers;

[ApiController, Authorize, Route("api/courses/{course:guid}")]
public sealed class CourseInteractionsController(NpgsqlDataSource source, LearnerExperience experience) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed class LikeInput { [JsonRequired] public bool Liked { get; set; } }
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed class ViewInput { [JsonRequired] public Guid Id { get; set; } }
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed class TextInput
    {
        [JsonRequired] public Guid Id { get; set; }
        [StringLength(200)] public string? Title { get; set; }
        [Required, StringLength(8000)] public string Text { get; set; } = "";
    }
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed class ReplyInput
    {
        [Required, StringLength(16000)] public string Response { get; set; } = "";
        [Range(1, int.MaxValue)] public int ExpectedVersion { get; set; }
    }
    private static NpgsqlCommand Command(NpgsqlConnection db, string sql, params object[] args)
    {
        var cmd = new NpgsqlCommand(sql, db);
        foreach (var arg in args) cmd.Parameters.AddWithValue(arg);
        return cmd;
    }
    private static string DiscussionTitle(string? title, string text)
    {
        var value = string.IsNullOrWhiteSpace(title) ? text.Trim() : title.Trim();
        return value.Length <= 200 ? value : value[..200];
    }
    // Lock the course and membership for the whole request so archiving/revocation cannot race a write.
    private async Task<IActionResult> Run(Guid course, bool write, Func<NpgsqlConnection, string, bool, Task<IActionResult>> action, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(write ? System.Data.IsolationLevel.ReadCommitted : System.Data.IsolationLevel.RepeatableRead, ct);
        await using var access = Command(db, "SELECT m.course_role,c.status FROM course_members m JOIN courses c ON c.id=m.course_id JOIN app_users u ON u.id=m.user_id WHERE m.course_id=$1 AND m.user_id=$2 AND m.status='active' AND u.status='active' FOR SHARE OF m,c,u", course, Actor);
        string role, status;
        await using (var r = await access.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct)) return Forbid();
            role = r.GetString(0); status = r.GetString(1);
        }
        var writable = status != "archived" && !experience.IsPractice;
        if (write && !writable) return Forbid();
        var result = await action(db, role, writable);
        await tx.CommitAsync(ct);
        return result;
    }
    private async Task<bool> Resource(NpgsqlConnection db, Guid course, Guid id, CancellationToken ct)
    {
        await using var cmd = Command(db, "SELECT id FROM course_resources WHERE course_id=$1 AND id=$2 FOR SHARE", course, id);
        return await cmd.ExecuteScalarAsync(ct) is Guid;
    }
    [HttpGet("resources/{id:guid}/interactions")]
    public Task<IActionResult> Interactions(Guid course, Guid id, CancellationToken ct, [FromQuery, Range(1, 100000)] int page = 1) => Run(course, false, async (db, role, writable) =>
    {
        if (!await Resource(db, course, id, ct)) return NotFound();
        await using var counts = Command(db, "SELECT (SELECT count(*) FROM resource_likes WHERE resource_id=$1),(SELECT count(*) FROM resource_comments WHERE resource_id=$1),EXISTS(SELECT 1 FROM resource_likes WHERE resource_id=$1 AND user_id=$2)", id, Actor);
        long likes, total; bool liked;
        await using (var r = await counts.ExecuteReaderAsync(ct)) { await r.ReadAsync(ct); likes=r.GetInt64(0);total=r.GetInt64(1);liked=r.GetBoolean(2); }
        await using var cmd = Command(db, "SELECT c.id,u.display_name,c.body,c.created_at FROM resource_comments c JOIN app_users u ON u.id=c.author_id WHERE resource_id=$1 ORDER BY c.created_at DESC,c.id DESC LIMIT 20 OFFSET $2", id, (page-1)*20);
        var items = new List<object>();
        await using (var r = await cmd.ExecuteReaderAsync(ct)) while(await r.ReadAsync(ct)) items.Add(new { id=r.GetGuid(0),author=r.GetString(1),text=r.GetString(2),createdAt=r.GetFieldValue<DateTimeOffset>(3) });
        return Ok(new { likes, liked, total, page, pageSize=20, writable, items });
    }, ct);
    [HttpPost("resources/{id:guid}/views")]
    public Task<IActionResult> RecordView(Guid course, Guid id, ViewInput input, CancellationToken ct) => Run(course, false, async (db, role, writable) =>
    {
        if (!await Resource(db, course, id, ct)) return NotFound();
        if (input.Id == Guid.Empty) return BadRequest(new { error="請提供有效的點閱識別。" });
        if (!writable || role != "student") return NoContent();
        await using var cmd = Command(db, "INSERT INTO resource_views(id,resource_id,user_id) VALUES($1,$2,$3) ON CONFLICT DO NOTHING", input.Id,id,Actor);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0)
        {
            await using var same = Command(db, "SELECT EXISTS(SELECT 1 FROM resource_views WHERE id=$1 AND resource_id=$2 AND user_id=$3)", input.Id,id,Actor);
            if (await same.ExecuteScalarAsync(ct) is not true) return Conflict(new { error="點閱識別已使用，請重新整理。" });
        }
        return NoContent();
    }, ct);
    [HttpPut("resources/{id:guid}/like")]
    public Task<IActionResult> Like(Guid course, Guid id, LikeInput input, CancellationToken ct) => Run(course, true, async (db, role, writable) =>
    {
        if (!await Resource(db, course, id, ct)) return NotFound();
        await using var cmd = Command(db, input.Liked ? "INSERT INTO resource_likes(resource_id,user_id) VALUES($1,$2) ON CONFLICT DO NOTHING" : "DELETE FROM resource_likes WHERE resource_id=$1 AND user_id=$2", id, Actor);
        await cmd.ExecuteNonQueryAsync(ct); return NoContent();
    }, ct);
    [HttpPost("resources/{id:guid}/comments")]
    public Task<IActionResult> Comment(Guid course, Guid id, TextInput input, CancellationToken ct) => Run(course, true, async (db, role, writable) =>
    {
        if (!await Resource(db, course, id, ct)) return NotFound();
        if(input.Id==Guid.Empty || string.IsNullOrWhiteSpace(input.Text) || input.Text.Trim().Length>4000) return BadRequest(new { error="留言請填寫 1–4000 字及有效請求識別。" });
        await using var cmd = Command(db, "INSERT INTO resource_comments(id,resource_id,author_id,body) VALUES($1,$2,$3,$4) ON CONFLICT DO NOTHING", input.Id,id,Actor,input.Text.Trim());
        if(await cmd.ExecuteNonQueryAsync(ct)==0)
        {
            await using var same = Command(db,"SELECT EXISTS(SELECT 1 FROM resource_comments WHERE id=$1 AND resource_id=$2 AND author_id=$3 AND body=$4)",input.Id,id,Actor,input.Text.Trim());
            if(await same.ExecuteScalarAsync(ct) is not true)return Conflict(new{error="請求識別已使用，請重新送出。"});
        }
        return NoContent();
    }, ct);
    [HttpGet("questions")]
    public Task<IActionResult> Questions(Guid course, CancellationToken ct, [FromQuery, Range(1,100000)] int page=1, [FromQuery] bool all=false) => Run(course,false,async(db,role,writable)=>
    {
        await using var count=Command(db,"SELECT count(*) FROM course_questions WHERE course_id=$1",course);
        var total=(long)(await count.ExecuteScalarAsync(ct))!;
        var limit=all?int.MaxValue:20;var offset=all?0:(page-1)*20;
        await using var cmd=Command(db,"""
            SELECT q.id,COALESCE(NULLIF(q.title,''),left(btrim(q.question),200)),u.display_name,author_member.course_role,q.question,q.response,a.display_name,
                   answer_member.course_role,q.created_at,q.answered_at,q.version,q.locked,q.attachment_name,
                   (SELECT count(*) FROM course_question_votes v WHERE v.question_id=q.id),
                   EXISTS(SELECT 1 FROM course_question_votes v WHERE v.question_id=q.id AND v.user_id=$3),
                   (SELECT count(*) FROM course_question_replies r WHERE r.question_id=q.id),
                   GREATEST(q.created_at,COALESCE(q.answered_at,q.created_at),COALESCE((SELECT max(r.created_at) FROM course_question_replies r WHERE r.question_id=q.id),q.created_at))
            FROM course_questions q
            JOIN app_users u ON u.id=q.author_id
            LEFT JOIN course_members author_member ON author_member.course_id=q.course_id AND author_member.user_id=q.author_id
            LEFT JOIN app_users a ON a.id=q.answered_by
            LEFT JOIN course_members answer_member ON answer_member.course_id=q.course_id AND answer_member.user_id=q.answered_by
            WHERE q.course_id=$1
            ORDER BY q.created_at DESC,q.id DESC LIMIT $4 OFFSET $2
            """,course,offset,Actor,limit);
        var items=new List<object>();
        await using(var r=await cmd.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))items.Add(new{id=r.GetGuid(0),title=r.GetString(1),author=r.GetString(2),authorRole=r.IsDBNull(3)?null:r.GetString(3),question=r.GetString(4),response=r.GetString(5),answeredBy=r.IsDBNull(6)?null:r.GetString(6),answeredByRole=r.IsDBNull(7)?null:r.GetString(7),createdAt=r.GetFieldValue<DateTimeOffset>(8),answeredAt=r.IsDBNull(9)?(DateTimeOffset?)null:r.GetFieldValue<DateTimeOffset>(9),version=r.GetInt32(10),locked=r.GetBoolean(11),attachmentName=r.IsDBNull(12)?null:r.GetString(12),likes=r.GetInt64(13),liked=r.GetBoolean(14),replyCount=r.GetInt64(15)+(r.IsDBNull(9)?0:1),updatedAt=r.GetFieldValue<DateTimeOffset>(16)});
        return Ok(new{total,page,pageSize=all?total:20,canAsk=writable,canReply=writable,canModerate=writable&&AssignmentPolicy.IsStaff(role),items});
    },ct);
    [HttpPost("questions")]
    public Task<IActionResult> Ask(Guid course,TextInput input,CancellationToken ct)=>Run(course,true,async(db,role,writable)=>
    {
        if(input.Id==Guid.Empty||string.IsNullOrWhiteSpace(input.Text))return BadRequest(new{error="請填寫問題及有效請求識別。"});
        var title=DiscussionTitle(input.Title,input.Text);
        await using var cmd=Command(db,"INSERT INTO course_questions(id,course_id,author_id,title,question) VALUES($1,$2,$3,$4,$5) ON CONFLICT DO NOTHING",input.Id,course,Actor,title,input.Text.Trim());
        if(await cmd.ExecuteNonQueryAsync(ct)==0)
        {
            await using var same=Command(db,"SELECT EXISTS(SELECT 1 FROM course_questions WHERE id=$1 AND course_id=$2 AND author_id=$3 AND title=$4 AND question=$5)",input.Id,course,Actor,title,input.Text.Trim());
            if(await same.ExecuteScalarAsync(ct) is not true)return Conflict(new{error="請求識別已使用，請重新送出。"});
        }
        return NoContent();
    },ct);
    [HttpPost("questions/with-pdf"),RequestSizeLimit(6*1024*1024)]
    public Task<IActionResult> AskWithPdf(Guid course,[FromForm]Guid id,[FromForm]string text,IFormFile file,CancellationToken ct,[FromForm]string? title=null)=>Run(course,true,async(db,role,writable)=>{
        var name=Path.GetFileName(file.FileName.Replace((char)92,'/'));
        if(id==Guid.Empty||string.IsNullOrWhiteSpace(text)||text.Length>8000||name.Length>200||
           !name.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase)||file.Length is <5 or >5*1024*1024)return BadRequest();
        using var buffer=new MemoryStream();await file.CopyToAsync(buffer,ct);var bytes=buffer.ToArray();
        if(FilePreviewPolicy.ContentType(name,bytes)!="application/pdf")return BadRequest(new{error="請選擇有效的 PDF。"});
        var discussionTitle=DiscussionTitle(title,text);
        await using var cmd=Command(db,"INSERT INTO course_questions(id,course_id,author_id,title,question,attachment_name,attachment_bytes) VALUES($1,$2,$3,$4,$5,$6,$7) ON CONFLICT DO NOTHING",id,course,Actor,discussionTitle,text.Trim(),name,bytes);
        if(await cmd.ExecuteNonQueryAsync(ct)==0){
            await using var same=Command(db,"SELECT id FROM course_questions WHERE id=$1 AND course_id=$2 AND author_id=$3 AND title=$4 AND question=$5 AND attachment_name=$6 AND attachment_bytes=$7",id,course,Actor,discussionTitle,text.Trim(),name,bytes);
            if(await same.ExecuteScalarAsync(ct)==null)return Conflict();
        }
        return NoContent();
    },ct);
    [HttpGet("questions/{id:guid}/attachment")]
    public Task<IActionResult> Attachment(Guid course,Guid id,CancellationToken ct)=>Run(course,false,async(db,role,writable)=>{
        await using var cmd=Command(db,"SELECT attachment_name,attachment_bytes FROM course_questions WHERE id=$1 AND course_id=$2 AND attachment_name IS NOT NULL",id,course);
        await using var r=await cmd.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))return NotFound();
        Response.Headers["X-Content-Type-Options"]="nosniff";
        return File(r.GetFieldValue<byte[]>(1),"application/pdf",r.GetString(0));
    },ct);
    [HttpPut("questions/{id:guid}/vote")]
    public Task<IActionResult> Vote(Guid course,Guid id,[FromBody]bool voted,CancellationToken ct)=>Run(course,true,async(db,role,writable)=>{
        await using var q=Command(db,"SELECT id FROM course_questions WHERE course_id=$1 AND id=$2 FOR SHARE",course,id);
        if(await q.ExecuteScalarAsync(ct) is not Guid)return NotFound();
        await using var cmd=Command(db,voted?"INSERT INTO course_question_votes(question_id,user_id) VALUES($1,$2) ON CONFLICT DO NOTHING":"DELETE FROM course_question_votes WHERE question_id=$1 AND user_id=$2",id,Actor);
        await cmd.ExecuteNonQueryAsync(ct);return NoContent();
    },ct);
    [HttpGet("questions/{id:guid}/replies")]
    public Task<IActionResult> Replies(Guid course,Guid id,CancellationToken ct,[FromQuery,Range(1,100000)] int page=1,[FromQuery]bool all=false)=>Run(course,false,async(db,role,writable)=>{
        await using var q=Command(db,"SELECT id FROM course_questions WHERE id=$1 AND course_id=$2",id,course);
        if(await q.ExecuteScalarAsync(ct)==null)return NotFound();
        var limit=all?int.MaxValue:21;var offset=all?0:(page-1)*20;
        await using var cmd=Command(db,"""
            SELECT r.id,u.display_name,m.course_role,r.body,r.created_at,
                   (SELECT count(*) FROM course_question_reply_likes l WHERE l.reply_id=r.id),
                   EXISTS(SELECT 1 FROM course_question_reply_likes l WHERE l.reply_id=r.id AND l.user_id=$3)
            FROM course_question_replies r
            JOIN course_questions q ON q.id=r.question_id
            JOIN app_users u ON u.id=r.author_id
            LEFT JOIN course_members m ON m.course_id=q.course_id AND m.user_id=r.author_id
            WHERE r.question_id=$1 ORDER BY r.created_at,r.id LIMIT $4 OFFSET $2
            """,id,offset,Actor,limit);
        var rows=new List<object>();await using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))rows.Add(new{id=r.GetGuid(0),author=r.GetString(1),authorRole=r.IsDBNull(2)?null:r.GetString(2),body=r.GetString(3),createdAt=r.GetFieldValue<DateTimeOffset>(4),likes=r.GetInt64(5),liked=r.GetBoolean(6)});
        return Ok(new{items=all?rows:rows.Take(20),page,hasMore=!all&&rows.Count>20});
    },ct);
    [HttpPost("questions/{id:guid}/replies")]
    public Task<IActionResult> AddReply(Guid course,Guid id,TextInput input,CancellationToken ct)=>Run(course,true,async(db,role,writable)=>{
        if(input.Id==Guid.Empty||string.IsNullOrWhiteSpace(input.Text))return BadRequest();
        await using var q=Command(db,"SELECT locked FROM course_questions WHERE id=$1 AND course_id=$2 FOR UPDATE",id,course);
        if(await q.ExecuteScalarAsync(ct) is not bool locked)return NotFound();
        if(locked)return Conflict(new{error="此討論已鎖定。"});
        await using var cmd=Command(db,"INSERT INTO course_question_replies(id,question_id,author_id,body) VALUES($1,$2,$3,$4) ON CONFLICT DO NOTHING",input.Id,id,Actor,input.Text.Trim());
        if(await cmd.ExecuteNonQueryAsync(ct)==0){
            await using var same=Command(db,"SELECT id FROM course_question_replies WHERE id=$1 AND question_id=$2 AND author_id=$3 AND body=$4",input.Id,id,Actor,input.Text.Trim());
            if(await same.ExecuteScalarAsync(ct)==null)return Conflict();
        }
        return NoContent();
    },ct);
    [HttpPut("questions/{id:guid}/replies/{replyId:guid}/like")]
    public Task<IActionResult> LikeReply(Guid course,Guid id,Guid replyId,LikeInput input,CancellationToken ct)=>Run(course,true,async(db,role,writable)=>{
        await using var q=Command(db,"SELECT r.id FROM course_question_replies r JOIN course_questions q ON q.id=r.question_id WHERE q.course_id=$1 AND q.id=$2 AND r.id=$3 FOR SHARE OF r,q",course,id,replyId);
        if(await q.ExecuteScalarAsync(ct) is not Guid)return NotFound();
        await using var cmd=Command(db,input.Liked?"INSERT INTO course_question_reply_likes(reply_id,user_id) VALUES($1,$2) ON CONFLICT DO NOTHING":"DELETE FROM course_question_reply_likes WHERE reply_id=$1 AND user_id=$2",replyId,Actor);
        await cmd.ExecuteNonQueryAsync(ct);return NoContent();
    },ct);
    [HttpPut("questions/{id:guid}/lock")]
    public Task<IActionResult> Lock(Guid course,Guid id,[FromBody]bool locked,CancellationToken ct)=>Run(course,true,async(db,role,writable)=>{
        if(!AssignmentPolicy.IsStaff(role))return Forbid();
        await using var cmd=Command(db,"UPDATE course_questions SET locked=$3 WHERE course_id=$1 AND id=$2",course,id,locked);
        return await cmd.ExecuteNonQueryAsync(ct)==1?NoContent():NotFound();
    },ct);
    [HttpPut("questions/{id:guid}/response")]
    public Task<IActionResult> Reply(Guid course,Guid id,ReplyInput input,CancellationToken ct)=>Run(course,true,async(db,role,writable)=>
    {
        if(role is not ("teacher" or "ta"))return Forbid();
        if(string.IsNullOrWhiteSpace(input.Response))return BadRequest(new{error="請填寫答覆。"});
        await using var cmd=Command(db,"UPDATE course_questions SET response=$3,answered_by=$4,answered_at=now(),version=version+1 WHERE course_id=$1 AND id=$2 AND version=$5 AND NOT locked",course,id,input.Response.Trim(),Actor,input.ExpectedVersion);
        if(await cmd.ExecuteNonQueryAsync(ct)>0)return NoContent();
        await using var check=Command(db,"SELECT EXISTS(SELECT 1 FROM course_questions WHERE course_id=$1 AND id=$2)",course,id);
        return await check.ExecuteScalarAsync(ct) is true?Conflict(new{error="答覆已更新，請重新載入；尚未儲存的文字已保留。"}):NotFound();
    },ct);
}
