using System.Security.Claims;
using System.Text.Json;
using AiTeachingAssistant.Controllers;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AiTeachingAssistant.Tests;

public class CourseInteractionTests:IAsyncLifetime
{
    private NpgsqlDataSource db=null!;
    private readonly Guid teacher=Guid.NewGuid(),ta=Guid.NewGuid(),student=Guid.NewGuid(),peer=Guid.NewGuid(),outsider=Guid.NewGuid(),course=Guid.NewGuid(),other=Guid.NewGuid(),resource=Guid.NewGuid();
    private static bool Enabled=>!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION"));
    private async Task<object?> Sql(string sql,params object[] args){await using var c=db.CreateCommand(sql);foreach(var arg in args)c.Parameters.AddWithValue(arg);return await c.ExecuteScalarAsync();}
    public async Task InitializeAsync()
    {
        if(!Enabled)return;db=NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")!);
        foreach(var id in new[]{teacher,ta,student,peer,outsider})await Sql("INSERT INTO app_users(id,display_name) VALUES($1,'合成互動測試')",id);
        foreach(var id in new[]{course,other})await Sql("INSERT INTO courses(id,code,title,term_code,owner_user_id,status) VALUES($1,$2,'合成互動課程','115-1',$3,'active')",id,"INTERACT-"+id.ToString("N"),teacher);
        foreach(var (id,role) in new[]{(teacher,"teacher"),(ta,"ta"),(student,"student"),(peer,"student")})await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,$3)",course,id,role);
        await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,'teacher')",other,teacher);
        await Sql("INSERT INTO course_resources(id,course_id,file_name,content,uploaded_by) VALUES($1,$2,'synthetic.txt',$3,$4)",resource,course,"synthetic"u8.ToArray(),teacher);
    }
    private (DefaultHttpContext Context,LearnerExperience Experience) Context(Guid id,bool practice=false)
    {
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim("can_teach",bool.TrueString)},"test"))};
        if(practice)context.Request.Headers[LearnerExperience.Header]="student";
        return(context,new LearnerExperience(new HttpContextAccessor{HttpContext=context}){CourseRole=id==teacher?"teacher":id==ta?"ta":"student"});
    }
    private CourseInteractionsController As(Guid id,bool practice=false){var c=Context(id,practice);return new(db,c.Experience){ControllerContext=new(){HttpContext=c.Context}};}
    private InteractionProfilesController Profile(Guid id){var c=Context(id);return new(db,c.Experience){ControllerContext=new(){HttpContext=c.Context}};}
    private NotificationsController Notices(Guid id,bool practice=false){var c=Context(id,practice);return new(db,c.Experience){ControllerContext=new(){HttpContext=c.Context}};}
    private static JsonElement Value(IActionResult result)=>JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result).Value);
    [Fact] public async Task LikesAreIdempotentAcrossConcurrentRequestsAndCourseBound()
    {
        if(!Enabled)return;
        var results=await Task.WhenAll(Enumerable.Range(0,5).Select(_=>As(student).Like(course,resource,new(){Liked=true},default)));
        Assert.All(results,r=>Assert.IsType<NoContentResult>(r));
        Assert.Equal(1,Value(await As(peer).Interactions(course,resource,default)).GetProperty("likes").GetInt64());
        Assert.IsType<ForbidResult>(await As(outsider).Like(course,resource,new(){Liked=true},default));
        Assert.IsType<NotFoundResult>(await As(teacher).Like(other,resource,new(){Liked=true},default));
        await As(student).Like(course,resource,new(){Liked=false},default);
        Assert.Equal(0,Value(await As(student).Interactions(course,resource,default)).GetProperty("likes").GetInt64());
    }
    [Fact] public async Task StaticViewsAreIdempotentAndStatisticsShowRosterViewsAndLikes()
    {
        if(!Enabled)return;
        var request=new CourseInteractionsController.ViewInput{Id=Guid.NewGuid()};
        Assert.IsType<NoContentResult>(await As(student).RecordView(course,resource,request,default));
        Assert.IsType<NoContentResult>(await As(student).RecordView(course,resource,request,default));
        Assert.IsType<NoContentResult>(await As(peer).RecordView(course,resource,new(){Id=Guid.NewGuid()},default));
        Assert.IsType<NoContentResult>(await As(teacher).RecordView(course,resource,new(){Id=Guid.NewGuid()},default));
        await As(student).Like(course,resource,new(){Liked=true},default);
        var stats=JsonSerializer.SerializeToElement(await new LearningTime(db,TimeProvider.System).Statistics(course,resource,teacher,false,default));
        Assert.False(stats.GetProperty("isVideo").GetBoolean());Assert.Equal(2,stats.GetProperty("viewCount").GetInt64());Assert.Equal(2,stats.GetProperty("viewedCount").GetInt32());Assert.Equal(1,stats.GetProperty("likes").GetInt64());
        Assert.Equal(1,stats.GetProperty("students").EnumerateArray().Single(x=>x.GetProperty("id").GetGuid()==student).GetProperty("views").GetInt64());
    }
    [Fact] public async Task CommentsArePlainTextDeduplicatedAndPaged()
    {
        if(!Enabled)return;
        var request=new CourseInteractionsController.TextInput{Id=Guid.NewGuid(),Text="<b>合成留言</b>"};
        Assert.IsType<NoContentResult>(await As(student).Comment(course,resource,request,default));
        Assert.IsType<NoContentResult>(await As(student).Comment(course,resource,request,default));
        Assert.IsType<ConflictObjectResult>(await As(peer).Comment(course,resource,request,default));
        for(var i=0;i<20;i++)await As(peer).Comment(course,resource,new(){Id=Guid.NewGuid(),Text="合成分頁"},default);
        var data=Value(await As(student).Interactions(course,resource,default,2));
        Assert.Equal(21,data.GetProperty("total").GetInt64());Assert.Equal(1,data.GetProperty("items").GetArrayLength());
        Assert.Equal(request.Text,data.GetProperty("items")[0].GetProperty("text").GetString());
    }
    [Fact] public async Task QuestionsAreVisibleBeforeReplyAndBothTeachingRolesCanReplyWithConflictProtection()
    {
        if(!Enabled)return;
        var id=Guid.NewGuid();await As(student).Ask(course,new(){Id=id,Text="合成未答問題"},default);
        var data=Value(await As(peer).Questions(course,default));Assert.Equal(1,data.GetProperty("total").GetInt64());Assert.Equal(JsonValueKind.Null,data.GetProperty("items")[0].GetProperty("answeredAt").ValueKind);
        Assert.IsType<ForbidResult>(await As(peer).Reply(course,id,new(){Response="冒用",ExpectedVersion=1},default));
        Assert.IsType<NoContentResult>(await As(ta).Reply(course,id,new(){Response="合成助教答覆",ExpectedVersion=1},default));
        Assert.IsType<ConflictObjectResult>(await As(teacher).Reply(course,id,new(){Response="舊版",ExpectedVersion=1},default));
        Assert.IsType<NoContentResult>(await As(teacher).Reply(course,id,new(){Response="合成教師答覆",ExpectedVersion=2},default));
        Assert.IsType<ForbidResult>(await As(outsider).Questions(course,default));
    }
    [Fact] public async Task DiscussionsAllowMemberRepliesAndLikesWhileOnlyModeratorsCanClose()
    {
        if(!Enabled)return;
        var id=Guid.NewGuid();await As(teacher).Ask(course,new(){Id=id,Title="合成討論主題",Text="教師也能發起討論"},default);
        var studentReply=Guid.NewGuid();var taReply=Guid.NewGuid();
        Assert.IsType<NoContentResult>(await As(student).AddReply(course,id,new(){Id=studentReply,Text="學生參與"},default));
        Assert.IsType<NoContentResult>(await As(ta).AddReply(course,id,new(){Id=taReply,Text="助教回覆"},default));
        Assert.IsType<NoContentResult>(await As(peer).LikeReply(course,id,taReply,new(){Liked=true},default));
        var list=Value(await As(peer).Questions(course,default,1,true));
        Assert.Equal("合成討論主題",list.GetProperty("items")[0].GetProperty("title").GetString());
        Assert.True(list.GetProperty("items")[0].GetProperty("replyCount").GetInt64()>=2);
        var replies=Value(await As(peer).Replies(course,id,default,1,true)).GetProperty("items");
        Assert.Equal("student",replies[0].GetProperty("authorRole").GetString());
        Assert.Equal("ta",replies[1].GetProperty("authorRole").GetString());
        Assert.Equal(1,replies[1].GetProperty("likes").GetInt64());
        Assert.IsType<ForbidResult>(await As(student).Lock(course,id,true,default));
        Assert.IsType<NoContentResult>(await As(teacher).Lock(course,id,true,default));
        Assert.IsType<ConflictObjectResult>(await As(student).AddReply(course,id,new(){Id=Guid.NewGuid(),Text="關閉後回覆"},default));
        Assert.IsType<NoContentResult>(await As(student).Vote(course,id,true,default));
        Assert.IsType<NoContentResult>(await As(student).LikeReply(course,id,taReply,new(){Liked=true},default));
    }
    [Fact] public async Task PracticeAndArchivedCoursesCannotWrite()
    {
        if(!Enabled)return;
        Assert.IsType<ForbidResult>(await As(teacher,true).Like(course,resource,new(){Liked=true},default));
        Assert.IsType<ForbidResult>(await As(ta,true).Reply(course,Guid.NewGuid(),new(){Response="練習",ExpectedVersion=1},default));
        await Sql("UPDATE courses SET status='archived' WHERE id=$1",course);
        Assert.IsType<ForbidResult>(await As(student).Comment(course,resource,new(){Id=Guid.NewGuid(),Text="封存"},default));
        Assert.IsType<ForbidResult>(await As(student).Ask(course,new(){Id=Guid.NewGuid(),Text="封存"},default));
        Assert.False(Value(await As(student).Interactions(course,resource,default)).GetProperty("writable").GetBoolean());
    }
    [Fact] public async Task ProfilesRestrictOtherLearnersToTaughtCourseAndExcludeCancelledLikesAndPracticeTime()
    {
        if(!Enabled)return;
        await As(student).Like(course,resource,new(){Liked=true},default);
        await As(student).Comment(course,resource,new(){Id=Guid.NewGuid(),Text="合成檔案互動"},default);
        await Sql("INSERT INTO learning_time_sessions(resource_id,user_id,is_practice,started_at,last_seen_at,credited_milliseconds,content_milliseconds,closed) VALUES($1,$2,false,now(),now(),10000,20000,true),($1,$2,true,now(),now(),90000,90000,true)",resource,student);
        var own=Value(await Profile(student).Get(default));Assert.Equal(3,own.GetProperty("total").GetInt64());
        Assert.IsType<ForbidResult>(await Profile(peer).Get(default,student,course));
        Assert.IsType<ForbidResult>(await Profile(outsider).Get(default,student,course));
        Assert.IsType<ForbidResult>(await Profile(teacher).Get(default,student));
        Assert.Equal(3,Value(await Profile(ta).Get(default,student,course)).GetProperty("total").GetInt64());
        await As(student).Like(course,resource,new(){Liked=false},default);
        Assert.Equal(0,Value(await Profile(student).Get(default,null,null,"like")).GetProperty("total").GetInt64());
        Assert.Equal(10,Value(await Profile(student).Get(default,null,null,"learning")).GetProperty("items")[0].GetProperty("seconds").GetInt64());
    }
    [Fact] public void ClientsCannotSupplyAuthors()=>Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<CourseInteractionsController.TextInput>("""{"id":"00000000-0000-0000-0000-000000000001","text":"synthetic","authorId":"spoof"}""",new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    [Fact] public async Task PublishedAssessmentsNotifyOnlyTheirStudentsAndReadsArePersonalAndPersistent()
    {
        if(!Enabled)return;
        var quiz=(Guid)(await Sql("INSERT INTO assignments(course_id,title) VALUES($1,'合成通知測驗') RETURNING id",course))!;
        var report=(Guid)(await Sql("INSERT INTO report_assignments(course_id,title,max_score,created_by) VALUES($1,'合成通知報告',100,$2) RETURNING id",course,teacher))!;
        await Sql("INSERT INTO course_grade_items(course_id,title,category,max_score,weight,created_by,source_assignment_id) VALUES($1,'合成通知測驗','quiz',100,1,$2,$3)",course,teacher,quiz);
        await Sql("INSERT INTO course_grade_items(course_id,title,category,max_score,weight,created_by,source_report_id) VALUES($1,'合成通知報告','assignment',100,1,$2,$3)",course,teacher,report);
        Assert.Equal(0,Value(await Notices(student).Summary(default)).GetProperty("total").GetInt64());
        await Sql("UPDATE assignments SET status='published' WHERE id=$1",quiz);await Sql("UPDATE report_assignments SET status='published' WHERE id=$1",report);
        Assert.Equal(2,Value(await Notices(student).Summary(default)).GetProperty("assignments").GetInt64());
        Assert.Equal(0,Value(await Notices(teacher).Summary(default)).GetProperty("assignments").GetInt64());
        Assert.Equal(0,Value(await Notices(outsider).Summary(default)).GetProperty("total").GetInt64());
        Assert.IsType<ForbidResult>(await Notices(outsider).Read(new(){Kind="quiz",Ids=[quiz]},default));
        var results=await Task.WhenAll(Enumerable.Range(0,3).Select(_=>Notices(student).Read(new(){Kind="quiz",Ids=[quiz]},default)));
        Assert.All(results,r=>Assert.IsType<NoContentResult>(r));
        Assert.Equal(1,Value(await Notices(student).Summary(default)).GetProperty("assignments").GetInt64());
        Assert.Equal(2,Value(await Notices(peer).Summary(default)).GetProperty("assignments").GetInt64());
        Assert.IsType<ForbidResult>(await Notices(student).Read(new(){Kind="report",Ids=[report,Guid.NewGuid()]},default));
        Assert.Equal(1,Value(await Notices(student).Summary(default)).GetProperty("assignments").GetInt64());
    }
    [Fact] public async Task QuestionNotificationsRemainForUnseenPagesAndOtherStaff()
    {
        if(!Enabled)return;
        for(var i=0;i<21;i++)await As(student).Ask(course,new(){Id=Guid.NewGuid(),Text="合成分頁新問題"},default);
        Assert.Equal(21,Value(await Notices(teacher).Summary(default)).GetProperty("questions").GetInt64());
        Assert.Equal(0,Value(await Notices(student).Summary(default)).GetProperty("questions").GetInt64());
        var shown=Value(await As(teacher).Questions(course,default)).GetProperty("items").EnumerateArray().Select(x=>x.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(20,shown.Length);
        await Notices(teacher).Read(new(){Kind="question",Ids=shown},default);
        Assert.Equal(1,Value(await Notices(teacher).Summary(default)).GetProperty("questions").GetInt64());
        Assert.Equal(21,Value(await Notices(ta).Summary(default)).GetProperty("questions").GetInt64());
        Assert.IsType<ForbidResult>(await Notices(student).Read(new(){Kind="question",Ids=shown},default));
        await As(student).Ask(course,new(){Id=Guid.NewGuid(),Text="讀取後才抵達的新問題"},default);
        Assert.Equal(2,Value(await Notices(teacher).Summary(default)).GetProperty("questions").GetInt64());
    }
    [Fact] public async Task NotificationCountsRespectPracticeArchiveAndRevokedMembership()
    {
        if(!Enabled)return;
        var id=Guid.NewGuid();await As(student).Ask(course,new(){Id=id,Text="合成權限問題"},default);
        Assert.Equal(1,Value(await Notices(teacher).Summary(default)).GetProperty("total").GetInt64());
        Assert.Equal(0,Value(await Notices(teacher,true).Summary(default)).GetProperty("total").GetInt64());
        Assert.IsType<ForbidResult>(await Notices(teacher,true).Read(new(){Kind="question",Ids=[id]},default));
        await Sql("UPDATE course_members SET status='inactive' WHERE course_id=$1 AND user_id=$2",course,ta);
        Assert.Equal(0,Value(await Notices(ta).Summary(default)).GetProperty("total").GetInt64());
        Assert.IsType<ForbidResult>(await Notices(ta).Read(new(){Kind="question",Ids=[id]},default));
        await Sql("UPDATE courses SET status='archived' WHERE id=$1",course);
        Assert.Equal(0,Value(await Notices(teacher).Summary(default)).GetProperty("total").GetInt64());
        Assert.IsType<ForbidResult>(await Notices(teacher).Read(new(){Kind="question",Ids=[id]},default));
    }
    public async Task DisposeAsync()
    {
        if(!Enabled)return;
        await Sql("DELETE FROM notification_reads WHERE user_id=ANY($1)",new[]{teacher,ta,student,peer,outsider});
        await Sql("DELETE FROM course_questions WHERE course_id=$1",course);
        await Sql("DELETE FROM learning_time_sessions WHERE resource_id=$1",resource);
        await Sql("DELETE FROM course_resources WHERE id=$1",resource);
        await Sql("DELETE FROM course_grade_items WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM assignments WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM report_assignments WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM course_members WHERE course_id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM courses WHERE id=ANY($1)",new[]{course,other});
        await Sql("DELETE FROM app_users WHERE id=ANY($1)",new[]{teacher,ta,student,peer,outsider});await db.DisposeAsync();
    }
}
