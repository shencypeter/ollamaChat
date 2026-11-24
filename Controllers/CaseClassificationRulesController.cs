using System.Security.Claims;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiTeachingAssistant.Controllers;

[ApiController,Authorize,Route("api/courses/{course:guid}/case-analysis/rules")]
public sealed class CaseClassificationRulesController(CaseAnalysisChat workflow,LearnerExperience experience):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<IActionResult> Run(Func<Task<CaseClassificationRuleSet>> action)
    {
        try
        {
            if(experience.IsPractice)throw new CaseChatException(403,"學生視角預覽不可查看或維護分類設定。");
            return Ok(await action());
        }
        catch(CaseChatException e){return StatusCode(e.StatusCode,new{error=e.Message});}
    }
    [HttpGet] public Task<IActionResult> Read(Guid course,CancellationToken ct)=>Run(()=>workflow.Rules(course,Actor,ct));
    [HttpPut,RequestSizeLimit(64*1024)] public Task<IActionResult> Save(Guid course,CaseClassificationRulesInput input,CancellationToken ct)=>Run(()=>workflow.SaveRules(course,Actor,input,ct));
}
