using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace AiTeachingAssistant.Models;
public sealed class StudioProfileInput
{
    [Required,StringLength(100)]public string DisplayName{get;set;}="";
    [Required,StringLength(40)]public string StudentNumber{get;set;}="";
    [StringLength(100)]public string DepartmentYear{get;set;}="";
}
public sealed class StudioCaseInput
{
    public Guid CourseId{get;set;}
    [Required,StringLength(200)]public string Title{get;set;}="";
    [Required,StringLength(24000)]public string Material{get;set;}="";
    public Dictionary<string,string> Prompts{get;set;}=[];
    public Dictionary<string,string> ReferencePoints{get;set;}=[];
    public Guid? ExpectedDraftVersionId{get;set;}
}
public sealed class StudioSaveInput
{
    public int ExpectedVersion{get;set;}
    public Dictionary<string,string> Answers{get;set;}=[];
    public bool Submit{get;set;}
}
public sealed class StudioFeedbackInput { public Guid AnswerVersionId{get;set;} }
public sealed class StudioTemplateInput {
    [Required,StringLength(12000)]public string Body{get;set;}="";
    public int ExpectedVersion{get;set;}
}
public sealed class StudioSettingsInput {
    public bool FeedbackEnabled{get;set;}=true;
    [Range(5,300)]public int TimeoutSeconds{get;set;}=180;
    [Range(1,10)]public int MaxRequestsPerRevision{get;set;}=3;
    public int ExpectedVersion{get;set;}
}
public sealed record StudioCase(Guid Id,Guid CourseId,string CourseTitle,Guid VersionId,int VersionNo,string Title,string Material,Dictionary<string,string> Prompts,Dictionary<string,string>? ReferencePoints,bool Published,Guid? DraftVersionId);
public sealed record StudioAnswer(Guid Id,string Stage,int RevisionNo,string Text,DateTimeOffset CreatedAt);
public sealed record StudioFeedback(Guid Id,Guid AnswerVersionId,string Status,StudioFeedbackContent? Content,string? ErrorCode,DateTimeOffset CreatedAt,DateTimeOffset? CompletedAt);
public sealed record StudioAttempt(Guid Id,int Version,DateTimeOffset? SubmittedAt,bool IsPractice,IReadOnlyList<StudioAnswer> Answers,IReadOnlyList<StudioFeedback> Feedback);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioEvidence(string Point,string Evidence);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioImprovement(string Point,string Evidence,string Suggestion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioFeedbackContent(string Summary,IReadOnlyList<StudioEvidence> SupportedPoints,IReadOnlyList<StudioImprovement> Improvements,IReadOnlyList<string> NextSteps);
