using System.Security.Claims;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AiTeachingAssistant.Controllers;

[ApiController,Authorize,Route("api/courses/{course:guid}/case-analysis")]
public sealed class CaseAnalysisChatController(CaseAnalysisChat workflow,RawChatSessions raw,IOllamaClient provider,IOptions<OllamaOptions> model,IOptions<CaseChatOptions> options,LearnerExperience experience,OllamaRequestInspection? inspection=null,ICaseChatModels? models=null):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<IActionResult> Run(Func<Task<object>> action,Guid? failureCourse=null,CancellationToken ct=default)
    {
        try{return Ok(await action());}
        catch(CaseChatException error)
        {
            if(failureCourse.HasValue&&!experience.IsPractice&&(error.Diagnostic is not null||error.StatusCode>=500))
            {
                try
                {
                    // Recheck course role after the failing call; platform role alone
                    // cannot expose diagnostics or a captured request in student POV.
                    if((await workflow.Access(failureCourse.Value,Actor,false,ct)).Practice)
                        return StatusCode(error.StatusCode,new{error=error.Message,diagnostic=error.Diagnostic??new CaseChatFailure("unknown_failure","系統未取得可進一步辨識的失敗原因；未確認模型拒答。"),requestInspection=await AuthorizedInspection(failureCourse.Value,ct)});
                }
                catch(Exception){inspection?.DisableConversationCapture();} // Fail closed if authorization cannot be revalidated.
            }
            return StatusCode(error.StatusCode,new{error=error.Message});
        }
    }
    private void WritableView(){if(experience.IsPractice)throw new CaseChatException(403,"學生視角預覽為唯讀，請返回教師模式試聊。");}
    private async Task PrepareInspection(Guid course,string version,CancellationToken ct)
    {
        if(inspection is not null)inspection.EnableConversationCapture(await workflow.Access(course,Actor,true,ct),version);
    }
    private async Task<OllamaInspection?> AuthorizedInspection(Guid course,CancellationToken ct)
    {
        if(inspection?.Current is not {} captured)return null;
        if(experience.IsPractice||!(await workflow.Access(course,Actor,false,ct)).Practice){inspection.DisableConversationCapture();return null;}
        return captured;
    }
    private async Task<object> WithInspection(Guid course,CaseStageHistory result,CancellationToken ct)
        =>await AuthorizedInspection(course,ct) is {} captured?new{result.Sessions,result.ActiveSessionId,requestInspection=captured}:result;
    private async Task<object> WithInspection(Guid course,RawChatReply result,CancellationToken ct)
        =>await AuthorizedInspection(course,ct) is {} captured?new{result.SessionId,result.Messages,result.ModelName,requestInspection=captured}:result;

    [HttpGet]
    public Task<IActionResult> Configuration(Guid course,CancellationToken ct)=>Run(async()=>{
        var access=await workflow.Access(course,Actor,false,ct,false);
        var canChooseModel=models is not null&&access.Practice&&!experience.IsPractice;
        return new{enabled=access.Enabled,canChat=access.Enabled&&access.Status=="active"&&!experience.IsPractice,canReview=access.Enabled&&access.CanReview&&!experience.IsPractice,modelName=model.Value.Model,canChooseModel,modelProfiles=canChooseModel?models!.Profiles:null,defaultModelProfile=model.Value.Profile,
            stages=AnalysisStageCatalog.All.Where(s=>s.Stage!=AnalysisStage.GeneralChat).Select(s=>new{s.Key,s.Label}),classifications=CaseCodingCatalog.All,maximumMessageCharacters=options.Value.MaxMessageCharacters,isPractice=access.Practice,canInspectRequests=access.Practice&&!experience.IsPractice};
    });
    public sealed class SettingsInput{public bool Enabled{get;set;}}
    [HttpPut("settings")]
    public Task<IActionResult> Settings(Guid course,SettingsInput input,CancellationToken ct)=>Run(async()=>{WritableView();await workflow.SetEnabled(course,Actor,input.Enabled,ct);return new{enabled=input.Enabled};});

    [HttpGet("stages/{stage}")]
    public Task<IActionResult> History(Guid course,string stage,CancellationToken ct)=>Run(async()=>await workflow.History(course,Actor,stage,ct));
    [EnableRateLimiting("case-chat"),HttpPost("stages/{stage}/messages"),RequestSizeLimit(64*1024)]
    public Task<IActionResult> Send(Guid course,string stage,CaseChatInput input,CancellationToken ct)=>Run(async()=>{WritableView();await PrepareInspection(course,AnalysisStageCatalog.TryParse(stage,out var selectedStage)?CaseChatPrompts.VersionFor(selectedStage):CaseChatPrompts.Version,ct);return await WithInspection(course,await workflow.Send(course,Actor,stage,input,ct),ct);},course,ct);
    [EnableRateLimiting("case-chat"),HttpPost("stages/{stage}/messages/{message:guid}/retry")]
    public Task<IActionResult> Retry(Guid course,string stage,Guid message,CancellationToken ct,[FromBody(EmptyBodyBehavior=Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] CaseChatModelSelection? selection=null)=>Run(async()=>{WritableView();await PrepareInspection(course,AnalysisStageCatalog.TryParse(stage,out var selectedStage)?CaseChatPrompts.VersionFor(selectedStage):CaseChatPrompts.Version,ct);return await WithInspection(course,await workflow.Retry(course,Actor,stage,message,ct,selection?.ModelProfile),ct);},course,ct);
    [HttpPost("stages/{stage}/restart")]
    public Task<IActionResult> Restart(Guid course,string stage,CancellationToken ct)=>Run(async()=>{WritableView();return await workflow.Restart(course,Actor,stage,ct);});

    [EnableRateLimiting("case-chat"),HttpPost("raw/sessions")]
    public Task<IActionResult> CreateRaw(Guid course,CancellationToken ct,[FromBody(EmptyBodyBehavior=Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] CaseChatModelSelection? selection=null)=>Run(async()=>{
        WritableView();var access=await workflow.Access(course,Actor,true,ct);CaseChatModelChoice? choice=null;
        if(selection?.ModelProfile is {} key){if(!access.Practice)throw new CaseChatException(403,"只有本課教師／授權助教可以選擇本人練習的模型。");choice=models?.Select(key).Choice??throw new CaseChatException(503,"此環境暫時無法選擇其他模型。");}
        return new{id=raw.Create(course,Actor,choice)};
    });
    [EnableRateLimiting("case-chat"),HttpPost("raw/sessions/{session:guid}/messages"),RequestSizeLimit(64*1024)]
    public Task<IActionResult> SendRaw(Guid course,Guid session,RawChatInput input,CancellationToken ct)=>Run(async()=>{
        WritableView();var access=await workflow.Access(course,Actor,true,ct);var choice=raw.GetChoice(course,Actor,session);
        if(choice is not null&&!access.Practice)throw new CaseChatException(403,"課程角色已變更，請重新開始純聊天。");
        var selectedProvider=choice is null?provider:models?.FromChoice(choice).Provider??throw new CaseChatException(503,"此環境暫時無法選擇其他模型。");
        await PrepareInspection(course,"raw-chat-no-system",ct);
        return await WithInspection(course,await raw.Send(course,Actor,session,input.Content,selectedProvider,ct,async token=>{var current=await workflow.Access(course,Actor,true,token);if(choice is not null&&!current.Practice)throw new CaseChatException(403,"課程角色已變更，未保留純聊天回覆。");}),ct);
    },course,ct);
    // Deletion is bound to the authenticated owner even after withdrawal. It never
    // exposes content and must remain possible when course access was revoked.
    [HttpDelete("raw/sessions/{session:guid}")]
    public Task<IActionResult> ClearRaw(Guid course,Guid session,CancellationToken ct)=>Run(async()=>{await raw.Clear(course,Actor,session,ct);return new{cleared=true};});
    [HttpDelete("raw/sessions")]
    public Task<IActionResult> ResetRaw(CancellationToken ct)=>Run(async()=>{await raw.ClearAll(Actor,ct);return new{cleared=true};});

    [CourseCapability("statistics"),HttpGet("records")]
    public Task<IActionResult> Records(Guid course,CancellationToken ct)=>Run(async()=>{if(experience.IsPractice)throw new CaseChatException(403,"預覽不可查閱學生紀錄。");return new{students=await workflow.Records(course,Actor,ct)};});
    [CourseCapability("statistics"),HttpGet("records/{student:guid}/{stage}")]
    public Task<IActionResult> StudentHistory(Guid course,Guid student,string stage,CancellationToken ct)=>Run(async()=>{if(experience.IsPractice)throw new CaseChatException(403,"預覽不可查閱學生紀錄。");return await workflow.ReviewHistory(course,Actor,student,stage,ct);});
}
