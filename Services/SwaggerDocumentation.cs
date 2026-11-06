using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AiTeachingAssistant.Services;

public static class SwaggerDocumentation
{
    public static IServiceCollection AddPortalSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options=>
        {
            options.SwaggerDoc("v1",new OpenApiInfo {
                Title="教學平台 API",Version="v1",
                Description="開發環境 API 文件。可先在同一網站登入，或執行 Auth 的 POST /api/auth/login；後續請求會沿用登入 Cookie。寫入請求自動取得 CSRF token，現有課程、平台角色與正式／練習權限仍由伺服器驗證。Try it out 會實際呼叫 API。"
            });
            options.CustomSchemaIds(type=>type.FullName?.Replace('+','.')??type.Name);
            options.AddSecurityDefinition("sessionCookie",new OpenApiSecurityScheme {
                Type=SecuritySchemeType.ApiKey,In=ParameterLocation.Cookie,Name=".TeachingPortal.Auth",
                Description="由 POST /api/auth/login 設定的 HttpOnly Cookie，瀏覽器自動帶入；請透過登入取得，不需在 Authorize 手動輸入。"
            });
            options.OperationFilter<PortalSwaggerOperationFilter>();
        });
        return services;
    }

    public static void UsePortalSwagger(this WebApplication app)
    {
        if(!app.Environment.IsDevelopment())
        {
            // Prevent the SPA fallback from returning a misleading successful docs page.
            app.Use(async(context,next)=> {
                if(context.Request.Path.StartsWithSegments("/swagger")){context.Response.StatusCode=404;return;}
                await next();
            });
            return;
        }
        app.UseSwagger();
        app.UseSwaggerUI(options=> {
            options.SwaggerEndpoint("v1/swagger.json","教學平台 API v1");
            options.DocumentTitle="教學平台 API · Swagger";
            options.EnableFilter();
            options.DisplayRequestDuration();
            options.ConfigObject.AdditionalItems["withCredentials"]=true;
            options.UseRequestInterceptor("""
                async (request) => {
                    const target = new URL(request.url, window.location.href);
                    if (target.origin !== window.location.origin) throw new Error('僅允許同一網站的 API 請求');
                    request.credentials = 'same-origin';
                    const method = (request.method || 'GET').toUpperCase();
                    if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
                        const apiStart = target.pathname.indexOf('/api/');
                        if (apiStart < 0) throw new Error('找不到 API 路徑');
                        const csrf = await fetch(target.pathname.slice(0, apiStart) + '/api/auth/csrf', {credentials:'same-origin', cache:'no-store'});
                        if (!csrf.ok) throw new Error('無法取得 CSRF token');
                        const {token} = await csrf.json();
                        request.headers = request.headers || {};
                        request.headers['X-CSRF-TOKEN'] = token;
                    }
                    return request;
                }
                """);
        });
    }
}

public sealed class PortalSwaggerOperationFilter:IOperationFilter
{
    public void Apply(OpenApiOperation operation,OperationFilterContext context)
    {
        var metadata=context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var protectedEndpoint=!metadata.OfType<IAllowAnonymous>().Any() && metadata.OfType<IAuthorizeData>().Any();
        if(protectedEndpoint)
        {
            operation.Security=[new OpenApiSecurityRequirement {
                [new OpenApiSecurityScheme {Reference=new OpenApiReference {Type=ReferenceType.SecurityScheme,Id="sessionCookie"}}]=[]
            }];
            operation.Responses.TryAdd("401",new OpenApiResponse {Description="尚未登入或登入權限已失效。"});
            operation.Responses.TryAdd("403",new OpenApiResponse {Description="目前帳號／課程角色無此操作權限。"});
        }
        if(context.ApiDescription.HttpMethod is not ("GET" or "HEAD" or "OPTIONS"))
            operation.Description=(operation.Description??"")+"\nSwagger UI 自動附上 X-CSRF-TOKEN；其他客戶端須先以相同 Cookie 工作階段呼叫 GET /api/auth/csrf。";
    }
}
