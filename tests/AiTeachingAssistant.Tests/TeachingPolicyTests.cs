using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
namespace AiTeachingAssistant.Tests;
public class TeachingPolicyTests
{
    private static readonly Guid A=Guid.NewGuid(),B=Guid.NewGuid();
    private static TeachingRubric Rubric=>new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,"測試","draft",
        [new(A,"證據","",1,["1","2","3","4"]),new(B,"推理","",3,["1","2","3","4"])]);
    [Fact] public void WeightedLevelsScaleToAssignmentMaximum()=>Assert.Equal(62.5m,TeachingPolicy.RubricScore(Rubric,[new(A,4),new(B,2)],100));
    [Fact] public void HighestLevelsReceiveMaximum()=>Assert.Equal(20,TeachingPolicy.RubricScore(Rubric,[new(A,4),new(B,4)],20));
    [Fact] public void LowestLevelsAreQuarterOfMaximum()=>Assert.Equal(25,TeachingPolicy.RubricScore(Rubric,[new(A,1),new(B,1)],100));
    [Fact] public void MissingCriterionRejected()=>Assert.Throws<AssignmentWorkflowException>(()=>TeachingPolicy.RubricScore(Rubric,[new(A,4)],100));
    [Fact] public void DuplicateCriterionRejected()=>Assert.Throws<AssignmentWorkflowException>(()=>TeachingPolicy.RubricScore(Rubric,[new(A,4),new(A,2)],100));
    [Fact] public void ForeignCriterionRejected()=>Assert.Throws<AssignmentWorkflowException>(()=>TeachingPolicy.RubricScore(Rubric,[new(A,4),new(Guid.NewGuid(),2)],100));
    [Theory][InlineData(0)][InlineData(5)] public void InvalidLevelRejected(int level)=>Assert.Throws<AssignmentWorkflowException>(()=>TeachingPolicy.RubricScore(Rubric,[new(A,level),new(B,2)],100));
    [Theory][InlineData(-1)][InlineData(101)] public void InvalidDirectScoreRejected(decimal score)=>Assert.Throws<AssignmentWorkflowException>(()=>TeachingPolicy.ValidateGrade(score,100));
    [Fact] public void ZeroAndMaximumAreValid(){TeachingPolicy.ValidateGrade(0,100);TeachingPolicy.ValidateGrade(100,100);}
    [Fact] public void FractionalCentsRejected()=>Assert.Throws<AssignmentWorkflowException>(()=>TeachingPolicy.ValidateGrade(1.001m,100));
}
