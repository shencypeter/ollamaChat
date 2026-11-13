using AiTeachingAssistant.Controllers;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;
using System.Text.Json;

namespace AiTeachingAssistant.Tests;

public class PortalFeedDatabaseTests : IAsyncLifetime
{
    private NpgsqlDataSource db=null!;
    private readonly Guid teacher=Guid.NewGuid(),ta=Guid.NewGuid(),student=Guid.NewGuid(),peer=Guid.NewGuid(),course=Guid.NewGuid(),other=Guid.NewGuid();
    private static bool Enabled=>!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION"));
    private async Task<object?> Sql(string sql,params object[] args){await using var cmd=db.CreateCommand(sql);foreach(var arg in args)cmd.Parameters.AddWithValue(arg);return await cmd.ExecuteScalarAsync();}
    public async Task InitializeAsync()
    {
        if(!Enabled)return;
        db=NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")!);
        foreach(var id in new[]{teacher,ta,student,peer})await Sql("INSERT INTO app_users(id,display_name) VALUES($1,'合成消息測試')",id);
        foreach(var id in new[]{course,other})await Sql("INSERT INTO courses(id,code,title,term_code,owner_user_id,status) VALUES($1,$2,'合成消息課程','115-1',$3,'active')",id,"FEED-"+id.ToString("N"),teacher);
        foreach(var (id,role) in new[]{(teacher,"teacher"),(ta,"ta"),(student,"student"),(peer,"student")})await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,$3)",course,id,role);
        await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,'teacher')",other,teacher);
    }
    private (DefaultHttpContext,LearnerExperience) Context(Guid actor,bool preview=false)
    {
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString()),new Claim("can_teach",bool.TrueString)],"test"))};
        if(preview)context.Request.Headers[LearnerExperience.Header]="student";
        return(context,new LearnerExperience(new HttpContextAccessor{HttpContext=context}){CourseRole=actor==teacher?"teacher":actor==ta?"ta":"student"});
    }
    private CourseAnnouncementsController Announcements(Guid actor,bool preview=false){var(c,e)=Context(actor,preview);return new(db,e){ControllerContext=new(){HttpContext=c}};}
    private PortalFeedController Feed(Guid actor,bool preview=false){var(c,e)=Context(actor,preview);return new(db,e){ControllerContext=new(){HttpContext=c}};}
    private static JsonElement Json(IActionResult result)=>JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result).Value);
    private static JsonElement[] Items(IActionResult result)=>Json(result).GetProperty("items").EnumerateArray().ToArray();

    [Fact]
    public async Task DraftCourseWithAnnouncementCannotBeDeleted()
    {
        if(!Enabled)return;
        await Sql("UPDATE courses SET status='draft' WHERE id=$1",other);
        await Announcements(teacher).Save(other,new(){Title="課程草稿公告",Body="必須保留的合成公告"},default);
        var(c,e)=Context(teacher);var controller=new CoursesController(db,e){ControllerContext=new(){HttpContext=c}};
        Assert.IsType<ConflictObjectResult>(await controller.Delete(other,default));
        Assert.Single(Items(await Announcements(teacher).List(other,default)));
        Assert.Equal(1L,await Sql("SELECT count(*) FROM courses WHERE id=$1",other));
    }

    [Fact]
    public async Task AnnouncementVisibilityRestrictionVersionAndArchivalAreEnforced()
    {
        if(!Enabled)return;
        var saved=Json(await Announcements(teacher).Save(course,new(){Title="草稿",Body="教師草稿"},default));var id=saved.GetProperty("id").GetGuid();
        Assert.Empty(Items(await Announcements(student).List(course,default)));
        Assert.Empty(Items(await Announcements(teacher,true).List(course,default)));
        Assert.IsType<ForbidResult>(await Announcements(student).Save(course,new(){Title="越權",Body="內容"},default));
        Assert.IsType<ForbidResult>(await Announcements(teacher,true).Save(course,new(){Title="預覽",Body="內容"},default));
        Assert.IsType<ConflictObjectResult>(await Announcements(teacher).Save(course,new(){Title="過期",Body="內容",ExpectedVersion=99},default,id));
        Assert.IsType<OkObjectResult>(await Announcements(teacher).Save(course,new(){Title="公告",Body="公開內容",Status="published",ExpectedVersion=1},default,id));
        var published=Assert.Single(Items(await Announcements(student).List(course,default)));Assert.Equal("published",published.GetProperty("status").GetString());Assert.Equal(2,published.GetProperty("version").GetInt32());
        Assert.IsType<NotFoundResult>(await Announcements(student).List(other,default));
        Assert.IsType<OkObjectResult>(await Announcements(ta).Save(course,new(){Title="助教公告",Body="公開",Status="published"},default));
        await Sql("UPDATE course_members SET ta_restrictions=ARRAY['materials'] WHERE course_id=$1 AND user_id=$2",course,ta);
        Assert.IsType<ForbidResult>(await Announcements(ta).Save(course,new(){Title="限制助教",Body="內容"},default));
        await Sql("UPDATE courses SET status='archived' WHERE id=$1",course);
        Assert.IsType<ForbidResult>(await Announcements(teacher).Save(course,new(){Title="封存",Body="內容"},default));
    }

    [Fact]
    public async Task FeedAndAgendaRespectEnrollmentPublicationAudiencePreviewAndPagination()
    {
        if(!Enabled)return;
        var assignment=Guid.NewGuid();var report=Guid.NewGuid();var item=Guid.NewGuid();var draftItem=Guid.NewGuid();var question=Guid.NewGuid();var onlineExam=Guid.NewGuid();
        await Announcements(teacher).Save(course,new(){Title="已發布公告",Body="合成公開文字",Status="published"},default);
        await Announcements(teacher).Save(course,new(){Title="隱藏公告",Body="合成草稿"},default);
        await Announcements(teacher).Save(other,new(){Title="其他課公告",Body="合成公開文字",Status="published"},default);
        await Sql("INSERT INTO assignments(id,course_id,title,status,due_at) VALUES($1,$2,'已發布作業','published',now()+interval '2 days')",assignment,course);
        await Sql("INSERT INTO assignments(course_id,title,status,due_at) VALUES($1,'隱藏草稿','draft',now()+interval '1 day')",course);
        await Sql("INSERT INTO exams(id,course_id,title,status,due_at,questions) VALUES($1,$2,'合成線上期末','published',now()+interval '4 days','[]')",onlineExam,course);
        await Sql("INSERT INTO report_assignments(id,course_id,title,max_score,created_by,status,submission_mode,due_at) VALUES($1,$2,'他組報告',100,$3,'published','group',now()+interval '1 day')",report,course,teacher);
        await Sql("INSERT INTO course_grade_items(id,course_id,title,category,max_score,weight,created_by,scheduled_at) VALUES($1,$3,'期中筆試','midterm',100,1,$4,now()+interval '3 days'),($2,$3,'未發布得分','other',100,1,$4,NULL)",item,draftItem,course,teacher);
        await Sql("INSERT INTO course_grade_scores(item_id,course_id,student_id,score,published_score,updated_by) VALUES($1,$3,$4,90,80,$6),($2,$3,$4,50,NULL,$6),($1,$3,$5,60,60,$6)",item,draftItem,course,student,peer,teacher);
        await Sql("INSERT INTO course_questions(id,course_id,author_id,title,question,created_at) VALUES($1,$2,$3,'新回覆討論','合成問題',now()-interval '5 days')",question,course,student);
        await Sql("INSERT INTO course_question_replies(id,question_id,author_id,body) VALUES($1,$2,$3,'合成回覆')",Guid.NewGuid(),question,teacher);
        var data=Items(await Feed(student).Feed(default,1,50,"student"));var ids=data.Select(x=>x.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(assignment,ids);Assert.DoesNotContain(report,ids);Assert.DoesNotContain(draftItem,ids);Assert.DoesNotContain(question,ids);
        Assert.Single(data.Where(x=>x.GetProperty("kind").GetString()=="grade"));Assert.All(data,x=>{Assert.Equal(course,x.GetProperty("courseId").GetGuid());Assert.False(x.TryGetProperty("score",out _));Assert.False(x.TryGetProperty("feedback",out _));});
        Assert.True(Json(await Feed(student).Feed(default,1,1,"student")).GetProperty("hasMore").GetBoolean());
        Assert.Empty(Items(await Feed(student).Feed(default,1,50,"teaching")));
        Assert.Empty(Items(await Feed(student).Feed(default,1,50,"student",other)));
        Assert.Empty(Items(await Feed(teacher,true).Feed(default,1,50,"teaching")));
        Assert.Empty(Items(await Feed(teacher,true).Agenda(default,"all")));
        var discussion=Assert.Single(Items(await Feed(teacher).Feed(default,1,50,"teaching",course)).Where(x=>x.GetProperty("id").GetGuid()==question));
        Assert.True(discussion.GetProperty("occurredAt").GetDateTimeOffset()>DateTimeOffset.UtcNow.AddMinutes(-5));
        var agenda=Items(await Feed(student).Agenda(default,"student"));Assert.Contains(agenda,x=>x.GetProperty("id").GetGuid()==item);Assert.Contains(agenda,x=>x.GetProperty("id").GetGuid()==assignment);Assert.DoesNotContain(agenda,x=>x.GetProperty("id").GetGuid()==report);
        Assert.Equal($"/courses/{course}?section=assessments&exam={item}",Assert.Single(agenda.Where(x=>x.GetProperty("id").GetGuid()==item)).GetProperty("url").GetString());
        Assert.Equal($"/courses/{course}?section=assessments&onlineExam={onlineExam}",Assert.Single(agenda.Where(x=>x.GetProperty("id").GetGuid()==onlineExam)).GetProperty("url").GetString());
        await Sql("UPDATE course_members SET status='inactive' WHERE course_id=$1 AND user_id=$2",course,student);
        Assert.Empty(Items(await Feed(student).Feed(default)));Assert.Empty(Items(await Feed(student).Agenda(default)));
    }

    [Fact]
    public async Task OnlyPublishedScoreChangesAdvanceGradeFeedTimeAndWithdrawalRemovesEvent()
    {
        if(!Enabled)return;
        var item=Guid.NewGuid();
        await Sql("INSERT INTO course_grade_items(id,course_id,title,category,max_score,weight,created_by) VALUES($1,$2,'出版時間測試','other',100,1,$3)",item,course,teacher);
        await Sql("INSERT INTO course_grade_scores(item_id,course_id,student_id,score,published_score,updated_by) VALUES($1,$2,$3,80,80,$4)",item,course,student,teacher);
        async Task<DateTimeOffset> EventTime()=>Assert.Single(Items(await Feed(student).Feed(default,1,50,"student"))).GetProperty("occurredAt").GetDateTimeOffset();
        var initial=await EventTime();
        await Sql("UPDATE course_grade_scores SET score=90,updated_at=now()+interval '1 day',published_at=now()+interval '2 days' WHERE item_id=$1",item);
        Assert.Equal(initial,await EventTime());
        await Sql("UPDATE course_grade_scores SET published_score=90 WHERE item_id=$1",item);
        var republished=await EventTime();Assert.True(republished>initial);
        await Sql("UPDATE course_grade_scores SET published_score=NULL WHERE item_id=$1",item);
        Assert.Equal(DBNull.Value,await Sql("SELECT published_at FROM course_grade_scores WHERE item_id=$1",item));
        Assert.Empty(Items(await Feed(student).Feed(default,1,50,"student")));
    }
    public async Task DisposeAsync()
    {
        if(!Enabled)return;
        await Sql("DELETE FROM audit_events WHERE actor_user_id=ANY($1)",new[]{teacher,ta,student,peer});
        await Sql("DELETE FROM course_announcements WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM course_questions WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM course_grade_scores WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM course_grade_items WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM exams WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM assignments WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM report_assignments WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM course_members WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM courses WHERE id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM app_users WHERE id=ANY($1)",new[]{teacher,ta,student,peer});await db.DisposeAsync();
    }
}
