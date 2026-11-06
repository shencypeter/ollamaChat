using AiTeachingAssistant.Models;
namespace AiTeachingAssistant.Services;
public static class TeachingPolicy
{
    public static decimal RubricScore(TeachingRubric rubric,IReadOnlyList<CriterionScore> scores,decimal maximum)
    {
        if(scores.Count!=rubric.Criteria.Count || scores.Select(s=>s.CriterionId).Distinct().Count()!=scores.Count ||
            scores.Any(s=>s.Level is <1 or >4 || !rubric.Criteria.Any(c=>c.Id==s.CriterionId)))
            throw new AssignmentWorkflowException(400,"請為每項規準選擇 1–4 級，且不得包含其他規準。");
        return Math.Round(scores.Sum(s=>rubric.Criteria.Single(c=>c.Id==s.CriterionId).Weight*s.Level)/
            (rubric.Criteria.Sum(c=>c.Weight)*4)*maximum,2,MidpointRounding.AwayFromZero);
    }
    public static void ValidateGrade(decimal score,decimal maximum)
    { if(score<0 || score>maximum || Math.Round(score,2)!=score)throw new AssignmentWorkflowException(400,"分數必須介於 0 與作業滿分之間，最多兩位小數。"); }
}
