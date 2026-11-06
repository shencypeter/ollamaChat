using AiTeachingAssistant.Services;
namespace AiTeachingAssistant.Tests;
public class QuizGradingTests
{
    [Theory]
    [InlineData("[0,2]",true)][InlineData("[2,0]",true)][InlineData("[0]",false)]
    [InlineData("[0,1,2]",false)][InlineData("[1,3]",false)][InlineData("[]",false)]
    [InlineData("",false)][InlineData("[0,0,2]",false)]
    public void MultipleChoiceIsAllOrNothingAndOrderIndependent(string answer,bool expected)=>Assert.Equal(expected,QuizGrading.Correct(answer,[0,2]));
    [Fact]public void AllocationPreservesTotalAndHonorsExplicitPoints()
    {
        Assert.Equal(new decimal[]{33.33m,33.33m,33.34m},QuizGrading.Allocate(100,[null,null,null]));
        Assert.Equal(new decimal[]{35,30,35},QuizGrading.Allocate(100,[null,30,null]));
        Assert.Throws<AssignmentWorkflowException>(()=>QuizGrading.Allocate(100,[30,30]));
    }
    [Fact]public void TeacherCanOnlyScoreEveryEssayWithinItsPoints()
    {
        var objective=Guid.NewGuid();var essay=Guid.NewGuid();QuizMark[] marks=[new(objective,"multiple",40,0,false),new(essay,"essay",60,null,null)];
        Assert.Equal(45,QuizGrading.Total(marks,new Dictionary<Guid,decimal>{{essay,45}}));
        Assert.Throws<AssignmentWorkflowException>(()=>QuizGrading.Total(marks,new Dictionary<Guid,decimal>()));
        Assert.Throws<AssignmentWorkflowException>(()=>QuizGrading.Total(marks,new Dictionary<Guid,decimal>{{essay,61}}));
        Assert.Throws<AssignmentWorkflowException>(()=>QuizGrading.Total(marks,new Dictionary<Guid,decimal>{{objective,40},{essay,45}}));
    }
}
