using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace AiTeachingAssistant.Models;

public sealed class LearningRequirementInput
{
    public Guid ResourceId {get;set;}
    [Range(1,86400)] public int MinimumSeconds {get;set;}
}
public sealed record LearningRequirement(Guid ResourceId,string Title,int MinimumSeconds,long AccumulatedSeconds,string? MediaType)
{
    public bool Met=>AccumulatedSeconds>=MinimumSeconds;
}
public sealed record LearningGate(bool Started,IReadOnlyList<LearningRequirement> Requirements,bool RequiresStart=false)
{
    public bool Met=>Requirements.All(r=>r.Met);
    public bool Locked=>(RequiresStart||Requirements.Count>0)&&!Started;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LearningHeartbeat
{
    public double? Position {get;set;}
    public bool Active {get;set;}
    public bool Close {get;set;}
    [Range(0.25,4)] public double PlaybackRate {get;set;}=1;
}
public sealed record LearningSession(Guid Id,long AccumulatedSeconds,long ContentSeconds=0);
public sealed record LearningResource(Guid Id,Guid CourseId,string Title,string FileName,string MediaType,long AccumulatedSeconds,bool CanTrack,bool IsPractice,string? YouTubeVideoId=null,long ContentSeconds=0);
