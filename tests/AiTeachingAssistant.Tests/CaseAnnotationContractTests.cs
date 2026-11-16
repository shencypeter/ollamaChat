using System.Text.Json;
using AiTeachingAssistant.Services;

namespace AiTeachingAssistant.Tests;

public class CaseAnnotationContractTests
{
    [Fact] public void ExplicitQuoteOccurrenceResolvesExactUtf16WithoutGuessingAnOffset()
    {
        const string original="😀55歲；55歲";
        var result=CaseAnnotationContract.Validate(original,Entries(new{text="55歲",occurrence=1,codes=new[]{"A1a"},explanation="年齡",status="unverified"}));
        var annotation=Assert.Single(result);Assert.Equal(6,annotation.Start);Assert.Equal(9,annotation.End);Assert.Equal("55歲",annotation.Text);
        Assert.Empty(CaseAnnotationContract.Validate(original,Entries(new{text="55歲",occurrence=2,codes=new[]{"A1a"}})));
        Assert.Empty(CaseAnnotationContract.Validate(original,Entries(new{text="55歲",occurrence=1,start=0,end=3,codes=new[]{"A1a"}})));
    }
    private static JsonElement Entries(params object[] annotations)=>JsonSerializer.SerializeToElement(annotations);
    private static object Ann(int start,int end,string text,string[]? codes=null,string status="unverified")=>new{start,end,text,codes=codes??["A1a"],explanation="分類辨識不是事實核實",status};

    [Fact] public void ValidatesMixedUnicodeRepeatedAndOverlappingSpansWithoutChangingOriginal()
    {
        const string original="😀55歲 ADL；55歲";
        var result=CaseAnnotationContract.Validate(original,Entries(Ann(2,5,"55歲"),Ann(2,9,"55歲 ADL",["A1a","A2a"]),Ann(10,13,"55歲")));
        Assert.Equal(3,result.Count);Assert.Equal(10,result[2].Start);Assert.Equal(new[]{"A1a","A2a"},result[1].Codes);
        Assert.All(result,a=>Assert.Equal(a.Text,original[a.Start..a.End]));
    }
    [Fact] public void RejectsInvalidOffsetsUnknownCodesAndSplitSurrogatesRatherThanGuessing()
    {
        const string original="😀55歲";
        var result=CaseAnnotationContract.Validate(original,Entries(Ann(-1,2,"😀"),Ann(0,1,"\ud83d"),Ann(1,2,"\ude00"),Ann(2,99,"55歲"),Ann(2,5,"年齡"),Ann(2,2,""),Ann(2,5,"55歲",["A2c","B2c","C5a","X1"])));
        Assert.Empty(result);
    }
    [Fact] public void ClassificationPreservesUncertaintyAndNeverPromotesUnknownStatusToCorrect()
    {
        var result=CaseAnnotationContract.Validate("移位獨立",Entries(Ann(0,4,"移位獨立",["C5"],"unsupported"),Ann(0,4,"移位獨立",["C5"],"correct")));
        Assert.Equal("unsupported",result[0].Status);Assert.Equal("unverified",result[1].Status);
    }
    [Fact] public void EmptyMalformedAndExcessiveCollectionsAreBounded()
    {
        Assert.Empty(CaseAnnotationContract.Validate("原文",Entries()));
        Assert.Empty(CaseAnnotationContract.Validate("原文",JsonSerializer.SerializeToElement("bad")));
        Assert.Empty(CaseAnnotationContract.Validate("原文",Entries(new{start="0",end=2,text="原文",codes=new[]{"A1a"}})));
        Assert.Equal(48,CaseAnnotationContract.Validate("55歲",Entries(Enumerable.Range(0,100).Select(_=>Ann(0,3,"55歲")).ToArray())).Count);
    }
    [Fact] public void FeedbackEnvelopeKeepsTextAndDiscardsBadAnnotations()
    {
        var output=CaseAnnotationContract.Parse("""{"feedback":"已掌握的部分：辨識年齡。","annotations":[{"start":0,"end":3,"text":"55歲","codes":["A1a"],"explanation":"年齡","status":"unverified"},{"start":9,"end":99,"text":"bad","codes":["A1a"]}]}""","55歲");
        Assert.Equal("已掌握的部分：辨識年齡。",output.Feedback);Assert.Single(output.Annotations);
    }
    [Theory]
    [InlineData("SENSITIVE invalid")][InlineData("{\"feedback\":\"\",\"annotations\":[]}")][InlineData("{\"feedback\":\"valid\"}")]
    public void InvalidEnvelopeProducesOnlySafeGenericError(string body)
    {
        var error=Assert.Throws<CaseChatException>(()=>CaseAnnotationContract.Parse(body,"原文"));
        Assert.Equal(502,error.StatusCode);Assert.DoesNotContain("SENSITIVE",error.ToString());Assert.Null(error.InnerException);
    }
}
