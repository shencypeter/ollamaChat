using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiTeachingAssistant.Controllers;

public sealed partial class LearningAssessmentsController
{
    public sealed record ReportSubmissionVersion(Guid Id,int ExpectedVersion);
    public sealed class ReportPublicationInput
    {
        public int ExpectedVersion {get;set;}
        public ReportSubmissionVersion[] Submissions {get;set;}=[];
    }

    [CourseCapability("grading"),HttpPost("api/reports/{id:guid}/grades/publish")]
    public Task<IActionResult> PublishReportGrades(Guid id,ReportPublicationInput input,CancellationToken ct)=>Run(async()=>{
        if(experience.IsPractice)return Forbid();
        await using var conn=await source.OpenConnectionAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(ct);
        // All report submit/return/grade/settings actions share this assignment lock.
        var report=await ReadReport(conn,id,true,ct);
        AssignmentPolicy.RequireStaff(report.CourseRole);
        if(report.Status=="draft" || report.Version!=input.ExpectedVersion)
            return Conflict(new{error="作業狀態或版本已變更，請重新載入後再發布。"});

        var audience=new List<Guid>();
        var grouped=report.SubmissionMode=="group";
        if(grouped)audience.AddRange(report.GroupIds??[]);
        else
        {
            await using var roster=Query(conn,"SELECT m.user_id FROM course_members m JOIN app_users u ON u.id=m.user_id AND u.status='active' WHERE m.course_id=$1 AND m.course_role='student' AND m.status='active' ORDER BY m.user_id FOR SHARE OF m,u",report.CourseId);
            await using var reader=await roster.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))audience.Add(reader.GetGuid(0));
        }

        var rows=new List<(Guid Id,Guid Audience,decimal? Score,int Version,bool Returned,bool HasMembers)>();
        await using(var submissions=Query(conn,"""
            SELECT s.id,CASE WHEN $2 THEN s.group_id ELSE s.student_id END,s.score,s.version,s.returned,
                   EXISTS(SELECT 1 FROM report_snapshot m WHERE m.submission_id=s.id)
            FROM report_submissions s WHERE s.report_id=$1 AND NOT s.is_practice
              AND CASE WHEN $2 THEN s.group_id ELSE s.student_id END=ANY($3)
            ORDER BY s.id FOR UPDATE OF s
            """,id,grouped,audience.ToArray()))
        await using(var reader=await submissions.ExecuteReaderAsync(ct))
            while(await reader.ReadAsync(ct))rows.Add((reader.GetGuid(0),reader.GetGuid(1),reader.IsDBNull(2)?null:reader.GetDecimal(2),reader.GetInt32(3),reader.GetBoolean(4),reader.GetBoolean(5)));

        if(audience.Count==0 || rows.Count!=audience.Count || rows.Select(row=>row.Audience).Distinct().Count()!=audience.Count ||
           rows.Any(row=>row.Returned || !row.Score.HasValue || grouped&&!row.HasMembers))
            return Conflict(new{error="尚有未繳交、退回或未評分的對象，必須完成本作業全班評分才能一次發布。"});
        if(input.Submissions is null || input.Submissions.Any(row=>row is null) || input.Submissions.Length!=rows.Count || input.Submissions.DistinctBy(row=>row.Id).Count()!=rows.Count ||
           rows.Any(row=>!input.Submissions.Any(expected=>expected.Id==row.Id&&expected.ExpectedVersion==row.Version)))
            return Conflict(new{error="繳交或評分版本已變更，請重新載入並確認全班成績後再發布。"});

        foreach(var row in rows)
        {
            GradebookPolicy.ValidateScore(row.Score,report.MaxScore);
            await using var publish=Query(conn,"UPDATE report_submissions SET published_score=score,published_feedback=feedback,version=version+1 WHERE id=$1",row.Id);
            await publish.ExecuteNonQueryAsync(ct);
            if(grouped)await GradeMembers(conn,report,row.Id,new(){Publish=true,Score=row.Score},ct);
            else await AssessmentGradeSync.SetScore(conn,report.ItemId,report.CourseId,row.Audience,row.Score!.Value,Actor,ct);
            await using var audit=Query(conn,"INSERT INTO report_grade_events(submission_id,actor,details) VALUES($1,$2,jsonb_build_object('batchRelease',true,'published',true,'score',$3,'assignmentVersion',$4))",row.Id,Actor,row.Score!.Value,report.Version);
            await audit.ExecuteNonQueryAsync(ct);
        }
        await StudioWorkflow.Audit(conn,Actor,id,"report.grades_batch_published",ct);
        await tx.CommitAsync(ct);
        return NoContent();
    });
}
