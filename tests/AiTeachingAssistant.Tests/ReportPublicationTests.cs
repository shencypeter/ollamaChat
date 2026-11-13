using System.Security.Claims;
using System.Text.Json;
using AiTeachingAssistant.Controllers;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AiTeachingAssistant.Tests;

public class ReportPublicationTests:IAsyncLifetime
{
    private NpgsqlDataSource db=null!;
    private readonly Guid teacher=Guid.NewGuid(),student=Guid.NewGuid(),peer=Guid.NewGuid(),third=Guid.NewGuid(),outsider=Guid.NewGuid(),course=Guid.NewGuid(),report=Guid.NewGuid(),item=Guid.NewGuid(),group=Guid.NewGuid(),otherGroup=Guid.NewGuid(),first=Guid.NewGuid(),second=Guid.NewGuid();
    private static bool Enabled=>!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION"));
    private async Task<object?> Sql(string sql,params object[] args){await using var cmd=db.CreateCommand(sql);foreach(var arg in args)cmd.Parameters.AddWithValue(arg);return await cmd.ExecuteScalarAsync();}
    public async Task InitializeAsync()
    {
        if(!Enabled)return;
        db=NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")!);
        foreach(var id in new[]{teacher,student,peer,third,outsider})await Sql("INSERT INTO app_users(id,display_name) VALUES($1,'合成發布測試')",id);
        await Sql("INSERT INTO courses(id,code,title,term_code,owner_user_id,status) VALUES($1,$2,'合成 PDF 發布課程','115-1',$3,'active')",course,"PDF-"+course.ToString("N"),teacher);
        foreach(var (id,role) in new[]{(teacher,"teacher"),(student,"student"),(peer,"student"),(third,"student")})await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,$3)",course,id,role);
        foreach(var id in new[]{group,otherGroup})await Sql("INSERT INTO course_groups(id,course_id,name) VALUES($1,$2,$3)",id,course,id.ToString("N"));
        foreach(var (id,g,leader) in new[]{(student,group,true),(peer,group,false),(third,otherGroup,true)})await Sql("INSERT INTO course_group_members(group_id,user_id,is_leader) VALUES($1,$2,$3)",g,id,leader);
        await Sql("INSERT INTO report_assignments(id,course_id,title,status,created_by,submission_mode,max_score) VALUES($1,$2,'合成 PDF 作業','published',$3,'group',100)",report,course,teacher);
        await Sql("INSERT INTO course_grade_items(id,course_id,title,category,max_score,weight,created_by,source_report_id) VALUES($1,$2,'合成 PDF','assignment',100,1,$3,$4)",item,course,teacher,report);
        foreach(var id in new[]{group,otherGroup})await Sql("INSERT INTO report_groups(report_id,course_id,group_id) VALUES($1,$2,$3)",report,course,id);
        await AddSubmission(first,student,group,0);
        await Sql("INSERT INTO report_snapshot(submission_id,report_id,student_id,display_name,is_leader,override_score,reason) VALUES($1,$2,$3,'合成組長',true,NULL,''),($1,$2,$4,'合成組員',false,90,'合成調整原因')",first,report,student,peer);
    }
    private async Task AddSubmission(Guid id,Guid user,Guid g,decimal score)=>await Sql("INSERT INTO report_submissions(id,report_id,student_id,file_name,content,group_id,group_name,score) VALUES($1,$2,$3,'synthetic.pdf',$4,$5,'合成小組',$6)",id,report,user,"%PDF-1.4 synthetic"u8.ToArray(),g,score);
    private async Task Complete()
    {
        await AddSubmission(second,third,otherGroup,70);
        await Sql("INSERT INTO report_snapshot(submission_id,report_id,student_id,display_name,is_leader) VALUES($1,$2,$3,'合成第二組長',true)",second,report,third);
    }
    private LearningAssessmentsController Controller(Guid actor,bool practice=false)
    {
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString()),new Claim("can_teach",bool.TrueString)],"test"))};
        if(practice)context.Request.Headers[LearnerExperience.Header]="student";
        var experience=new LearnerExperience(new HttpContextAccessor{HttpContext=context}){CourseRole=actor==teacher?"teacher":"student"};
        return new(db,new StudioWorkflow(db,experience),experience,TimeProvider.System){ControllerContext=new(){HttpContext=context}};
    }
    private LearningAssessmentsController.ReportPublicationInput Input(params (Guid Id,int Version)[] versions)=>new(){ExpectedVersion=1,Submissions=versions.Select(row=>new LearningAssessmentsController.ReportSubmissionVersion(row.Id,row.Version)).ToArray()};
    private static JsonElement Json(IActionResult result)=>JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result).Value);

    [FormativeDatabaseFact]
    public async Task CompleteGroupPublicationIsAtomicAndPreservesZeroAndIndividualOverrides()
    {
        Assert.IsType<ConflictObjectResult>(await Controller(teacher).Grade(report,first,new(){Score=50,ExpectedVersion=1,Publish=true},default));
        Assert.IsType<ConflictObjectResult>(await Controller(teacher).PublishReportGrades(report,Input((first,1)),default));
        Assert.Equal(0L,await Sql("SELECT count(*) FROM report_submissions WHERE report_id=$1 AND published_score IS NOT NULL",report));
        await Complete();
        Assert.IsType<NoContentResult>(await Controller(teacher).PublishReportGrades(report,Input((first,1),(second,1)),default));
        Assert.Equal(3L,await Sql("SELECT count(*) FROM course_grade_scores WHERE item_id=$1 AND published_score IS NOT NULL",item));
        Assert.Equal(0m,await Sql("SELECT published_score FROM course_grade_scores WHERE item_id=$1 AND student_id=$2",item,student));
        Assert.Equal(90m,await Sql("SELECT published_score FROM course_grade_scores WHERE item_id=$1 AND student_id=$2",item,peer));
        Assert.Equal(70m,await Sql("SELECT published_score FROM course_grade_scores WHERE item_id=$1 AND student_id=$2",item,third));
        Assert.Equal(2L,await Sql("SELECT count(*) FROM report_grade_events e JOIN report_submissions s ON s.id=e.submission_id WHERE s.report_id=$1 AND e.details->>'batchRelease'='true'",report));
        Assert.IsType<ConflictObjectResult>(await Controller(teacher).PublishReportGrades(report,Input((first,1),(second,1)),default));
    }

    [FormativeDatabaseFact]
    public async Task InvalidMemberFinalScoreRollsBackSubmissionsAndGradebookTogether()
    {
        await Complete();
        await Sql("UPDATE report_snapshot SET override_score=101 WHERE submission_id=$1 AND student_id=$2",first,peer);
        var result=await Controller(teacher).PublishReportGrades(report,Input((first,1),(second,1)),default);
        Assert.Equal(400,Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(0L,await Sql("SELECT count(*) FROM report_submissions WHERE report_id=$1 AND (published_score IS NOT NULL OR version<>1)",report));
        Assert.Equal(0L,await Sql("SELECT count(*) FROM course_grade_scores WHERE item_id=$1",item));
        Assert.Equal(0L,await Sql("SELECT count(*) FROM report_grade_events e JOIN report_submissions s ON s.id=e.submission_id WHERE s.report_id=$1",report));
    }

    [FormativeDatabaseFact]
    public async Task StaleMissingDuplicateOrReturnedSubmissionsNeverPublishAPartialAssignment()
    {
        await Complete();
        foreach(var input in new[]{Input((first,2),(second,1)),Input((first,1)),Input((first,1),(first,1)),Input((first,1),(Guid.NewGuid(),1)),new LearningAssessmentsController.ReportPublicationInput{ExpectedVersion=1,Submissions=[null!,null!]}})
            Assert.IsType<ConflictObjectResult>(await Controller(teacher).PublishReportGrades(report,input,default));
        var changed=Input((first,1),(second,1));changed.ExpectedVersion=2;
        Assert.IsType<ConflictObjectResult>(await Controller(teacher).PublishReportGrades(report,changed,default));
        await Sql("UPDATE report_submissions SET returned=true WHERE id=$1",second);
        Assert.IsType<ConflictObjectResult>(await Controller(teacher).PublishReportGrades(report,Input((first,1),(second,1)),default));
        await Sql("UPDATE report_submissions SET returned=false,score=NULL WHERE id=$1",second);
        Assert.IsType<ConflictObjectResult>(await Controller(teacher).PublishReportGrades(report,Input((first,1),(second,1)),default));
        Assert.Equal(0L,await Sql("SELECT count(*) FROM report_submissions WHERE report_id=$1 AND published_score IS NOT NULL",report));
        Assert.Equal(0L,await Sql("SELECT count(*) FROM course_grade_scores WHERE item_id=$1",item));
    }

    [FormativeDatabaseFact]
    public async Task IndividualPublicationRequiresEveryActiveStudentAndSingleSubmissionPublicationIsRejected()
    {
        await Sql("DELETE FROM report_snapshot WHERE report_id=$1",report);
        await Sql("UPDATE report_submissions SET group_id=NULL,group_name=NULL WHERE report_id=$1",report);
        await Sql("DELETE FROM report_groups WHERE report_id=$1",report);
        await Sql("UPDATE report_assignments SET submission_mode='individual' WHERE id=$1",report);
        Assert.IsType<ConflictObjectResult>(await Controller(teacher).PublishReportGrades(report,Input((first,1)),default));
        var ids=new[]{first,Guid.NewGuid(),second};
        foreach(var (id,user) in new[]{(ids[1],peer),(ids[2],third)})await Sql("INSERT INTO report_submissions(id,report_id,student_id,file_name,content,score) VALUES($1,$2,$3,'synthetic.pdf',$4,80)",id,report,user,"%PDF-1.4"u8.ToArray());
        Assert.IsType<ConflictObjectResult>(await Controller(teacher).Grade(report,first,new(){Score=50,ExpectedVersion=1,Publish=true},default));
        Assert.IsType<NoContentResult>(await Controller(teacher).PublishReportGrades(report,Input(ids.Select(id=>(id,1)).ToArray()),default));
        Assert.Equal(3L,await Sql("SELECT count(*) FROM course_grade_scores WHERE item_id=$1 AND published_score IS NOT NULL",item));
    }

    [FormativeDatabaseFact]
    public async Task RosterIdentifiersAreStaffOnlyAndPublicationRequiresStaffAndWritableCourse()
    {
        await Complete();
        var staff=Json(await Controller(teacher).Roster(report,default));
        Assert.Equal(2,staff.GetArrayLength());Assert.All(staff.EnumerateArray(),row=>Assert.True(row.TryGetProperty("groupId",out _)));
        var own=Json(await Controller(student).Roster(report,default));Assert.Single(own.EnumerateArray());
        Assert.False(own[0].TryGetProperty("groupId",out _));Assert.False(own[0].TryGetProperty("submissionId",out _));
        foreach(var actor in new[]{student,outsider})Assert.Equal(403,Assert.IsType<ObjectResult>(await Controller(actor).PublishReportGrades(report,Input((first,1),(second,1)),default)).StatusCode);
        Assert.IsType<ForbidResult>(await Controller(teacher,true).PublishReportGrades(report,Input((first,1),(second,1)),default));
        await Sql("UPDATE courses SET status='archived' WHERE id=$1",course);
        Assert.Equal(409,Assert.IsType<ObjectResult>(await Controller(teacher).PublishReportGrades(report,Input((first,1),(second,1)),default)).StatusCode);
    }
    public async Task DisposeAsync()
    {
        if(!Enabled)return;
        await Sql("DELETE FROM audit_events WHERE actor_user_id=$1",teacher);
        await Sql("DELETE FROM report_grade_events WHERE submission_id IN (SELECT id FROM report_submissions WHERE report_id=$1)",report);
        await Sql("DELETE FROM course_grade_scores WHERE item_id=$1",item);
        await Sql("DELETE FROM report_submissions WHERE report_id=$1",report);
        await Sql("DELETE FROM course_grade_items WHERE id=$1",item);
        await Sql("DELETE FROM report_assignments WHERE id=$1",report);
        await Sql("DELETE FROM course_group_members WHERE group_id=ANY($1)",new[]{group,otherGroup});
        await Sql("DELETE FROM course_groups WHERE course_id=$1",course);
        await Sql("DELETE FROM course_members WHERE course_id=$1",course);
        await Sql("DELETE FROM courses WHERE id=$1",course);
        await Sql("DELETE FROM app_users WHERE id=ANY($1)",new[]{teacher,student,peer,third,outsider});
        await db.DisposeAsync();
    }
}
