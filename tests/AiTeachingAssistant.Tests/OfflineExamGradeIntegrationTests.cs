using System.Security.Claims;
using System.Text.Json;
using AiTeachingAssistant.Controllers;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
namespace AiTeachingAssistant.Tests;

public class OfflineExamGradeIntegrationTests:IAsyncLifetime
{
    private NpgsqlDataSource db=null!;
    private readonly Guid teacher=Guid.NewGuid(),ta=Guid.NewGuid(),a=Guid.NewGuid(),b=Guid.NewGuid(),course=Guid.NewGuid(),exam=Guid.NewGuid();
    private static bool Enabled=>!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION"));
    private async Task<object?> Sql(string sql,params object[] args){await using var cmd=db.CreateCommand(sql);foreach(var arg in args)cmd.Parameters.AddWithValue(arg);return await cmd.ExecuteScalarAsync();}
    public async Task InitializeAsync(){
        if(!Enabled)return;db=NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")!);
        foreach(var id in new[]{teacher,ta,a,b})await Sql("INSERT INTO app_users(id,display_name) VALUES($1,'合成紙本考試名冊')",id);
        await Sql("INSERT INTO courses(id,code,title,term_code,owner_user_id,status) VALUES($1,$2,'合成考試課程','115-1',$3,'active')",course,"PAPER-"+course.ToString("N"),teacher);
        foreach(var (id,role) in new[]{(teacher,"teacher"),(ta,"ta"),(a,"student"),(b,"student")})await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,$3)",course,id,role);
        await Sql("INSERT INTO course_grade_items(id,course_id,title,category,max_score,weight,created_by) VALUES($1,$2,'合成紙本期中','midterm',100,1,$3)",exam,course,teacher);
    }
    private GradebookController Controller(Guid actor,bool preview=false){
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString())],"test"))};
        if(preview)context.Request.Headers[LearnerExperience.Header]="student";
        return new(db,new LearnerExperience(new HttpContextAccessor{HttpContext=context}){CourseRole=actor==teacher?"teacher":actor==ta?"ta":"student"}){ControllerContext=new(){HttpContext=context}};
    }
    private OfflineExamBatchInput Input(int version,decimal? first=0,decimal? second=80)=>new(){ExpectedItemVersion=1,Scores=[new(){StudentId=a,Score=first,ExpectedVersion=version},new(){StudentId=b,Score=second,ExpectedVersion=version}]};
    private static void Conflict(IActionResult result)=>Assert.Equal(409,Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    private static void Forbidden(IActionResult result)=>Assert.Equal(403,Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    private static JsonElement Json(IActionResult result)=>JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result).Value);

    [FormativeDatabaseFact]public async Task WholeClassPublicationRequiresEveryScoreAndKeepsOldPublishedSnapshotDuringRevision(){
        var controller=Controller(teacher);
        Assert.IsType<NoContentResult>(await controller.SaveExamScores(course,exam,Input(0,0,null),default));
        Conflict(await controller.PublishExamScores(course,exam,Input(1),default));
        Assert.Equal(0L,(long)(await Sql("SELECT count(*) FROM course_grade_scores WHERE course_id=$1 AND published_score IS NOT NULL",course))!);
        Assert.Empty(Json(await Controller(a).Read(course,default)).GetProperty("scores").EnumerateArray());
        Assert.IsType<NoContentResult>(await controller.SaveExamScores(course,exam,Input(1),default));
        Assert.IsType<NoContentResult>(await controller.PublishExamScores(course,exam,Input(2),default));
        var own=Json(await Controller(a).Read(course,default)).GetProperty("scores").EnumerateArray().ToArray();
        Assert.Single(own);Assert.Equal(a,own[0].GetProperty("StudentId").GetGuid());Assert.Equal(0m,own[0].GetProperty("Score").GetDecimal());
        Assert.IsType<NoContentResult>(await controller.SaveExamScores(course,exam,Input(3,50,90),default));
        Assert.Equal(0m,(decimal)(await Sql("SELECT published_score FROM course_grade_scores WHERE item_id=$1 AND student_id=$2",exam,a))!);
        Conflict(await controller.PublishExamScores(course,exam,Input(3),default));
        Assert.Equal(0m,Json(await Controller(a).Read(course,default)).GetProperty("scores")[0].GetProperty("Score").GetDecimal());
        Assert.IsType<NoContentResult>(await controller.PublishExamScores(course,exam,Input(4),default));
        Assert.Equal(50m,(decimal)(await Sql("SELECT published_score FROM course_grade_scores WHERE item_id=$1 AND student_id=$2",exam,a))!);
    }

    [FormativeDatabaseFact]public async Task StaleOrIncompleteBatchRollsBackAllScoresAndSingleStudentPublishIsRejected(){
        var controller=Controller(teacher);await controller.SaveExamScores(course,exam,Input(0),default);
        var stale=Input(1,90,90);stale.Scores[1].ExpectedVersion=0;
        Conflict(await controller.SaveExamScores(course,exam,stale,default));
        Assert.Equal(0m,(decimal)(await Sql("SELECT score FROM course_grade_scores WHERE item_id=$1 AND student_id=$2",exam,a))!);
        var incomplete=Input(1);incomplete.Scores.RemoveAt(1);Conflict(await controller.PublishExamScores(course,exam,incomplete,default));
        var duplicate=Input(1);duplicate.Scores[1].StudentId=a;Conflict(await controller.SaveExamScores(course,exam,duplicate,default));
        Conflict(await controller.SaveScore(course,exam,new(){StudentId=a,Score=10,ExpectedVersion=1,Publish=true},default));
        Assert.Equal(0L,(long)(await Sql("SELECT count(*) FROM course_grade_scores WHERE item_id=$1 AND published_score IS NOT NULL",exam))!);
        Forbidden(await controller.SaveExamScores(Guid.NewGuid(),exam,Input(1),default));
        Assert.IsType<NotFoundObjectResult>(await controller.SaveExamScores(course,Guid.NewGuid(),Input(1),default));
    }

    [FormativeDatabaseFact]public async Task ServerEnforcesTeacherAuthorizedTaStudentPreviewAndArchiveRules(){
        Forbidden(await Controller(a).SaveExamScores(course,exam,Input(0),default));
        Forbidden(await Controller(teacher,true).SaveExamScores(course,exam,Input(0),default));
        await Sql("UPDATE course_members SET ta_restrictions=ARRAY['grading'] WHERE course_id=$1 AND user_id=$2",course,ta);
        Forbidden(await Controller(ta).SaveExamScores(course,exam,Input(0),default));
        await Sql("UPDATE course_members SET ta_restrictions='{}' WHERE course_id=$1 AND user_id=$2",course,ta);
        Assert.IsType<NoContentResult>(await Controller(ta).SaveExamScores(course,exam,Input(0),default));
        Assert.IsType<NoContentResult>(await Controller(ta).PublishExamScores(course,exam,Input(1),default));
        await Sql("UPDATE courses SET status='archived' WHERE id=$1",course);
        Forbidden(await Controller(teacher).PublishExamScores(course,exam,Input(2),default));
    }

    [FormativeDatabaseFact]public async Task ExamSettingsReuseGradeItemsAndPersistOptionalScheduledTime(){
        var when=new DateTimeOffset(2026,11,15,2,0,0,TimeSpan.Zero);
        Assert.IsType<OkObjectResult>(await Controller(teacher).SaveItem(course,new(){Title="合成期末考",Category="final",MaxScore=50,ScheduledAt=when,ExpectedVersion=1},default,exam));
        var item=Json(await Controller(a).Read(course,default)).GetProperty("items")[0];
        Assert.Equal(exam,item.GetProperty("Id").GetGuid());Assert.Equal(when,item.GetProperty("ScheduledAt").GetDateTimeOffset());
        Conflict(await Controller(teacher).SaveExamScores(course,exam,Input(0),default));
    }

    public async Task DisposeAsync(){
        if(!Enabled)return;
        await Sql("DELETE FROM audit_events WHERE actor_user_id=ANY($1)",new[]{teacher,ta,a,b});
        await Sql("DELETE FROM course_grade_scores WHERE course_id=$1",course);await Sql("DELETE FROM course_grade_items WHERE course_id=$1",course);
        await Sql("DELETE FROM course_members WHERE course_id=$1",course);await Sql("DELETE FROM courses WHERE id=$1",course);
        await Sql("DELETE FROM app_users WHERE id=ANY($1)",new[]{teacher,ta,a,b});await db.DisposeAsync();
    }
}
