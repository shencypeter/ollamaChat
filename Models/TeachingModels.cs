using System.ComponentModel.DataAnnotations;
namespace AiTeachingAssistant.Models;

public sealed record SnapshotAnswer(Guid QuestionId, Guid RevisionId, string Text);
public sealed record SubmissionSnapshot(Guid Id, int RevisionNo, Guid AuthorUserId, DateTimeOffset SubmittedAt, IReadOnlyList<SnapshotAnswer> Answers);
public sealed class RubricCriterionInput
{
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [StringLength(4000)] public string Description { get; set; } = "";
    [Range(0.001,1000)] public decimal Weight { get; set; } = 1;
    [Required, MinLength(4), MaxLength(4)] public List<string> Levels { get; set; } = [];
}
public sealed class SaveRubricRequest
{
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [Required, MinLength(1), MaxLength(20)] public List<RubricCriterionInput> Criteria { get; set; } = [];
    public int ExpectedVersion { get; set; }
}
public sealed record RubricCriterion(Guid Id,string Title,string Description,decimal Weight,IReadOnlyList<string> Levels);
public sealed record TeachingRubric(Guid Id,Guid CourseId,Guid FamilyId,int VersionNo,string Title,string Status,IReadOnlyList<RubricCriterion> Criteria);
public sealed record CriterionScore(Guid CriterionId,int Level);
public sealed class SaveGradeRequest
{
    public Guid? ExpectedGradeId { get; set; }
    [Required] public Dictionary<Guid,decimal> EssayScores {get;set;}=[];
    [Range(0,999999)] public decimal Score { get; set; }
    [StringLength(16000)] public string Feedback { get; set; } = "";
    [Required, MaxLength(20)] public List<CriterionScore> Criteria { get; set; } = [];
}
public sealed record ManualGrade(Guid Id,Guid SubmissionRevisionId,Guid? GraderUserId,string GraderName,decimal Score,string Feedback,
    DateTimeOffset GradedAt,DateTimeOffset? ReleasedAt,Guid? SupersedesGradeId,Guid? RubricId,IReadOnlyList<CriterionScore> Criteria,Dictionary<Guid,decimal>? EssayScores=null);
