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

    [Fact]public void NormalizesWeekAliasesAndDeduplicatesWithoutLosingCustomTopics()
    {
        Assert.Equal(new[]{"第1週","PDF","案例 分析"},ResourceTagPolicy.Normalize([" 第１周 ","week 01","PDF","pdf","案例   分析"," "]));
        Assert.Empty(ResourceTagPolicy.Normalize(null));
    }
    [Fact]public void RejectsOversizedOrAmbiguousTags()
    {
        Assert.Throws<ArgumentException>(()=>ResourceTagPolicy.Normalize([new string('a',31)]));
        Assert.Throws<ArgumentException>(()=>ResourceTagPolicy.Normalize(Enumerable.Range(0,11).Select(i=>"tag"+i)));
        Assert.Throws<ArgumentException>(()=>ResourceTagPolicy.Normalize([null]));
        Assert.Throws<ArgumentException>(()=>ResourceTagPolicy.Normalize(["one,two"]));
        Assert.Throws<ArgumentException>(()=>ResourceTagPolicy.Normalize(["bad\0tag"]));
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
