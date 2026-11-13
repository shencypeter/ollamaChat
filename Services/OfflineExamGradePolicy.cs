using AiTeachingAssistant.Models;
namespace AiTeachingAssistant.Services;
public static class OfflineExamGradePolicy
{
    public static void Validate(OfflineExamBatchInput input,int itemVersion,IReadOnlyList<Guid> students,IReadOnlyList<GradeScore> current,decimal max,bool publish)
    {
        if(input.ExpectedItemVersion!=itemVersion)throw new AssignmentWorkflowException(409,"考試設定已被修改，請重新載入。");
        if(input.Scores is null||input.Scores.Any(score=>score is null)||input.Scores.Count!=students.Count||input.Scores.Select(score=>score.StudentId).Distinct().Count()!=students.Count||!input.Scores.Select(score=>score.StudentId).ToHashSet().SetEquals(students))
            throw new AssignmentWorkflowException(409,"學生名冊已變更或不完整，請重新載入全班名冊。");
        var scores=current.ToDictionary(score=>score.StudentId);
        foreach(var inputScore in input.Scores)
        {
            scores.TryGetValue(inputScore.StudentId,out var saved);
            if(inputScore.ExpectedVersion!=(saved?.Version??0))throw new AssignmentWorkflowException(409,"得分已被修改，請重新載入；本次整班操作未套用。");
            if(!publish)GradebookPolicy.ValidateScore(inputScore.Score,max);
            else if(saved?.Score is null)throw new AssignmentWorkflowException(409,"全班有效學生皆須完成登分，才能一次發布。空白不會視為 0 分。");
            else GradebookPolicy.ValidateScore(saved.Score,max);
        }
        if(publish&&students.Count==0)throw new AssignmentWorkflowException(409,"尚無有效學生，無法發布考試成績。");
    }
}
