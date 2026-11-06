using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace AiTeachingAssistant.Services;

public sealed class ApiAntiforgeryFilter(IAntiforgery antiforgery):IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if(HttpMethods.IsGet(context.HttpContext.Request.Method)||HttpMethods.IsHead(context.HttpContext.Request.Method)||HttpMethods.IsOptions(context.HttpContext.Request.Method))return;
        try{await antiforgery.ValidateRequestAsync(context.HttpContext);}
        catch(AntiforgeryValidationException){context.Result=new BadRequestObjectResult(new{error="安全驗證失敗，請重新整理頁面再試。"});}
    }
}
