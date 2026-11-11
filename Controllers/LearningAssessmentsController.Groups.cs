using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AiTeachingAssistant.Controllers;

public sealed partial class LearningAssessmentsController
{
    private static NpgsqlCommand Query(NpgsqlConnection conn,string sql,params object[] args)
    {
        var cmd=new NpgsqlCommand(sql,conn);
        foreach(var arg in args)cmd.Parameters.AddWithValue(arg);
        return cmd;
    }

    private static async Task<JsonElement[]> Rows(NpgsqlCommand cmd,CancellationToken ct)
    {
        await using(cmd)
        {
            var rows=new List<JsonElement>();
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))rows.Add(JsonSerializer.Deserialize<JsonElement>(reader.GetString(0)));
            return rows.ToArray();
        }
    }

    private static async Task ConfigureGroups(NpgsqlConnection conn,Guid course,Guid report,ReportInput input,CancellationToken ct)
    {
        if(input.SubmissionMode is not ("individual" or "group") || input.EvaluationMode is not ("off" or "simple" or "consensus"))
            throw new AssignmentWorkflowException(400,"作業或評量模式無效。");
        var grouped=input.SubmissionMode=="group";
        var groups=grouped?input.GroupIds.Distinct().ToArray():Array.Empty<Guid>();
        if(grouped && groups.Length==0)throw new AssignmentWorkflowException(400,"請選擇作業小組。");
        if(input.Dimensions.Length>4 || input.Dimensions.Any(d=>string.IsNullOrWhiteSpace(d)||d.Length>40) ||
           input.Dimensions.Distinct().Count()!=input.Dimensions.Length || grouped && input.EvaluationMode!="off" && input.Dimensions.Length==0)
            throw new AssignmentWorkflowException(400,"請指定一至四個不同的評量面向。");
        await using var update=Query(conn,"UPDATE report_assignments SET submission_mode=$2,allow_delegate=$3,allow_late=$4,evaluation_mode=$5,dimensions=$6 WHERE id=$1",
            report,input.SubmissionMode,grouped && input.AllowDelegate,input.AllowLate,grouped?input.EvaluationMode:"off",input.Dimensions);
        await update.ExecuteNonQueryAsync(ct);
        await using var clear=Query(conn,"DELETE FROM report_groups WHERE report_id=$1",report);
        await clear.ExecuteNonQueryAsync(ct);
        await using var add=Query(conn,"INSERT INTO report_groups SELECT $1,course_id,id FROM course_groups WHERE course_id=$2 AND id=ANY($3) AND dissolved_at IS NULL AND EXISTS(SELECT 1 FROM course_group_members gm JOIN course_members cm ON cm.course_id=$2 AND cm.user_id=gm.user_id AND cm.course_role='student' AND cm.status='active' WHERE gm.group_id=course_groups.id AND gm.is_leader AND gm.left_at IS NULL)",report,course,groups);
        if(await add.ExecuteNonQueryAsync(ct)!=groups.Length)throw new AssignmentWorkflowException(400,"請選擇本課程內有有效組長的小組。");
    }

    private async Task StoreFile(NpgsqlConnection conn,Report report,string name,byte[] bytes,CancellationToken ct)
    {
        // ReadReport holds the assignment lock and a course share lock through commit.
        await using var existing=Query(conn,"""
            SELECT s.id,s.returned,COALESCE(m.is_leader,true)
            FROM report_submissions s LEFT JOIN report_snapshot m ON m.submission_id=s.id AND m.student_id=$2
            WHERE s.report_id=$1 AND NOT s.is_practice AND (m.student_id IS NOT NULL OR s.group_id IS NULL AND s.student_id=$2)
            """,report.Id,Actor);
        Guid? previous=null;
        await using(var r=await existing.ExecuteReaderAsync(ct))
        {
            if(await r.ReadAsync(ct))
            {
                if(!r.GetBoolean(1))throw new AssignmentWorkflowException(409,"已有正式繳交紀錄。");
                if(!r.GetBoolean(2)&&!report.AllowDelegate)throw new AssignmentWorkflowException(403,"限繳交時的組長重新繳交。");
                previous=r.GetGuid(0);
            }
        }
        if(previous.HasValue)
        {
            await using var archive=Query(conn,"INSERT INTO report_previous_files SELECT id,revision,student_id,submitted_at,file_name,content FROM report_submissions WHERE id=$1",previous.Value);
            await archive.ExecuteNonQueryAsync(ct);
            await using var replace=Query(conn,"UPDATE report_submissions SET student_id=$2,file_name=$3,content=$4,submitted_at=now(),revision=revision+1,version=version+1,returned=false WHERE id=$1",previous.Value,Actor,name,bytes);
            await replace.ExecuteNonQueryAsync(ct);return;
        }
        if(report.Status!="published")throw new AssignmentWorkflowException(409,"已關閉繳交。");
        if(!report.AllowLate && report.DueAt.HasValue && clock.GetUtcNow()>=report.DueAt)
            throw new AssignmentWorkflowException(409,"已超過 PDF 作業截止時間。");
        Guid? group=null;string groupName="";
        if(report.SubmissionMode=="group")
        {
            await using var eligible=Query(conn,"""
                SELECT g.id,g.name,m.is_leader FROM report_groups rg JOIN course_groups g ON g.id=rg.group_id
                JOIN course_group_members m ON m.group_id=g.id AND m.left_at IS NULL
                WHERE rg.report_id=$1 AND g.dissolved_at IS NULL AND m.user_id=$2
                """,report.Id,Actor);
            await using var reader=await eligible.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct)||!reader.GetBoolean(2)&&!report.AllowDelegate)
                throw new AssignmentWorkflowException(403,"限指定小組的組長或獲准代交的組員繳交。");
            group=reader.GetGuid(0);groupName=reader.GetString(1);
        }
        try
        {
            await using var save=Query(conn,"INSERT INTO report_submissions(report_id,student_id,file_name,content,group_id,group_name) VALUES($1,$2,$3,$4,NULLIF($5,$6),$7) RETURNING id",
                report.Id,Actor,name,bytes,group??Guid.Empty,Guid.Empty,groupName);
            var submission=(Guid)(await save.ExecuteScalarAsync(ct))!;
            if(group.HasValue)
            {
                await using var snapshot=Query(conn,"""
                    INSERT INTO report_snapshot(submission_id,report_id,student_id,display_name,is_leader)
                    SELECT $1,$2,m.user_id,u.display_name,m.is_leader FROM course_group_members m
                    JOIN course_members c ON c.course_id=$4 AND c.user_id=m.user_id AND c.status='active' AND c.course_role='student'
                    JOIN app_users u ON u.id=m.user_id AND u.status='active' WHERE m.group_id=$3 AND m.left_at IS NULL
                    """,submission,report.Id,group.Value,report.CourseId);
                await snapshot.ExecuteNonQueryAsync(ct);
            }
        }
        catch(PostgresException e)when(e.SqlState==PostgresErrorCodes.UniqueViolation)
        {throw new AssignmentWorkflowException(409,"本組或組員已有正式繳交紀錄，請重新載入。");}
    }

    private static Task<JsonElement[]> SubmissionRows(NpgsqlConnection conn,Guid id,Guid actor,bool staff,bool practice,CancellationToken ct)=>
        Rows(Query(conn,"""
            SELECT jsonb_build_object('id',s.id,'studentName',COALESCE(NULLIF(s.group_name,''),u.display_name),'studentNumber',CASE WHEN s.group_id IS NULL THEN u.student_number END,
              'submittedBy',u.display_name,'fileName',s.file_name,'submittedAt',s.submitted_at,'revision',s.revision,'returned',s.returned,
              'canResubmit',s.returned AND (s.group_id IS NULL OR own.is_leader OR a.allow_delegate),
              'score',CASE WHEN $3 THEN s.score WHEN s.group_id IS NULL THEN s.published_score ELSE own.published_score END,
              'publishedScore',CASE WHEN $3 OR s.group_id IS NULL THEN s.published_score ELSE own.published_score END,
              'feedback',CASE WHEN $3 THEN s.feedback ELSE COALESCE(s.published_feedback,'') END,'version',s.version,
              'members',COALESCE((SELECT jsonb_agg(jsonb_build_object('id',m.student_id,'name',m.display_name,'isLeader',m.is_leader,'self',m.student_id=$2)
                || CASE WHEN $3 THEN jsonb_build_object('score',m.override_score,'reason',m.reason,'publishedScore',m.published_score) ELSE '{}'::jsonb END ORDER BY m.display_name,m.student_id)
                FROM report_snapshot m WHERE m.submission_id=s.id),'[]'::jsonb),
              'history',COALESCE((SELECT jsonb_agg(jsonb_build_object('revision',p.revision,'submittedAt',p.submitted_at,'fileName',p.file_name,'submittedBy',v.display_name) ORDER BY p.revision)
                FROM report_previous_files p JOIN app_users v ON v.id=p.actor WHERE p.submission_id=s.id),'[]'::jsonb))
            FROM report_submissions s JOIN report_assignments a ON a.id=s.report_id JOIN app_users u ON u.id=s.student_id
            LEFT JOIN report_snapshot own ON own.submission_id=s.id AND own.student_id=$2
            WHERE s.report_id=$1 AND s.is_practice=$4 AND ($3 OR own.student_id IS NOT NULL OR s.student_id=$2) ORDER BY u.display_name
            """,id,actor,staff,practice),ct);

    [CourseCapability("grading"),HttpPost("api/reports/{id:guid}/submissions/{submission:guid}/return")]
    public Task<IActionResult> ReturnFile(Guid id,Guid submission,[FromQuery]int version,CancellationToken ct)=>Run(async()=>{
        await using var conn=await source.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
        var report=await ReadReport(conn,id,true,ct);AssignmentPolicy.RequireStaff(report.CourseRole);
        await using var cmd=Query(conn,"UPDATE report_submissions SET returned=true,version=version+1 WHERE report_id=$1 AND id=$2 AND version=$3 AND NOT returned AND NOT is_practice AND published_score IS NULL",id,submission,version);
        if(await cmd.ExecuteNonQueryAsync(ct)!=1)return Conflict(new{error="請確認尚未發布成績且版本正確。"});
        await StudioWorkflow.Audit(conn,Actor,submission,"report.returned",ct);await tx.CommitAsync(ct);return NoContent();
    });

    private async Task GradeMembers(NpgsqlConnection conn,Report report,Guid submission,ReportGradeInput input,CancellationToken ct)
    {
        await using var members=Query(conn,"SELECT student_id FROM report_snapshot WHERE submission_id=$1 ORDER BY student_id",submission);
        var ids=new List<Guid>();await using(var reader=await members.ExecuteReaderAsync(ct)){while(await reader.ReadAsync(ct))ids.Add(reader.GetGuid(0));}
        if(ids.Count==0 || input.Adjustments.Any(a=>!ids.Contains(a.StudentId)) || input.Adjustments.DistinctBy(a=>a.StudentId).Count()!=input.Adjustments.Length)
            throw new AssignmentWorkflowException(400,"個別成績必須屬於繳交時的組員。");
        foreach(var member in ids)
        {
            var adjustment=input.Adjustments.SingleOrDefault(a=>a.StudentId==member);
            if(adjustment!=null)
            {
                if(adjustment.Score.HasValue){
                    GradebookPolicy.ValidateScore(adjustment.Score,report.MaxScore);
                    if(string.IsNullOrWhiteSpace(adjustment.Reason))throw new AssignmentWorkflowException(400,"個別調整必須填寫原因。");
                }
                if(!adjustment.Score.HasValue && string.IsNullOrWhiteSpace(adjustment.Reason)){
                    await using var prior=Query(conn,"SELECT override_score IS NOT NULL FROM report_snapshot WHERE submission_id=$1 AND student_id=$2",submission,member);
                    if(await prior.ExecuteScalarAsync(ct) is true)throw new AssignmentWorkflowException(400,"取消個別調整也必須填寫原因。");
                }
                await using var save=Query(conn,"UPDATE report_snapshot SET override_score=NULLIF($3,-1),reason=$4 WHERE submission_id=$1 AND student_id=$2",submission,member,adjustment.Score??-1,adjustment.Reason??"");
                await save.ExecuteNonQueryAsync(ct);
            }
            if(input.Publish){
                await using var publish=Query(conn,"UPDATE report_snapshot SET published_score=COALESCE(override_score,$3) WHERE submission_id=$1 AND student_id=$2 RETURNING published_score",submission,member,input.Score!.Value);
                var score=(decimal)(await publish.ExecuteScalarAsync(ct))!;
                GradebookPolicy.ValidateScore(score,report.MaxScore);
                await AssessmentGradeSync.SetScore(conn,report.ItemId,report.CourseId,member,score,Actor,ct);
            }
        }

    }
    public sealed record ReviewInput(int[] Ratings,[StringLength(500)]string Comment="");
    public sealed record VoteInput(string Choice,[StringLength(500)]string Explanation="");

    private async Task<IActionResult> Respond(Guid id,Guid submission,Guid? subject,ReviewInput? review,VoteInput? vote,CancellationToken ct)
    {
        await using var conn=await source.OpenConnectionAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(ct);
        var report=await ReadReport(conn,id,true,ct);
        if(report.CourseRole!="student" || experience.IsPractice || report.EvaluationMode=="off" || vote!=null && report.EvaluationMode!="consensus")return Forbid();
        await using var member=Query(conn,"SELECT EXISTS(SELECT 1 FROM report_snapshot WHERE report_id=$1 AND submission_id=$2 AND student_id=$3)",id,submission,Actor);
        if(await member.ExecuteScalarAsync(ct) is not true)return NotFound();
        if(review!=null)
        {
            if(subject==Actor || review.Ratings.Length!=report.Dimensions!.Length || review.Ratings.Any(n=>n<1||n>5))return BadRequest(new{error="請評估其他組員，各面向為 1 至 5 分。"});
            await using var save=Query(conn,"""
                INSERT INTO report_peer_reviews SELECT $1,$2,m.student_id,$4,$5 FROM report_snapshot m WHERE m.submission_id=$1 AND m.student_id=$3
                ON CONFLICT(submission_id,reviewer,subject) DO UPDATE SET ratings=EXCLUDED.ratings,comment=EXCLUDED.comment
                """,submission,Actor,subject!.Value,review.Ratings,review.Comment);
            if(await save.ExecuteNonQueryAsync(ct)!=1)return NotFound();
        }
        else
        {
            if(vote!.Choice is not ("agree" or "review" or "pending"))return BadRequest();
            await using var save=Query(conn,"INSERT INTO report_votes VALUES($1,$2,$3,$4) ON CONFLICT(submission_id,voter) DO UPDATE SET choice=EXCLUDED.choice,explanation=EXCLUDED.explanation",submission,Actor,vote.Choice,vote.Explanation);
            await save.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);return NoContent();
    }
    [HttpPut("api/reports/{id:guid}/submissions/{submission:guid}/reviews/{subject:guid}")]
    public Task<IActionResult> Review(Guid id,Guid submission,Guid subject,ReviewInput input,CancellationToken ct)=>Run(()=>Respond(id,submission,subject,input,null,ct));
    [HttpPut("api/reports/{id:guid}/submissions/{submission:guid}/vote")]
    public Task<IActionResult> Vote(Guid id,Guid submission,VoteInput input,CancellationToken ct)=>Run(()=>Respond(id,submission,null,null,input,ct));

    [CourseCapability("grading"),HttpGet("api/reports/{id:guid}/submissions/{submission:guid}/evaluation")]
    public Task<IActionResult> Evaluation(Guid id,Guid submission,CancellationToken ct)=>Run(async()=>{
        await using var conn=await source.OpenConnectionAsync(ct);
        var report=await ReadReport(conn,id,false,ct);
        var staff=StudioWorkflow.Staff(report.CourseRole);
        if(report.EvaluationMode=="off" || experience.IsPractice)return NotFound();
        await using var access=Query(conn,"SELECT EXISTS(SELECT 1 FROM report_snapshot WHERE submission_id=$1 AND report_id=$2 AND ($3 OR student_id=$4))",submission,id,staff,Actor);
        if(await access.ExecuteScalarAsync(ct) is not true)return NotFound();
        var reviews=await Rows(Query(conn,"SELECT jsonb_build_object('reviewer',reviewer,'subject',subject,'ratings',ratings,'comment',comment) FROM report_peer_reviews WHERE submission_id=$1 AND ($2 OR reviewer=$3)",submission,staff,Actor),ct);
        var votes=await Rows(Query(conn,"SELECT jsonb_build_object('voter',voter,'choice',choice,'explanation',explanation) FROM report_votes WHERE submission_id=$1 AND ($2 OR voter=$3)",submission,staff,Actor),ct);
        return Ok(new{reviews,votes});
    });

    [HttpGet("api/reports/{id:guid}/roster")]
    public Task<IActionResult> Roster(Guid id,CancellationToken ct)=>Run(async()=>{
        await using var conn=await source.OpenConnectionAsync(ct);
        var report=await ReadReport(conn,id,false,ct);var staff=StudioWorkflow.Staff(report.CourseRole);
        if(report.SubmissionMode=="individual" && !staff)return Ok(Array.Empty<object>());
        var rows= report.SubmissionMode=="group"
          ? Query(conn,"""
              SELECT jsonb_build_object('name',COALESCE(s.group_name,g.name),'submitted',s.id IS NOT NULL,'late',s.submitted_at>a.due_at,
               'canSubmit',s.id IS NULL AND g.dissolved_at IS NULL AND (m.is_leader OR a.allow_delegate),'dissolved',g.dissolved_at IS NOT NULL)
              FROM report_groups rg JOIN report_assignments a ON a.id=rg.report_id JOIN course_groups g ON g.id=rg.group_id
              LEFT JOIN course_group_members m ON m.group_id=g.id AND m.left_at IS NULL AND m.user_id=$2
              LEFT JOIN report_submissions s ON s.group_id=g.id AND s.report_id=rg.report_id
              WHERE rg.report_id=$1 AND ($3 OR s.id IS NULL AND m.user_id IS NOT NULL OR EXISTS(SELECT 1 FROM report_snapshot x WHERE x.submission_id=s.id AND x.student_id=$2)) ORDER BY g.name
              """,id,Actor,staff)
          : Query(conn,"""
              SELECT jsonb_build_object('name',u.display_name,'submitted',s.id IS NOT NULL,'late',s.submitted_at>a.due_at)
              FROM report_assignments a JOIN course_members m ON m.course_id=a.course_id AND m.course_role='student' AND m.status='active'
              JOIN app_users u ON u.id=m.user_id LEFT JOIN report_submissions s ON s.report_id=a.id AND s.student_id=u.id AND NOT s.is_practice
              WHERE a.id=$1 ORDER BY u.student_number,u.display_name
              """,id);
        return Ok(await Rows(rows,ct));
    });
}
