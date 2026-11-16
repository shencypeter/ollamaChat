using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using AiTeachingAssistant.Controllers;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AiTeachingAssistant.Tests;

public class AcademicCalendarTests
{
    [Theory]
    [InlineData("2026-11-16T00:00:00Z", "115-1")]
    [InlineData("2027-01-31T15:59:59Z", "115-1")]
    [InlineData("2027-01-31T16:00:00Z", "115-2")]
    [InlineData("2027-07-31T15:59:59Z", "115-2")]
    [InlineData("2027-07-31T16:00:00Z", "116-1")]
    [InlineData("2026-07-15T00:00:00Z", "114-2")]
    public void DefaultsFollowTaiwanSystemDateAndAcademicYear(string utc, string expected)
        => Assert.Equal(expected, new AcademicCalendarOptions().ResolveCurrentTerm(DateTimeOffset.Parse(utc)));

    [Fact]
    public void MonthBoundariesAreConfigurableAndExplicitOverridesRemainCompatible()
    {
        var date=DateTimeOffset.Parse("2026-08-15T00:00:00Z");
        Assert.Equal("114-2",new AcademicCalendarOptions{FirstSemesterStartMonth=9,SecondSemesterStartMonth=3}.ResolveCurrentTerm(date));
        Assert.Equal("115-1",new AcademicCalendarOptions{CurrentTerm=""}.ResolveCurrentTerm(date));
        Assert.Equal("115-S",new AcademicCalendarOptions{CurrentTerm="115-S"}.ResolveCurrentTerm(date));
    }

    [Theory]
    [InlineData(0,2)]
    [InlineData(13,2)]
    [InlineData(8,0)]
    [InlineData(8,13)]
    [InlineData(2,8)]
    [InlineData(8,8)]
    public void InvalidCalendarBoundariesAreRejected(int first,int second)
    {
        var options=new AcademicCalendarOptions{FirstSemesterStartMonth=first,SecondSemesterStartMonth=second};
        Assert.False(Validator.TryValidateObject(options,new ValidationContext(options),[],true));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("115-1", true)]
    [InlineData("115-2", true)]
    [InlineData("115-S", true)]
    [InlineData("115-3", false)]
    [InlineData("2026-1", false)]
    [InlineData("latest", false)]
    public void CurrentTermRequiresAnExplicitValidAcademicTerm(string? term, bool valid)
    {
        var options = new AcademicCalendarOptions { CurrentTerm = term };
        Assert.Equal(valid, Validator.TryValidateObject(options, new ValidationContext(options), [], true));
        Assert.NotNull(typeof(AcademicCalendarController).GetCustomAttributes(typeof(AuthorizeAttribute), true).SingleOrDefault());
    }

    [Fact]
    public async Task TermsUseOnlyEligibleActorCoursesWhileCurrentTermUsesSystemDate()
    {
        var connection = Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        await using var db = NpgsqlDataSource.Create(connection);
        var teacher=Guid.NewGuid();var student=Guid.NewGuid();var active=Guid.NewGuid();var draft=Guid.NewGuid();var other=Guid.NewGuid();var archived=Guid.NewGuid();
        async Task Sql(string sql, params object[] values) { await using var cmd=db.CreateCommand(sql);foreach(var value in values)cmd.Parameters.AddWithValue(value);await cmd.ExecuteNonQueryAsync(); }
        try
        {
            await Sql("INSERT INTO app_users(id,display_name) VALUES($1,'合成行事曆教師'),($2,'合成行事曆學生')",teacher,student);
            foreach(var (id,term,status) in new[]{(active,"115-1","active"),(draft,"116-1","draft"),(other,"117-S","active"),(archived,"114-2","archived")})
            {
                await Sql("INSERT INTO courses(id,code,title,term_code,owner_user_id,status) VALUES($1,$2,'合成行事曆課程',$3,$4,$5)",id,"CAL-"+id.ToString("N"),term,teacher,status);
                await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,'teacher')",id,teacher);
                if(id!=other)await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,'student')",id,student);
            }
            AcademicCalendarController Controller(Guid actor,string? configured=null)
            {
                var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString())],"test"))};
                return new(db,new LearnerExperience(new HttpContextAccessor{HttpContext=context}),Microsoft.Extensions.Options.Options.Create(new AcademicCalendarOptions{CurrentTerm=configured}),new FixedCalendarClock()){ControllerContext=new(){HttpContext=context}};
            }
            static JsonElement Data(IActionResult result)=>JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result).Value);
            var result=Data(await Controller(student).Get(default,true));
            Assert.Equal("115-1",result.GetProperty("currentTerm").GetString());
            Assert.Equal(new[]{"115-1","114-2"},result.GetProperty("terms").EnumerateArray().Select(x=>x.GetString()));
            Assert.Equal(new[]{"1","2","S"},result.GetProperty("semesters").EnumerateArray().Select(x=>x.GetProperty("code").GetString()));
            var teaching=Data(await Controller(teacher,"118-S").Get(default));
            Assert.Equal("118-S",teaching.GetProperty("currentTerm").GetString());
            Assert.Contains("116-1",teaching.GetProperty("terms").EnumerateArray().Select(x=>x.GetString()));
            await Sql("UPDATE course_members SET status='inactive' WHERE course_id=$1 AND user_id=$2",active,student);
            Assert.Equal(new[]{"114-2"},Data(await Controller(student).Get(default,true)).GetProperty("terms").EnumerateArray().Select(x=>x.GetString()));
        }
        finally
        {
            foreach(var id in new[]{active,draft,other,archived})await Sql("DELETE FROM courses WHERE id=$1",id);
            await Sql("DELETE FROM app_users WHERE id IN ($1,$2)",teacher,student);
        }
    }
    private sealed class FixedCalendarClock:TimeProvider
    {
        public override DateTimeOffset GetUtcNow()=>DateTimeOffset.Parse("2026-11-16T00:00:00Z");
    }
}
