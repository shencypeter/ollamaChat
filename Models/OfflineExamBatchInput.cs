using System.ComponentModel.DataAnnotations;
namespace AiTeachingAssistant.Models;
public sealed class OfflineExamBatchInput
{
    [Range(1,int.MaxValue)] public int ExpectedItemVersion {get;set;}
    [Required] public List<GradeScoreInput> Scores {get;set;}=[];
}
