using AiTeachingAssistant.Services;
namespace AiTeachingAssistant.Tests;
public class DemoPresentationTextTests
{
    [Fact] public void RemovesOnlyKnownSeedFeedbackFraming()
    {
        Assert.Equal("已提出主要證據。",DemoPresentationText.Clean("feedback","Codex 示範批改：已提出主要證據。 此結果為展示用教師草稿，請授課教師確認後再發布。"));
        Assert.Equal("已回應核心概念。",DemoPresentationText.Clean("published_feedback","Codex 示範批改：已回應核心概念。"));
        Assert.Equal("學生比較 Codex 與其他工具。",DemoPresentationText.Clean("feedback","學生比較 Codex 與其他工具。"));
    }
    [Fact] public void NormalizesKnownFileAndPeerLabelsWithoutChangingTheirMeaning()
    {
        Assert.Equal("作業報告.pdf",DemoPresentationText.Clean("file_name","codex-demo-submission.pdf"));
        Assert.Equal("研究Codex.pdf",DemoPresentationText.Clean("file_name","研究Codex.pdf"));
        Assert.Equal("能完成分工。",DemoPresentationText.Clean("comment","示範互評：能完成分工。"));
        Assert.Equal("協助核對資料與整理圖表。",DemoPresentationText.Clean("comment","示範：協助核對資料與整理圖表。"));
        Assert.Equal("希望教師參考實際分工。",DemoPresentationText.Clean("explanation","示範：希望教師參考實際分工。"));
    }
    [Fact] public void IsIdempotentAndPreservesUnrelatedTeacherText()
    {
        const string original="Codex 示範批改：論點完整。";
        var clean=DemoPresentationText.Clean("feedback",original);
        Assert.Equal(clean,DemoPresentationText.Clean("feedback",clean));
        Assert.Equal("請在課堂示範互評流程。",DemoPresentationText.Clean("comment","請在課堂示範互評流程。"));
    }
}
