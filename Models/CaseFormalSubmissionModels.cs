using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiTeachingAssistant.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CaseFormalSubmissionInput
{
    public Guid RequestId {get;set;}
    public Guid MessageId {get;set;}
    public Guid? AssignmentId {get;set;}
    public Guid? ExpectedPreviousId {get;set;}
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CaseFormalReturnInput
{
    [Required,StringLength(2000,MinimumLength=1)] public string Reason {get;set;}="";
}
public sealed record CaseFormalAssignment(Guid Id,string Title);
public sealed record CaseFormalSubmission(Guid Id,Guid CourseId,Guid StudentId,string StudentName,string Stage,int Version,string Status,
    DateTimeOffset SubmittedAt,DateTimeOffset? ReturnedAt,string? ReturnReason,Guid? AssignmentId,Guid AnalysisMessageId,
    string Answer,string Feedback,IReadOnlyList<CaseTextAnnotation> Annotations,string ModelName,string PromptVersion,
    int? RuleVersion=null,JsonElement? RuleSnapshot=null);
public sealed record CaseFormalEnvelope(IReadOnlyList<CaseFormalSubmission> Submissions,IReadOnlyList<CaseFormalAssignment> Assignments);
