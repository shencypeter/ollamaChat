using System.Security.Claims;
using System.Text.Json;
using AiTeachingAssistant.Controllers;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AiTeachingAssistant.Tests;

public class CourseUnitTests : IAsyncLifetime
{
    private NpgsqlDataSource db=null!;
    private readonly Guid teacher=Guid.NewGuid(),ta=Guid.NewGuid(),student=Guid.NewGuid(),course=Guid.NewGuid(),other=Guid.NewGuid(),material=Guid.NewGuid(),draftQuiz=Guid.NewGuid(),publishedQuiz=Guid.NewGuid();
    private static bool Enabled=>!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION"));
    private async Task<object?> Sql(string sql,params object[] args){await using var cmd=db.CreateCommand(sql);foreach(var arg in args)cmd.Parameters.AddWithValue(arg);return await cmd.ExecuteScalarAsync();}
    public async Task InitializeAsync()
    {
        if(!Enabled)return;db=NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")!);
        foreach(var id in new[]{teacher,ta,student})await Sql("INSERT INTO app_users(id,display_name) VALUES($1,'合成單元測試')",id);
        foreach(var id in new[]{course,other})await Sql("INSERT INTO courses(id,code,title,term_code,owner_user_id,status) VALUES($1,$2,'合成單元課程','115-1',$3,'active')",id,"UNIT-"+id.ToString("N"),teacher);
        foreach(var (id,role) in new[]{(teacher,"teacher"),(ta,"ta"),(student,"student")})await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,$3)",course,id,role);
        await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,'teacher')",other,teacher);
        await Sql("INSERT INTO course_resources(id,course_id,file_name,content,uploaded_by) VALUES($1,$2,'unit.txt',$3,$4)",material,course,"unit"u8.ToArray(),teacher);
        await Sql("INSERT INTO assignments(id,course_id,title,status) VALUES($1,$3,'未發布單元測驗','draft'),($2,$3,'已發布單元測驗','published')",draftQuiz,publishedQuiz,course);
    }
    private (DefaultHttpContext Context,LearnerExperience Experience) Context(Guid id,bool practice=false)
    {
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim("can_teach",bool.TrueString)},"test"))};
        if(practice)context.Request.Headers[LearnerExperience.Header]="student";
        return(context,new LearnerExperience(new HttpContextAccessor{HttpContext=context}){CourseRole=id==teacher?"teacher":id==ta?"ta":"student"});
    }
    private CourseUnitsController Units(Guid id,bool practice=false){var x=Context(id,practice);return new(db,x.Experience){ControllerContext=new(){HttpContext=x.Context}};}
    private CoursesController Courses(Guid id){var x=Context(id);return new(db,x.Experience){ControllerContext=new(){HttpContext=x.Context}};}
    private CourseToolsController Tools(Guid id){var x=Context(id);return new(db,x.Experience){ControllerContext=new(){HttpContext=x.Context}};}
    private static async Task<IActionResult> Upload(CourseToolsController controller,Guid course,IFormFile file,string title,Guid unit)
    {
        var method=typeof(CourseToolsController).GetMethods().Single(x=>x.Name=="Upload");
        var values=method.GetParameters().Select(p=>p.Name switch{"course"=>course,"file"=>file,"title"=>title,"unitId"=>unit,"ct"=>default(CancellationToken),_=>p.HasDefaultValue?p.DefaultValue:null}).ToArray();
        return await (Task<IActionResult>)method.Invoke(controller,values)!;
    }
    private static JsonElement Json(IActionResult result)=>JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result).Value);

    [Fact]
    public async Task TeacherCanCreateReorderAndMoveWhileTaAndStudentCannotWrite()
    {
        if(!Enabled)return;
        var first=Json(await Units(teacher).Create(course,new(){Name="主題一",Description="基礎"},default)).GetProperty("id").GetGuid();
        var second=Json(await Units(teacher).Create(course,new(){Name="主題二"},default)).GetProperty("id").GetGuid();
        Assert.IsType<ForbidResult>(await Units(ta).Create(course,new(){Name="助教冒用"},default));
        Assert.IsType<ForbidResult>(await Units(student).Create(course,new(){Name="學生冒用"},default));
        Assert.IsType<NoContentResult>(await Units(teacher).Reorder(course,new(){UnitIds=[second,first]},default));
        var rows=Json(await Units(teacher).Curriculum(course,default));
        Assert.Equal(second,rows.GetProperty("units")[0].GetProperty("id").GetGuid());
        Assert.IsType<BadRequestObjectResult>(await Units(teacher).Move(course,"material",material,new(){UnitId=Guid.NewGuid()},default));
        Assert.IsType<NoContentResult>(await Units(teacher).Move(course,"material",material,new(){UnitId=first},default));
        Assert.IsType<ConflictObjectResult>(await Units(teacher).Delete(course,first,default));
        Assert.IsType<NoContentResult>(await Units(teacher).Move(course,"material",material,new(){UnitId=null},default));
        Assert.IsType<NoContentResult>(await Units(teacher).Delete(course,first,default));
    }

    [Fact]
    public async Task StudentCurriculumHidesDraftAssessmentAndPreviewIsReadOnly()
    {
        if(!Enabled)return;
        var unit=Json(await Units(teacher).Create(course,new(){Name="公開單元"},default)).GetProperty("id").GetGuid();
        await Units(teacher).Move(course,"quiz",draftQuiz,new(){UnitId=unit},default);
        await Units(teacher).Move(course,"quiz",publishedQuiz,new(){UnitId=unit},default);
        var studentData=Json(await Units(student).Curriculum(course,default));
        Assert.False(studentData.GetProperty("canManage").GetBoolean());
        var ids=studentData.GetProperty("activities").EnumerateArray().Select(x=>x.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(publishedQuiz,ids);Assert.DoesNotContain(draftQuiz,ids);
        var preview=Json(await Units(teacher,true).Curriculum(course,default));
        Assert.False(preview.GetProperty("canManage").GetBoolean());Assert.DoesNotContain(draftQuiz,preview.GetProperty("activities").EnumerateArray().Select(x=>x.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task NewResourcesRequireASameCourseUnitAndListItsUnitWithoutTags()
    {
        if(!Enabled)return;
        var own=Json(await Units(teacher).Create(course,new(){Name="教材單元"},default)).GetProperty("id").GetGuid();
        var foreign=Json(await Units(teacher).Create(other,new(){Name="其他課單元"},default)).GetProperty("id").GetGuid();
        IFormFile File()=>new FormFile(new MemoryStream("material"u8.ToArray()),0,8,"file","material.txt");
        Assert.IsType<BadRequestObjectResult>(await Upload(Tools(teacher),course,File(),"缺單元",Guid.Empty));
        Assert.IsType<BadRequestObjectResult>(await Upload(Tools(teacher),course,File(),"跨課",foreign));
        Assert.IsType<OkObjectResult>(await Upload(Tools(teacher),course,File(),"同課",own));
        Assert.IsType<BadRequestObjectResult>(await Tools(teacher).AddYouTube(course,new(){Title="缺單元回放",Url="https://youtu.be/M7lc1UVf-VE",UnitId=Guid.Empty},default));
        Assert.IsType<OkObjectResult>(await Tools(teacher).AddYouTube(course,new(){Title="同課回放",Url="https://youtu.be/M7lc1UVf-VE",UnitId=own},default));
        var listed=Json(await Tools(teacher).Resources(course,default));
        var created=listed.EnumerateArray().Where(x=>x.GetProperty("unitId").ValueKind!=JsonValueKind.Null && x.GetProperty("unitId").GetGuid()==own).ToArray();
        Assert.Equal(2,created.Length);
        Assert.All(created,x=>Assert.False(x.TryGetProperty("tags",out _)));
    }

    [Fact]
    public async Task CourseEditUsesVersionAndRelatedCourseMustArchiveInsteadOfDelete()
    {
        if(!Enabled)return;
        var before=Json(await Courses(teacher).Get(course,default));var version=before.GetProperty("Version").GetInt32();
        var input=new UpdateCourseRequest{Code="UNIT-EDIT-"+course.ToString("N"),Title="單元課程已編輯",TermCode="115-2",ClassLabel="A班",ExpectedVersion=version};
        Assert.IsType<NoContentResult>(await Courses(teacher).Update(course,input,default));
        Assert.IsType<ConflictObjectResult>(await Courses(teacher).Update(course,input,default));
        Assert.IsType<ConflictObjectResult>(await Courses(teacher).Delete(course,default));
        Assert.IsType<NoContentResult>(await Courses(teacher).Archive(course,default));
        Assert.IsType<ConflictObjectResult>(await Courses(teacher).Update(course,new(){Code=input.Code,Title=input.Title,TermCode=input.TermCode,ClassLabel=input.ClassLabel,ExpectedVersion=version+1},default));
    }

    public async Task DisposeAsync()
    {
        if(!Enabled)return;
        await Sql("DELETE FROM audit_events WHERE actor_user_id=ANY($1) OR entity_id=ANY($1)",new[]{teacher,ta,student,course,other,material,draftQuiz,publishedQuiz});
        await Sql("DELETE FROM course_resources WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM assignments WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM course_units WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM course_members WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM courses WHERE id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM app_users WHERE id=ANY($1)",new[]{teacher,ta,student});await db.DisposeAsync();
    }
}
