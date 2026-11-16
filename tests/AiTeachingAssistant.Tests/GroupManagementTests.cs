using System.Reflection;
using System.Security.Claims;
using AiTeachingAssistant.Controllers;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AiTeachingAssistant.Tests;

public class GroupManagementTests:IAsyncLifetime
{
    private NpgsqlDataSource db=null!;
    private readonly Guid teacher=Guid.NewGuid(),ta=Guid.NewGuid(),student=Guid.NewGuid(),peer=Guid.NewGuid(),available=Guid.NewGuid(),occupied=Guid.NewGuid(),outsider=Guid.NewGuid(),course=Guid.NewGuid(),group=Guid.NewGuid(),otherGroup=Guid.NewGuid(),report=Guid.NewGuid(),submission=Guid.NewGuid(),item=Guid.NewGuid();
    private static bool Enabled=>!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION"));
    private async Task<object?> Sql(string sql,params object[] args){await using var cmd=db.CreateCommand(sql);foreach(var arg in args)cmd.Parameters.AddWithValue(arg);return await cmd.ExecuteScalarAsync();}
    public async Task InitializeAsync()
    {
        if(!Enabled)return;
        db=NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")!);
        foreach(var id in new[]{teacher,ta,student,peer,available,occupied,outsider})await Sql("INSERT INTO app_users(id,display_name) VALUES($1,'合成分組測試')",id);
        await Sql("INSERT INTO courses(id,code,title,term_code,owner_user_id,status) VALUES($1,$2,'合成分組課程','115-1',$3,'active')",course,"GROUP-"+course.ToString("N"),teacher);
        foreach(var (id,role) in new[]{(teacher,"teacher"),(ta,"ta"),(student,"student"),(peer,"student"),(available,"student"),(occupied,"student")})await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,$3)",course,id,role);
        await Sql("INSERT INTO course_groups(id,course_id,name) VALUES($1,$3,'第一組'),($2,$3,'第二組')",group,otherGroup,course);
        await Sql("INSERT INTO course_group_members(group_id,user_id,is_leader) VALUES($1,$2,true),($1,$3,false),($4,$5,true)",group,student,peer,otherGroup,occupied);
    }
    private CourseToolsController Controller(Guid actor,bool practice=false)
    {
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString()),new Claim("can_teach",bool.TrueString)],"test"))};
        if(practice)context.Request.Headers[LearnerExperience.Header]="student";
        var experience=new LearnerExperience(new HttpContextAccessor{HttpContext=context}){CourseRole=actor==teacher?"teacher":actor==ta?"ta":"student"};
        return new(db,experience){ControllerContext=new(){HttpContext=context}};
    }
    private Task<IActionResult> Save(string name,Guid? leader,params Guid[] members)=>Controller(teacher).SaveGroup(course,new(){Name=name,LeaderId=leader,Members=[..members]},default,group);

    [Fact]
    public void GroupWritesRetainAssessmentCapabilityChecks()
    {
        foreach(var name in new[]{nameof(CourseToolsController.SaveGroup),nameof(CourseToolsController.Dissolve)})
            Assert.Equal("assessments",typeof(CourseToolsController).GetMethod(name)!.GetCustomAttribute<CourseCapabilityAttribute>()!.Capability);
    }

    [FormativeDatabaseFact]
    public async Task RenameAndLeaderReplacementRetainMembershipRowsInEitherRequestOrder()
    {
        var original=await Sql("SELECT string_agg(user_id::text||joined_at::text,',' ORDER BY user_id) FROM course_group_members WHERE group_id=$1 AND left_at IS NULL",group);
        Assert.IsType<OkObjectResult>(await Save("改名小組",peer,peer,student));
        Assert.Equal(peer,await Sql("SELECT user_id FROM course_group_members WHERE group_id=$1 AND left_at IS NULL AND is_leader",group));
        Assert.IsType<OkObjectResult>(await Save("改名小組",student,peer,student));
        Assert.Equal(student,await Sql("SELECT user_id FROM course_group_members WHERE group_id=$1 AND left_at IS NULL AND is_leader",group));
        Assert.Equal(original,await Sql("SELECT string_agg(user_id::text||joined_at::text,',' ORDER BY user_id) FROM course_group_members WHERE group_id=$1 AND left_at IS NULL",group));
        Assert.Equal(2L,await Sql("SELECT count(*) FROM course_group_members WHERE group_id=$1",group));
    }

    [FormativeDatabaseFact]
    public async Task ClearLeaderRemoveMembersAndAddMembersWithoutDissolving()
    {
        Assert.IsType<OkObjectResult>(await Save("第一組",null,student,peer));
        Assert.Equal(0L,await Sql("SELECT count(*) FROM course_group_members WHERE group_id=$1 AND left_at IS NULL AND is_leader",group));
        Assert.IsType<OkObjectResult>(await Save("第一組",available,peer,available));
        Assert.Equal(available,await Sql("SELECT user_id FROM course_group_members WHERE group_id=$1 AND left_at IS NULL AND is_leader",group));
        Assert.Equal(1L,await Sql("SELECT count(*) FROM course_group_members WHERE group_id=$1 AND user_id=$2 AND left_at IS NOT NULL",group,student));
        Assert.IsType<OkObjectResult>(await Save("第一組",null));
        Assert.Equal(0L,await Sql("SELECT count(*) FROM course_group_members WHERE group_id=$1 AND left_at IS NULL",group));
        Assert.Equal(1L,await Sql("SELECT count(*) FROM course_groups WHERE id=$1 AND dissolved_at IS NULL",group));
    }

    [FormativeDatabaseFact]
    public async Task InvalidLeaderOccupiedStudentAndDuplicateMembersLeaveGroupUnchanged()
    {
        foreach(var request in new[]{new CourseToolsController.GroupInput{Name="錯誤",LeaderId=available,Members=[student,peer]},new(){Name="錯誤",LeaderId=student,Members=[student,occupied]},new(){Name="錯誤",Members=[student,student]},new(){Name="錯誤",Members=[outsider]}})
            Assert.IsType<BadRequestObjectResult>(await Controller(teacher).SaveGroup(course,request,default,group));
        Assert.Equal("第一組",await Sql("SELECT name FROM course_groups WHERE id=$1",group));
        Assert.Equal(2L,await Sql("SELECT count(*) FROM course_group_members WHERE group_id=$1 AND left_at IS NULL",group));
        Assert.Equal(student,await Sql("SELECT user_id FROM course_group_members WHERE group_id=$1 AND is_leader AND left_at IS NULL",group));
    }

    [FormativeDatabaseFact]
    public async Task DuplicateNameRollsBackAndDissolutionReleasesMembers()
    {
        Assert.IsType<ConflictObjectResult>(await Save("第二組",peer,peer));
        Assert.Equal(2L,await Sql("SELECT count(*) FROM course_group_members WHERE group_id=$1 AND left_at IS NULL",group));
        Assert.IsType<NoContentResult>(await Controller(teacher).Dissolve(course,otherGroup,default));
        Assert.IsType<OkObjectResult>(await Save("第一組",occupied,student,occupied));
        Assert.Equal(occupied,await Sql("SELECT user_id FROM course_group_members WHERE group_id=$1 AND is_leader AND left_at IS NULL",group));
    }

    [FormativeDatabaseFact]
    public async Task GroupChangesDoNotAlterSubmittedSnapshotOrPublishedGrades()
    {
        await Sql("INSERT INTO report_assignments(id,course_id,title,status,created_by,submission_mode,max_score) VALUES($1,$2,'合成作業','published',$3,'group',100)",report,course,teacher);
        await Sql("INSERT INTO course_grade_items(id,course_id,title,category,max_score,weight,created_by,source_report_id) VALUES($1,$2,'合成作業','assignment',100,1,$3,$4)",item,course,teacher,report);
        await Sql("INSERT INTO report_groups(report_id,course_id,group_id) VALUES($1,$2,$3)",report,course,group);
        await Sql("INSERT INTO report_submissions(id,report_id,student_id,file_name,content,group_id,group_name,score,published_score) VALUES($1,$2,$3,'synthetic.pdf',$4,$5,'第一組',88,88)",submission,report,student,"%PDF-1.4 synthetic"u8.ToArray(),group);
        await Sql("INSERT INTO report_snapshot(submission_id,report_id,student_id,display_name,is_leader,published_score) VALUES($1,$2,$3,'合成組長',true,88),($1,$2,$4,'合成組員',false,88)",submission,report,student,peer);
        await Sql("INSERT INTO course_grade_scores(item_id,course_id,student_id,score,published_score,updated_by) VALUES($1,$2,$3,88,88,$4)",item,course,student,teacher);
        const string hashSql="SELECT md5((SELECT jsonb_agg(to_jsonb(s) ORDER BY student_id)::text FROM report_snapshot s WHERE submission_id=$1)|| (SELECT to_jsonb(r)::text FROM report_submissions r WHERE id=$1)|| (SELECT jsonb_agg(to_jsonb(g))::text FROM course_grade_scores g WHERE item_id=$2))";
        var before=await Sql(hashSql,submission,item);
        Assert.IsType<OkObjectResult>(await Save("改名後",peer,peer,available));
        Assert.Equal(before,await Sql(hashSql,submission,item));
        Assert.IsType<NoContentResult>(await Controller(teacher).Dissolve(course,group,default));
        Assert.Equal(before,await Sql(hashSql,submission,item));
    }

    [FormativeDatabaseFact]
    public async Task StudentsOutsidersPreviewAndArchivedCoursesCannotWriteWhileTaCan()
    {
        var request=new CourseToolsController.GroupInput{Name="助教修改",Members=[student,peer]};
        foreach(var actor in new[]{student,outsider})Assert.IsType<ForbidResult>(await Controller(actor).SaveGroup(course,request,default,group));
        Assert.IsType<ForbidResult>(await Controller(teacher,true).SaveGroup(course,request,default,group));
        Assert.IsType<OkObjectResult>(await Controller(ta).SaveGroup(course,request,default,group));
        await Sql("UPDATE courses SET status='archived' WHERE id=$1",course);
        Assert.IsType<ForbidResult>(await Controller(teacher).SaveGroup(course,request,default,group));
        Assert.IsType<ForbidResult>(await Controller(teacher).Dissolve(course,group,default));
    }

    public async Task DisposeAsync()
    {
        if(!Enabled)return;
        await Sql("DELETE FROM audit_events WHERE actor_user_id=ANY($1)",new[]{teacher,ta});
        await Sql("DELETE FROM course_grade_scores WHERE item_id=$1",item);
        await Sql("DELETE FROM report_submissions WHERE report_id=$1",report);
        await Sql("DELETE FROM course_grade_items WHERE id=$1",item);
        await Sql("DELETE FROM report_assignments WHERE id=$1",report);
        await Sql("DELETE FROM course_group_members WHERE group_id=ANY($1)",new[]{group,otherGroup});
        await Sql("DELETE FROM course_groups WHERE course_id=$1",course);
        await Sql("DELETE FROM course_members WHERE course_id=$1",course);
        await Sql("DELETE FROM courses WHERE id=$1",course);
        await Sql("DELETE FROM app_users WHERE id=ANY($1)",new[]{teacher,ta,student,peer,available,occupied,outsider});
        await db.DisposeAsync();
    }
}
