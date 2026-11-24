using AiTeachingAssistant.Services;
namespace AiTeachingAssistant.Tests;

public sealed class CaseClauseEvidenceTests
{
    [Theory][InlineData(",")][InlineData("，")][InlineData("、")][InlineData(";")][InlineData("；")][InlineData(":")][InlineData("：")][InlineData(".")][InlineData("。")][InlineData("!")][InlineData("！")][InlineData("?")][InlineData("？")][InlineData("\n")]
    public void QuotesActualClausesAcrossPunctuation(string punctuation)
    {
        var source="He was 15"+punctuation+" had surgery"+punctuation+" ADL needs help😀";
        var candidates=CaseClauseEvidence.Candidates(source,160);
        Assert.Equal(new[]{"He was 15","had surgery","ADL needs help😀"},candidates);Assert.All(candidates,text=>Assert.Contains(text,source));
    }
    [Fact] public void KeepsDecimalsDatesTimesAbbreviationsAndParentheticalCommasIntact()
    {
        const string source="Dr. Lin at 14:30, dose 2.5 mg; date 2026.1.3, 1,000 steps; MMT（muscle, power） and ADL (a, b). More facts";
        Assert.Equal(new[]{"Dr. Lin at 14:30","dose 2.5 mg","date 2026.1.3","1,000 steps","MMT（muscle, power） and ADL (a, b)","More facts"},CaseClauseEvidence.Candidates(source,160));
    }
    [Fact] public void DeduplicatesChoicesWithoutChangingRepeatedSourceOccurrencesOrInventingLabels()
    {
        const string source="😀can eat，needs help；😀can eat。";
        Assert.Equal(new[]{"😀can eat","needs help"},CaseClauseEvidence.Candidates(source,160));
        var schema=CaseAnnotationContract.SchemaFor(true,original:source);Assert.Contains("enum",schema.GetRawText());
        var parsed=CaseAnnotationContract.Parse("{\"feedback\":\"synthetic\",\"annotations\":[{\"text\":\"😀can eat\",\"occurrence\":1,\"codes\":[\"A2a\"],\"explanation\":\"synthetic\",\"status\":\"unverified\"}]}",source);
        Assert.Equal(source.LastIndexOf("😀can eat",StringComparison.Ordinal),Assert.Single(parsed.Annotations).Start);
    }
    [Fact] public void KeepsShortFieldLabelsWithTheirEvidenceWhileSplittingCompleteClausesAtColons()
    {
        Assert.Equal(new[]{"影像：右額葉挫傷","ADL: needs help","He was 15","had surgery"},CaseClauseEvidence.Candidates("影像：右額葉挫傷；ADL: needs help；He was 15: had surgery",160));
    }
    [Fact] public void OversizedOrSingleClauseRetainsFreeExcerptsRatherThanTruncatingTheAnswer()
    {
        foreach(var source in new[]{"one clause",new string('測',161)+", short",string.Join(',',Enumerable.Range(0,129))}){
            Assert.Empty(CaseClauseEvidence.Candidates(source,160));var schema=CaseAnnotationContract.SchemaFor(true,original:source);
            Assert.False(schema.GetProperty("properties").GetProperty("annotations").GetProperty("items").GetProperty("properties").GetProperty("text").TryGetProperty("enum",out _));
        }
    }
}
