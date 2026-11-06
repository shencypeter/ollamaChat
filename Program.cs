using System.Security.Claims;
using System.Threading.RateLimiting;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
// Select a versioned payload profile before applying machine-local and environment overrides.
var ollamaProfile = builder.Configuration["Ollama:Profile"] ?? "breeze";
if (!new[] { "breeze", "llama3", "qwen" }.Contains(ollamaProfile))
    throw new InvalidOperationException("Unknown Ollama profile. Use breeze, llama3 or qwen.");
builder.Configuration.AddJsonFile($"Configuration/Ollama/{ollamaProfile}.json", optional: false);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
builder.Logging.ClearProviders();builder.Logging.AddConsole();builder.Logging.AddDebug();
var connectionString=builder.Configuration.GetConnectionString("Portal")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:Portal using appsettings.Local.json or environment variables.");
if(args.Contains("--inspect-database") || args.Contains("--migrate"))
{
    await using var database=NpgsqlDataSource.Create(connectionString);
    await DatabaseMigrator.RunAsync(database,Path.Combine(builder.Environment.ContentRootPath,"Database"),args.Contains("--migrate"));
    return;
}
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddScoped<IPortalAccountStore,PortalAccountStore>();
builder.Services.AddScoped<AssignmentWorkflow>();
builder.Services.AddScoped<LearningHistory>();
builder.Services.AddScoped<LearningTime>();
builder.Services.AddScoped<StudioWorkflow>();
builder.Services.AddScoped<LearnerExperience>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<PasswordHasherOptions>(o=>o.IterationCount=210000);
builder.Services.AddScoped<IPasswordHasher<PortalUser>,PasswordHasher<PortalUser>>();
builder.Services.AddScoped<ApiAntiforgeryFilter>();
builder.Services.AddControllers(o=>o.Filters.AddService<ApiAntiforgeryFilter>());
builder.Services.AddPortalSwagger();
builder.Services.AddAntiforgery(o=>{o.HeaderName="X-CSRF-TOKEN";o.Cookie.Name=".TeachingPortal.Csrf";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>
{
    o.Cookie.Name=".TeachingPortal.Auth";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Lax;
    o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
    o.ExpireTimeSpan=TimeSpan.FromHours(8);o.SlidingExpiration=true;
    o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};
    o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
    o.Events.OnValidatePrincipal=async c=>
    {
        var id=c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var store=c.HttpContext.RequestServices.GetRequiredService<IPortalAccountStore>();
        var account=Guid.TryParse(id,out var parsed)?await store.GetAsync(parsed,c.HttpContext.RequestAborted):null;
        if(account is null || account.Status!="active" || account.User.Role!=c.Principal?.FindFirstValue(ClaimTypes.Role) || account.User.CanCreateCourses.ToString()!=c.Principal?.FindFirstValue("can_create_courses")) c.RejectPrincipal();
        else if(c.Principal?.Identity is ClaimsIdentity identity){
            foreach(var claim in identity.FindAll("can_teach").ToArray())identity.RemoveClaim(claim);
            identity.AddClaim(new Claim("can_teach",account.User.CanTeach.ToString()));
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o=>
{
    o.RejectionStatusCode=429;
    o.AddPolicy("login",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=15,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
});
builder.Services.AddHttpContextAccessor();builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o=>{o.Cookie.Name=".TeachingPortal.Session";o.Cookie.HttpOnly=true;o.Cookie.IsEssential=true;o.IdleTimeout=TimeSpan.FromHours(4);});
builder.Services.AddOptions<OllamaOptions>().Bind(builder.Configuration.GetSection(OllamaOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<MemoryOptions>().Bind(builder.Configuration.GetSection(MemoryOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddHttpClient<IOllamaClient,OllamaClient>((services,client)=>
{
    var options=services.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
    client.BaseAddress=new Uri(options.BaseUrl.TrimEnd('/')+"/");client.Timeout=TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddScoped<IChatSessionStore,ChatSessionStore>();builder.Services.AddScoped<IMemoryOrchestrator,LangChainMemoryOrchestrator>();
builder.Services.AddScoped<OllamaRequestInspection>();
builder.Services.AddSingleton<IMarkdownRenderer,MarkdownRenderer>();builder.Services.AddSingleton<OllamaPayloadDiagnostics>();
var app=builder.Build();
app.UseExceptionHandler(error=>error.Run(async context=>
{
    context.Response.StatusCode=500;
    await context.Response.WriteAsJsonAsync(new{error="服務暫時無法完成請求，請稍後再試。"});
}));
app.UsePortalSwagger();
if(!app.Environment.IsDevelopment()){app.UseHsts();app.UseHttpsRedirection();}
app.Use(async (context,next)=>{ if(context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl="no-store"; await next(); });
app.UseStaticFiles();app.UseRouting();app.UseSession();app.UseAuthentication();app.UseAuthorization();app.UseRateLimiter();
app.Use(async(context,next)=>
{
    var experience=context.RequestServices.GetRequiredService<LearnerExperience>();
    if(context.User.Identity?.IsAuthenticated==true){
        var parts=context.Request.Path.Value?.Split('/',StringSplitOptions.RemoveEmptyEntries)??[];
        Guid? course=null;string? lookup=null;Guid target=Guid.Empty;
        if(parts.Length>=3 && parts[0]=="api" && Guid.TryParse(parts[2],out target)) {
            if(parts[1]=="courses")course=target;
            if(parts[1]=="assignments")lookup="SELECT course_id FROM assignments WHERE id=$1";
            if(parts[1]=="reports")lookup="SELECT course_id FROM report_assignments WHERE id=$1";
        }
        if(parts.Length>=4 && parts[1]=="studio" && Guid.TryParse(parts[3],out target)) {
            if(parts[2]=="cases")lookup="SELECT course_id FROM studio_cases WHERE id=$1";
            if(parts[2]=="attempts")lookup="SELECT c.course_id FROM studio_attempts a JOIN studio_case_versions v ON v.id=a.case_version_id JOIN studio_cases c ON c.id=v.case_id WHERE a.id=$1";
        }
        var db=context.RequestServices.GetRequiredService<NpgsqlDataSource>();
        if(lookup!=null){await using var cmd=db.CreateCommand(lookup);cmd.Parameters.AddWithValue(target);course=await cmd.ExecuteScalarAsync(context.RequestAborted) as Guid?;}
        if(course.HasValue){await using var cmd=db.CreateCommand("SELECT course_role FROM course_members WHERE course_id=$1 AND user_id=$2 AND status='active'");cmd.Parameters.AddWithValue(course.Value);cmd.Parameters.AddWithValue(Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!));experience.CourseRole=await cmd.ExecuteScalarAsync(context.RequestAborted) as string??"none";}
    }
    if(context.Request.Headers.TryGetValue(LearnerExperience.Header,out var view) &&
       (view!="student" || context.User.Identity?.IsAuthenticated!=true || !LearnerExperience.IsStaff(context.User)))
    {context.Response.StatusCode=403;await context.Response.WriteAsJsonAsync(new{error="只有課程教師／助教可切換學生體驗模式。"});return;}
    if(experience.IsPractice &&
       ((context.GetEndpoint()?.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any(a=>!string.IsNullOrEmpty(a.Roles))??false) ||
        context.Request.Path.Value?.TrimEnd('/').EndsWith("/members",StringComparison.OrdinalIgnoreCase)==true ||
        context.Request.Path.StartsWithSegments("/api/studio/admin") || context.Request.Path.StartsWithSegments("/api/studio/teaching")))
    {context.Response.StatusCode=403;await context.Response.WriteAsJsonAsync(new{error="學生體驗模式不提供教學管理操作，請先返回教學視角。"});return;}
    await next();
});
app.MapControllers();
app.MapFallback("/api/{**path}",context=>{context.Response.StatusCode=404;return context.Response.WriteAsJsonAsync(new{error="API endpoint not found."});});
app.MapFallbackToFile("portal/index.html");
app.Run();
public partial class Program { }
