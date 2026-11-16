using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;

namespace AiTeachingAssistant.Tests;

public class StudioPromptAlignmentTests
{
    [Fact] public void StudioIncludesAuthoritativeCurrentPhaseIndependentlyOfStudentText()
    {
        var answer=new StudioAnswer(Guid.NewGuid(),"illnessCourse",1,"病前自行搭車及管理帳務。",DateTimeOffset.UtcNow);
        var messages=StudioFeedbackContract.Build(Case(),answer,"簡短回饋");
        using var input=JsonDocument.Parse(messages.Last().Content);
        Assert.Equal("B",input.RootElement.GetProperty("currentPhase").GetString());
        Assert.Equal("B 疾病病程",input.RootElement.GetProperty("currentStageLabel").GetString());
        Assert.Equal(answer.Text,input.RootElement.GetProperty("answerText").GetString());
        Assert.StartsWith("本次作答階段：B 疾病病程",messages[0].Content);
    }
    private const string Core="""{"summary":"核對已提供的資料","supportedPoints":[],"improvements":[],"nextSteps":["依原文釐清時間點"]}""";
    private static StudioCase Case()=>new(Guid.NewGuid(),Guid.NewGuid(),"合成課程",Guid.NewGuid(),1,"合成案例","測試來源原文",StudioFeedbackContract.Stages.ToDictionary(s=>s,_=>"測試結構提示"),StudioFeedbackContract.Stages.ToDictionary(s=>s,_=>"測試內部重點"),true,null);
    private static string WithAnnotations(object annotations)=>Core[..^1]+",\"annotations\":"+JsonSerializer.Serialize(annotations)+"}";

    [Fact]
    public void LegacyFeedbackStillParsesAndDoesNotInventAnnotations()
    {
        var value=StudioFeedbackContract.Parse(Core,"學生作答");
        Assert.Null(value.Annotations);
        Assert.Single(value.NextSteps);
    }

    [Fact]
    public void FeedbackSchemaExtendsExistingFieldsWithoutScores()
    {
        var schema=StudioFeedbackContract.Schema;
        Assert.Equal(new[]{"summary","supportedPoints","improvements","nextSteps"},schema.GetProperty("required").EnumerateArray().Select(x=>x.GetString()));
        var properties=schema.GetProperty("properties");
        Assert.Equal("array",properties.GetProperty("annotations").GetProperty("type").GetString());
        Assert.False(properties.TryGetProperty("score",out _));
        Assert.False(properties.TryGetProperty("thinking",out _));
    }

    [Fact]
    public void AnnotationsUseExactSavedUtf16TextAndFilterInvalidEntries()
    {
        const string original="🙂目前需協助，目前需協助";
        var raw=WithAnnotations(new object[]{
            new{annotationId="first",start=2,end=7,text="目前需協助",codes=new[]{"C5"},explanation="功能分類",status="unverified"},
            new{annotationId="second",start=8,end=13,text="目前需協助",codes=new[]{"C5"},explanation="仍須核對原文",status="unsupported"},
            new{annotationId="wrong-text",start=2,end=7,text="目前能自理",codes=new[]{"C5"},explanation="錯誤位置",status="unverified"},
            new{annotationId="split-emoji",start=1,end=2,text="\ude42",codes=new[]{"C5"},explanation="不得切斷代理對",status="unverified"},
            new{annotationId="unknown",start=2,end=7,text="目前需協助",codes=new[]{"C999"},explanation="未知代碼",status="unverified"}
        });
        var parsed=StudioFeedbackContract.Parse(raw,original);
        var annotations=Assert.IsAssignableFrom<IReadOnlyList<CaseTextAnnotation>>(parsed.Annotations);
        Assert.Equal(new[]{2,8},annotations.Select(x=>x.Start));
        Assert.Equal("unsupported",annotations[1].Status);
        Assert.Equal("核對已提供的資料",parsed.Summary);
        Assert.Empty(StudioFeedbackContract.Parse(raw,"已修訂答案").Annotations!);
    }

    [Fact]
    public void AnnotationCannotBeTrustedWithoutTheSavedAnswer()
    {
        var raw=WithAnnotations(new[]{new{annotationId="a",start=0,end=2,text="原文",codes=new[]{"A1"},explanation="辨識分類",status="unverified"}});
        Assert.Empty(StudioFeedbackContract.Parse(raw).Annotations!);
        Assert.Empty(StudioFeedbackContract.Parse(WithAnnotations(new{invalid="shape"}),"原文").Annotations!);
    }

    [Theory]
    [InlineData("premorbid","A1")]
    [InlineData("illnessCourse","B1")]
    [InlineData("functionalAssessment","C1")]
    [InlineData("psychosocial","D1")]
    public void EachStudioStageUsesProfessorRulesAndExistingDataEnvelope(string stage,string code)
    {
        var c=Case();var answer=new StudioAnswer(Guid.NewGuid(),stage,2,"本次修訂原文",DateTimeOffset.UtcNow);
        var messages=StudioFeedbackContract.Build(c,answer,"管理者維護風格");
        Assert.Equal(2,messages.Count);
        Assert.Equal("system",messages[0].Role);
        Assert.Contains(code,messages[0].Content);
        Assert.Contains("UTF-16",messages[0].Content);
        Assert.Contains("caseFacts",messages[0].Content);
        Assert.Contains("supportedPoints",messages[0].Content);
        Assert.Contains("分類辨識不代表",messages[0].Content);
        using var payload=JsonDocument.Parse(messages[1].Content);
        Assert.Equal(c.Material,payload.RootElement.GetProperty("caseFacts").GetString());
        Assert.Equal(answer.Text,payload.RootElement.GetProperty("answerText").GetString());
        Assert.Equal(answer.Id,payload.RootElement.GetProperty("answerVersionId").GetGuid());
        Assert.Equal("測試內部重點",payload.RootElement.GetProperty("referenceChecklist").GetString());
        Assert.DoesNotContain(c.Material,messages[0].Content);
    }

    [Fact]
    public void StudioExposesTheSharedClassificationCatalog()
    {
        var serialized=JsonSerializer.SerializeToElement(Case(),StudioFeedbackContract.Json);
        var codes=serialized.GetProperty("classifications").EnumerateArray().Select(x=>x.GetProperty("code").GetString());
        Assert.Contains("A1",codes);Assert.Contains("B1",codes);Assert.Contains("C1",codes);Assert.Contains("D1",codes);
    }
}
