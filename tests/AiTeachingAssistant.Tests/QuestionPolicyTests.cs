using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using AiTeachingAssistant.Controllers;
using Microsoft.AspNetCore.Mvc;
namespace AiTeachingAssistant.Tests;
public class QuestionPolicyTests
{
    [Theory][InlineData("single")][InlineData("true_false")][InlineData("multiple")]
    public void ObjectiveQuestionsRequireValidSecretKey(string type){Assert.Throws<AssignmentWorkflowException>(()=>QuestionPolicy.Validate(new(){QuestionType=type,Options=["A","B"],StandardAnswer=[]}));QuestionPolicy.Validate(new(){QuestionType=type,Options=["A","B"],StandardAnswer=[0]});}
    [Theory][InlineData("[2]")][InlineData("[0,0]")][InlineData("[0,1]")][InlineData("not-json")]
    public void SingleChoiceRejectsInvalidSubmittedAnswers(string text)=>Assert.Throws<AssignmentWorkflowException>(()=>QuestionPolicy.ValidateAnswer(new(Guid.NewGuid(),"Q",null,1,"single",["A","B"]),text,true));
    [Fact]public void UnansweredObjectiveIsValidAndWillScoreZero()=>QuestionPolicy.ValidateAnswer(new(Guid.NewGuid(),"Q",null,1,"single",["A","B"]),"[]",true);
    [Fact]public void EssaysAndMultipleSelectionsRemainSupported(){QuestionPolicy.Validate(new(){PromptText="Essay"});QuestionPolicy.ValidateAnswer(new(Guid.NewGuid(),"Essay",null,1),"申論答案",true);QuestionPolicy.ValidateAnswer(new(Guid.NewGuid(),"Q",null,1,"multiple",["A","B"]),"[1,0]",true);}
    [Fact]public void AssignmentAiEndpointIsDisabled(){var result=Assert.IsType<ObjectResult>(new FormativeFeedbackController().Disabled());Assert.Equal(403,result.StatusCode);}
}
