using AiTeachingAssistant.Models;
namespace AiTeachingAssistant.Services;
public static class GradebookPolicy
{
    public static void Validate(GradeItemInput input)
    {
        if(string.IsNullOrWhiteSpace(input.Title)||input.Title.Length>200 || input.Category is not ("offline_assignment" or "assignment" or "quiz" or "midterm" or "final" or "other") || input.MaxScore is <0.01m or >999999m || Math.Round(input.MaxScore,2)!=input.MaxScore || input.Weight is <0.001m or >999999m || Math.Round(input.Weight,3)!=input.Weight)
            throw new AssignmentWorkflowException(400,"請填寫有效名稱、類型、滿分（兩位小數）與正數權重（三位小數）。");
    }
    public static void ValidateScore(decimal? score,decimal max)
    {
        if(score.HasValue && (score<0 || score>max || Math.Round(score.Value,2)!=score.Value))
            throw new AssignmentWorkflowException(400,"得分須介於 0 與滿分之間，最多兩位小數；空白表示未登錄。");
    }
    public static GradebookSummary Summarize(IReadOnlyList<GradeItem> items,IReadOnlyList<GradeStudent> students,IReadOnlyList<GradeScore> scores)
    {
        // Average at full precision, then round for display. Missing scores never become zero.
        var itemMap=items.ToDictionary(x=>x.Id);
        var means=students.Select(student=>{
            var recorded=scores.Where(s=>s.Score.HasValue && s.StudentId==student.Id && itemMap.ContainsKey(s.ItemId)).ToArray();
            decimal? mean=recorded.Length==0?null:recorded.Sum(s=>s.Score!.Value/itemMap[s.ItemId].MaxScore*100*itemMap[s.ItemId].Weight)/recorded.Sum(s=>itemMap[s.ItemId].Weight);
            return (student.Id,Mean:mean,Count:recorded.Length);
        }).ToArray();
        var valid=means.Where(x=>x.Mean.HasValue).ToArray();
        return new(means.Select(x=>new StudentAverage(x.Id,x.Mean.HasValue?Math.Round(x.Mean.Value,2,MidpointRounding.AwayFromZero):null,x.Count)).ToArray(),
            items.Select(item=>{var values=scores.Where(s=>s.Score.HasValue && s.ItemId==item.Id && students.Any(u=>u.Id==s.StudentId)).ToArray();return new ItemAverage(item.Id,values.Length==0?null:Math.Round(values.Average(s=>s.Score!.Value/item.MaxScore*100),2,MidpointRounding.AwayFromZero),values.Length);}).ToArray(),
            valid.Length==0?null:Math.Round(valid.Average(x=>x.Mean!.Value),2,MidpointRounding.AwayFromZero),valid.Length);
    }
}
