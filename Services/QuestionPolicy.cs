using System.Text.Json;
using AiTeachingAssistant.Models;
namespace AiTeachingAssistant.Services;
public static class QuestionPolicy
{
    public static void Validate(AssignmentQuestionInput q)
    {
        if(q.QuestionType is not ("essay" or "true_false" or "single" or "multiple")) Bad("題型無效。");
        if(q.Options is null || q.StandardAnswer is null) Bad("選項與標準答案不可為 null。");
        if(q.QuestionType=="essay") { if(q.StandardAnswer!.Count!=0) Bad("申論題不設定客觀標準答案。"); return; }
        var count=q.QuestionType=="true_false"?2:q.Options!.Count;
        if(count is <2 or >20 || (q.QuestionType!="true_false" && q.Options!.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>1000))) Bad("客觀題需 2 至 20 個有效選項。");
        var key=q.StandardAnswer!;
        if(key.Count==0 || key.Distinct().Count()!=key.Count || key.Any(x=>x<0||x>=count) || (q.QuestionType!="multiple" && key.Count!=1)) Bad("請設定有效標準答案；單選與是非題只能有一個答案。");
    }
    public static void ValidateAnswer(AssignmentQuestion q,string text,bool submit)
    {
        if(q.QuestionType=="essay" || string.IsNullOrEmpty(text)) return;
        try {
            var selected=JsonSerializer.Deserialize<int[]>(text);
            if(selected is null || selected.Distinct().Count()!=selected.Length || selected.Any(x=>x<0||x>=(q.Options?.Count??0)) || (q.QuestionType!="multiple" && selected.Length>1)) Bad("請選擇有效答案。");
        } catch(JsonException) { Bad("選擇題答案格式無效。"); }
    }
    private static void Bad(string message)=>throw new AssignmentWorkflowException(400,message);
}
