using System.ComponentModel.DataAnnotations;
namespace AiTeachingAssistant.Models;

public sealed class AssignmentQuestionInput
{
    [Required, StringLength(8000)] public string PromptText { get; set; } = "";
    [StringLength(40)] public string? StageCode { get; set; }
    public string QuestionType { get; set; } = "essay";
    public List<string> Options { get; set; } = [];
    public List<int> StandardAnswer { get; set; } = [];
    public decimal? Points {get;set;}
    [StringLength(8000)] public string GradingGuidance {get;set;}="";
}
public sealed class SaveAssignmentRequest
{
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [StringLength(24000)] public string Instructions { get; set; } = "";
    public DateTimeOffset? OpensAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    [Required, MinLength(1), MaxLength(20)] public List<AssignmentQuestionInput> Questions { get; set; } = [];
    public string? ExpectedEditVersion { get; set; }
    public bool AllowRevisions { get; set; }
    [Range(0.01,999999)] public decimal MaxScore { get; set; } = 100;
    public Guid? RubricId { get; set; }
    public List<LearningRequirementInput> LearningRequirements {get;set;}=[];
}
public sealed class AnswerInput
{
    public Guid QuestionId { get; set; }
    [Required(AllowEmptyStrings = true), StringLength(16000)] public string Text { get; set; } = "";
}
public sealed class SaveSubmissionRequest
{
    [Range(0, int.MaxValue)] public int ExpectedVersion { get; set; }
    [Required, MaxLength(20)] public List<AnswerInput> Answers { get; set; } = [];
    public bool Submit { get; set; }
}
public sealed record AssignmentQuestion(Guid Id, string PromptText, string? StageCode, int SortOrder,
    string QuestionType = "essay", IReadOnlyList<string>? Options = null, IReadOnlyList<int>? StandardAnswer = null,decimal? Points=null,string? GradingGuidance=null);
public sealed record AssignmentDetail(Guid Id, Guid CourseId, string CourseTitle, string CourseRole, string CourseStatus,
    string Title, string Instructions, string Status, DateTimeOffset? OpensAt, DateTimeOffset? DueAt,
    IReadOnlyList<AssignmentQuestion> Questions, string EditVersion, bool AllowRevisions = false, decimal MaxScore = 100, Guid? RubricId = null, bool IsPractice = false, bool IsQuiz = false,LearningGate? LearningGate=null);
public sealed record AssignmentListItem(Guid Id, string Title, string Status, DateTimeOffset? OpensAt,
    DateTimeOffset? DueAt, int QuestionCount, string? MyStatus, int SubmittedCount);
public sealed record AnswerRevision(Guid Id, int RevisionNo, string Text, DateTimeOffset CreatedAt, bool IsSubmitted);
public sealed record SubmissionAnswer(Guid QuestionId, string Text, Guid? CurrentRevisionId, IReadOnlyList<AnswerRevision> Revisions);
public sealed record SubmissionDetail(Guid? Id, Guid AssignmentId, Guid StudentUserId, string StudentName,
    string Status, int Version, DateTimeOffset? SubmittedAt, IReadOnlyList<SubmissionAnswer> Answers);
public sealed record SubmissionListItem(Guid Id, Guid StudentUserId, string StudentName, DateTimeOffset SubmittedAt, int AnswerCount, string GradingStatus = "ungraded");
