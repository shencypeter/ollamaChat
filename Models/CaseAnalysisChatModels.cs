using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiTeachingAssistant.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CaseChatInput
{
    public Guid RequestId {get;set;}
    public Guid? RevisesMessageId {get;set;}
    [StringLength(32)] public string? ModelProfile {get;set;}
    [Required,StringLength(8000,MinimumLength=1)] public string Content {get;set;}="";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RawChatInput
{
    [StringLength(32)] public string? ModelProfile {get;set;}
    [Required,StringLength(8000,MinimumLength=1)] public string Content {get;set;}="";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CaseChatModelSelection
{
    [StringLength(32)] public string? ModelProfile {get;set;}
}
public sealed record CaseChatStatistics(int StudentMessages,int AssistantReplies,int RoundTrips,DateTimeOffset? LastActivityAt,int RevisionCount=0);
public sealed record CaseChatMessage(Guid Id,string Role,string Content,int Sequence,string Status,Guid? ReplyTo,string? ModelName,string? PromptVersion,DateTimeOffset CreatedAt,RequestMetrics? Metrics=null,Guid? RevisesMessageId=null,IReadOnlyList<CaseTextAnnotation>? Annotations=null,DateTimeOffset? TrashedAt=null,int? RuleVersion=null,IReadOnlyList<CaseClassificationRuleDefinition>? RuleSnapshot=null,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] int? HistoryNumber=null);
public sealed record CaseTextAnnotation(string AnnotationId,int Start,int End,string Text,IReadOnlyList<string> Codes,string Explanation,string Status);
public sealed record CaseChatSession(Guid Id,string Stage,string Status,IReadOnlyList<CaseChatMessage> Messages,CaseChatStatistics Statistics);
public sealed record CaseStageHistory(IReadOnlyList<CaseChatSession> Sessions,Guid? ActiveSessionId,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] CaseHistoryPagination? Pagination=null);
public sealed class CaseHistoryQuery
{
    public bool Paged {get;set;}
    [Range(1,int.MaxValue)] public int? Before {get;set;}
    public Guid? Target {get;set;}
    [StringLength(512)] public string? Model {get;set;}
    public bool Trashed {get;set;}
}
public sealed record CaseHistoryModel(string Key,string? ModelName,int NormalCount,int TrashCount);
public sealed record CaseHistoryPagination(int? NextCursor,int TotalCount,int TrashCount,IReadOnlyList<CaseHistoryModel> Models,Guid? ActiveLastMessageId,string ModelKey,bool Trashed,int PageSize=5,bool Focused=false);
public sealed record CaseChatStageStatistics(string Stage,CaseChatStatistics Statistics);
public sealed record CaseChatStudent(Guid Id,string DisplayName,string? StudentNumber,IReadOnlyList<CaseChatStageStatistics> Stages,CaseChatStatistics Statistics);
public sealed record CaseChatAccess(string Role,string Status,bool Enabled,string[] Restrictions)
{
    public bool Practice=>Role is "teacher" or "ta";
    public bool CanReview=>Practice&&!(Role=="ta"&&Restrictions.Contains("statistics"));
}
