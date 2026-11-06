using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
namespace AiTeachingAssistant.Tests;
public class GradebookPolicyTests
{
    private static GradeItem Item(Guid id,decimal max,decimal weight)=>new(id,"item","quiz",max,weight,1);
    [Fact]public void NormalizesDifferentMaxScoresAndWeightsBeforeAveraging()
    {
        var first=Guid.NewGuid();var second=Guid.NewGuid();var student=Guid.NewGuid();
        var summary=GradebookPolicy.Summarize([Item(first,20,30),Item(second,100,70)],[new(student,"A",null)],[new(first,student,10,1),new(second,student,80,1)]);
        Assert.Equal(71m,summary.Students.Single().Average);Assert.Equal(71m,summary.ClassAverage);
    }
    [Fact]public void MissingAndClearedScoresAreExcludedButZeroCounts()
    {
        var first=Guid.NewGuid();var second=Guid.NewGuid();var a=Guid.NewGuid();var b=Guid.NewGuid();var c=Guid.NewGuid();
        var summary=GradebookPolicy.Summarize([Item(first,20,30),Item(second,100,70)],[new(a,"A",null),new(b,"B",null),new(c,"C",null)],[new(first,a,10,1),new(second,a,80,1),new(first,b,0,1),new(second,b,null,2)]);
        Assert.Equal(35.5m,summary.ClassAverage);Assert.Equal(2,summary.GradedStudentCount);Assert.Equal(0m,summary.Students.Single(x=>x.StudentId==b).Average);Assert.Null(summary.Students.Single(x=>x.StudentId==c).Average);
        Assert.Equal(25m,summary.Items.Single(x=>x.ItemId==first).Average);
    }
    [Fact]public void ClassAverageUsesEachStudentMeanOnce()
    {
        var first=Guid.NewGuid();var second=Guid.NewGuid();var a=Guid.NewGuid();var b=Guid.NewGuid();
        var summary=GradebookPolicy.Summarize([Item(first,100,1),Item(second,100,1)],[new(a,"A",null),new(b,"B",null)],[new(first,a,40,1),new(second,a,60,1),new(first,b,100,1)]);
        Assert.Equal(75m,summary.ClassAverage);
    }
    [Fact]public void RoundsHalfUpOnlyAfterCalculatingTheMean()
    {
        var item=Guid.NewGuid();var student=Guid.NewGuid();var summary=GradebookPolicy.Summarize([Item(item,20000,1)],[new(student,"A",null)],[new(item,student,1,1)]);
        Assert.Equal(0.01m,summary.ClassAverage);Assert.Equal(0.01m,summary.Students.Single().Average);
    }
    [Fact]public void EmptyGradebookHasNoFabricatedZeroAverage()=>Assert.Null(GradebookPolicy.Summarize([],[],[]).ClassAverage);
    [Theory][InlineData(0)][InlineData(-1)]public void RejectsNonPositiveWeight(decimal weight)=>Assert.Throws<AssignmentWorkflowException>(()=>GradebookPolicy.Validate(new(){Title="test",Weight=weight}));
    [Theory][InlineData(-1)][InlineData(21)][InlineData(1.234)]public void RejectsOutOfRangeOrOverPreciseScore(decimal score)=>Assert.Throws<AssignmentWorkflowException>(()=>GradebookPolicy.ValidateScore(score,20));
}
