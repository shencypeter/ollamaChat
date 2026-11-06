using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace AiTeachingAssistant.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RequestFormativeFeedback
{
    [Range(0,int.MaxValue)] public int ExpectedVersion { get; set; }
    public Guid AnswerRevisionId { get; set; }
}
public sealed record FormativeFeedback(Guid Id,Guid QuestionId,Guid AnswerRevisionId,int RevisionNo,string AnswerSha256,
    string Status,DateTimeOffset CreatedAt,DateTimeOffset? CompletedAt,string Model,string Profile,string PromptVersion,
    AssessmentResult? Assessment,bool IsStale,string? Error);
