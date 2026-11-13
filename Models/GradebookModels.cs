using System.ComponentModel.DataAnnotations;
namespace AiTeachingAssistant.Models;

public sealed class GradeItemInput
{
    [Required,StringLength(200)] public string Title { get; set; } = "";
    public string Category { get; set; } = "offline_assignment";
    [Range(0.01,999999)] public decimal MaxScore { get; set; } = 100;
    // Accept legacy payloads, but course-level items always contribute equally.
    public decimal Weight { get => 1; set { } }
    public int ExpectedVersion { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
}
public sealed class GradeScoreInput
{
    public bool Publish {get;set;}
    public Guid StudentId { get; set; }
    public decimal? Score { get; set; }
    [Range(0,int.MaxValue)] public int ExpectedVersion { get; set; }
}
public sealed record GradeItem(Guid Id,string Title,string Category,decimal MaxScore,decimal Weight,int Version,Guid? SourceAssignmentId=null,Guid? SourceReportId=null,DateTimeOffset? ScheduledAt=null);
public sealed record GradeScore(Guid ItemId,Guid StudentId,decimal? Score,int Version,decimal? PublishedScore=null);
public sealed record GradeStudent(Guid Id,string DisplayName,string? StudentNumber);
public sealed record StudentAverage(Guid StudentId,decimal? Average,int RecordedCount);
public sealed record ItemAverage(Guid ItemId,decimal? Average,int RecordedCount);
public sealed record GradebookSummary(IReadOnlyList<StudentAverage> Students,IReadOnlyList<ItemAverage> Items,decimal? ClassAverage,int GradedStudentCount);
