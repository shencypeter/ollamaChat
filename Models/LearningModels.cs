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
public sealed record LearningGate(bool Started,IReadOnlyList<LearningRequirement> Requirements)
{
    public bool Met=>Requirements.All(r=>r.Met);
    public bool Locked=>Requirements.Count>0&&!Started;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LearningHeartbeat
{
    public double? Position {get;set;}
    public bool Active {get;set;}
}
public sealed record LearningSession(Guid Id,long AccumulatedSeconds);
public sealed record LearningResource(Guid Id,Guid CourseId,string Title,string FileName,string MediaType,long AccumulatedSeconds,bool CanTrack,bool IsPractice);
