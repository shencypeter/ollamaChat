using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace AiTeachingAssistant.Models;
public sealed class ShowcaseCreateInput
{
 public Guid? SnapshotId{get;set;}public Guid? QuestionId{get;set;}public Guid? ReportSubmissionId{get;set;}
}
public sealed class ShowcaseContentInput
{
 [Required,StringLength(200)]public string Title{get;set;}="";
 [Required(AllowEmptyStrings=true),StringLength(24000)]public string Background{get;set;}="";
 [Required(AllowEmptyStrings=true),StringLength(64000)]public string Answer{get;set;}="";
 [Required(AllowEmptyStrings=true),StringLength(16000)]public string Notes{get;set;}="";
 [Range(1,int.MaxValue)]public int ExpectedVersion{get;set;}
}
public sealed class ShowcasePublishInput
{
 [Range(1,int.MaxValue)]public int ExpectedVersion{get;set;}
 public bool Anonymized{get;set;}public bool Authorized{get;set;}public bool NoAnswerLeak{get;set;}
 [Required,StringLength(500)]public string AuthorizationReference{get;set;}="";
}
public sealed class ShowcaseVersionInput{[Range(1,int.MaxValue)]public int ExpectedVersion{get;set;}}
public sealed record ShowcasePublic(Guid Id,string Title,string Background,string Answer,string Notes,bool HasAttachment,DateTimeOffset? PublishedAt);
public sealed record ShowcaseDraft(Guid Id,int Version,int DraftRevision,int? PublishedRevision,string Status,string Kind,ShowcaseContentInput Content,bool HasAttachment,DateTimeOffset? PublishedAt);
