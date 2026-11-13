using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
namespace AiTeachingAssistant.Tests;

public class OfflineExamGradeTests
{
    private readonly Guid item=Guid.NewGuid(),a=Guid.NewGuid(),b=Guid.NewGuid();
    private GradeScore[] Current()=>[new(item,a,0,2),new(item,b,80,1)];
    private OfflineExamBatchInput Input()=>new(){ExpectedItemVersion=3,Scores=[new(){StudentId=a,ExpectedVersion=2,Score=0},new(){StudentId=b,ExpectedVersion=1,Score=80}]};
    [Fact]public void CompleteRosterCanPublishRealZeroScores()=>OfflineExamGradePolicy.Validate(Input(),3,[a,b],Current(),100,true);
    [Fact]public void DraftsAllowNullWithoutPublishingIt(){var input=Input();input.Scores[1].Score=null;OfflineExamGradePolicy.Validate(input,3,[a,b],Current(),100,false);}
    [Fact]public void MissingDraftBlocksWholeExamPublication(){var scores=Current();scores[1]=scores[1] with{Score=null};var error=Assert.Throws<AssignmentWorkflowException>(()=>OfflineExamGradePolicy.Validate(Input(),3,[a,b],scores,100,true));Assert.Equal(409,error.StatusCode);}
    [Fact]public void EmptyClassCannotBePublished(){var error=Assert.Throws<AssignmentWorkflowException>(()=>OfflineExamGradePolicy.Validate(new(){ExpectedItemVersion=3},3,[],[],100,true));Assert.Equal(409,error.StatusCode);}
    [Fact]public void NewOrDepartedStudentRequiresRosterReload(){var input=Input();input.Scores.RemoveAt(1);Assert.Throws<AssignmentWorkflowException>(()=>OfflineExamGradePolicy.Validate(input,3,[a,b],Current(),100,false));input.Scores.Add(new(){StudentId=Guid.NewGuid(),ExpectedVersion=0});Assert.Throws<AssignmentWorkflowException>(()=>OfflineExamGradePolicy.Validate(input,3,[a,b],Current(),100,true));}
    [Fact]public void DuplicateStudentCannotReplaceMissingStudent(){var input=Input();input.Scores[1].StudentId=a;Assert.Throws<AssignmentWorkflowException>(()=>OfflineExamGradePolicy.Validate(input,3,[a,b],Current(),100,false));}
    [Fact]public void StaleScoreOrExamVersionBlocksEntireBatch(){var input=Input();input.ExpectedItemVersion=2;Assert.Throws<AssignmentWorkflowException>(()=>OfflineExamGradePolicy.Validate(input,3,[a,b],Current(),100,true));input.ExpectedItemVersion=3;input.Scores[0].ExpectedVersion=1;Assert.Throws<AssignmentWorkflowException>(()=>OfflineExamGradePolicy.Validate(input,3,[a,b],Current(),100,false));}
    [Theory][InlineData(-1)][InlineData(101)][InlineData(1.234)]public void InvalidDraftScoreRejectsBatch(decimal score){var input=Input();input.Scores[1].Score=score;Assert.Throws<AssignmentWorkflowException>(()=>OfflineExamGradePolicy.Validate(input,3,[a,b],Current(),100,false));}
}
