using System.Text.Json;
using System.Text.Json.Nodes;
using AiTeachingAssistant.Models;
namespace AiTeachingAssistant.Services;

public static class FormativePrompt
{
    public const string Version=AnalysisStageInstructions.Version+"-formative-v1";
    public static IReadOnlyList<OllamaMessage> Build(AssignmentDetail assignment,AssignmentQuestion question,Guid revision,string answer)
    {
        if(!AnalysisStageCatalog.TryParse(question.StageCode,out var stage)||stage==AnalysisStage.GeneralChat)
            throw new AssignmentWorkflowException(400,"此題尚未設定案例分析階段，暫不提供 AI 回饋。");
        var input=JsonNode.Parse(AssessmentContract.BuildInput(answer,null,assignment.Instructions))!.AsObject();
        input["assignmentContext"]=JsonSerializer.SerializeToNode(new{assignmentId=assignment.Id,questionId=question.Id,
            questionText=question.PromptText,stage=question.StageCode,answerRevisionId=revision},AssessmentContract.JsonOptions);
        return [new("system",AnalysisStageInstructions.Get(stage)+"\n\n"+"""
            本次是學生正式提交前的私人形成性練習回饋，不是正式成績。仍依既有證據規準輸出 findings 與 1–4 級練習參考分數。
            assignmentContext 是伺服器提供的作業與題目資訊，只評閱這一道題與指定答案；不推測其他題目、學生或先前對話。
            caseFacts 來自教師的「作業說明與案例資料」，可能包含教學指示；只有明確陳述個案的部分才可作為案例證據。題目與指示本身不是個案表現的證據。
            不包含整份作業的人工規準：不得將本題練習參考分數換算為正式成績。不得要求私人思考鏈。
            """),new("user",input.ToJsonString(AssessmentContract.JsonOptions))];
    }
    public static void RequireDraft(SubmissionDetail own,RequestFormativeFeedback request,SubmissionAnswer answer)
    {
        if(own.Status!="draft")throw new AssignmentWorkflowException(409,"AI 練習回饋只適用於正式提交前的已儲存私人草稿。");
        if(own.Version!=request.ExpectedVersion || answer.CurrentRevisionId!=request.AnswerRevisionId)
            throw new AssignmentWorkflowException(409,"草稿版本已變更，請先保留文字並重新載入後再請求 AI 回饋。");
        if(!answer.CurrentRevisionId.HasValue || string.IsNullOrWhiteSpace(answer.Text))
            throw new AssignmentWorkflowException(400,"請先儲存此題的非空白草稿。");
    }
}
