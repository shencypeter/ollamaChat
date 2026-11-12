using AiTeachingAssistant.Services;
namespace AiTeachingAssistant.Tests;

public class ResourceMaterialPolicyTests
{
    [Theory]
    [InlineData(2,20,10000)]
    [InlineData(0.5,5,10000)]
    [InlineData(2,120,0)]
    [InlineData(0,10,0)]
    [InlineData(5,50,0)]
    public void VideoRateCapsWallTimeAndStillRejectsForwardSeeks(double rate,double position,long expected)
    {
        var now=DateTimeOffset.UtcNow;
        Assert.Equal(expected,LearningTimePolicy.Credit(now,now.AddSeconds(10),true,true,0,position,rate));
    }

    [Fact]public void ResourceCreationContractsRequireAUnit()
    {
        Assert.Equal(typeof(Guid),typeof(AiTeachingAssistant.Controllers.CourseToolsController.YouTubeResourceInput).GetProperty("UnitId")!.PropertyType);
        var upload=typeof(AiTeachingAssistant.Controllers.CourseToolsController).GetMethods().Single(x=>x.Name=="Upload");
        Assert.Equal(typeof(Guid),upload.GetParameters().Single(x=>x.Name=="unitId").ParameterType);
    }
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=M7lc1UVf-VE")]
    [InlineData("https://youtu.be/M7lc1UVf-VE?si=tracking")]
    [InlineData("https://youtube.com/live/M7lc1UVf-VE")]
    [InlineData("https://m.youtube.com/shorts/M7lc1UVf-VE")]
    [InlineData("https://www.youtube-nocookie.com/embed/M7lc1UVf-VE")]
    public void AcceptsOnlyAnExplicitVideo(string url)=>Assert.Equal("M7lc1UVf-VE",YouTubeResourcePolicy.VideoId(url));
    [Theory]
    [InlineData("https://youtube.com.evil.invalid/watch?v=M7lc1UVf-VE")]
    [InlineData("https://www.youtube.com/playlist?list=anything")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://youtu.be/invalid")]
    [InlineData("https://user@youtube.com/watch?v=M7lc1UVf-VE")]
    [InlineData("https://youtube.com:1234/watch?v=M7lc1UVf-VE")]
    [InlineData("https://youtube.com/watch?v=M7lc1UVf-VE&v=abcdefghijk")]
    public void RejectsOtherHostsPlaylistsAndAmbiguousIds(string url)=>Assert.Throws<ArgumentException>(()=>YouTubeResourcePolicy.VideoId(url));
    [Fact]public void YouTubeUsesVideoTimeBoundsInsteadOfDocumentIdleCredit()
    {
        Assert.Equal("video/youtube",LearningTimePolicy.MediaType("YouTube 課程回放",[],"youtube"));
        var now=DateTimeOffset.UtcNow;
        Assert.Equal(10000,LearningTimePolicy.Credit(now,now.AddSeconds(10),true,true,0,10));
        Assert.Equal(0,LearningTimePolicy.Credit(now,now.AddSeconds(10),true,true,0,300));
    }
}
