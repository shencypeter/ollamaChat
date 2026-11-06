using AiTeachingAssistant.Models;
namespace AiTeachingAssistant.Services;

public sealed class LearnerExperience(IHttpContextAccessor accessor)
{
    public const string Header="X-Portal-View";
    public string? CourseRole {get;set;}
    public static bool IsStaff(System.Security.Claims.ClaimsPrincipal user)=>user.FindFirst("can_teach")?.Value==bool.TrueString;
    public bool Requested=>accessor.HttpContext is { } c && c.User.Identity?.IsAuthenticated==true && c.Request.Headers[Header]=="student";
    // A real student in another course always remains in the formal namespace.
    public bool IsPractice=>Requested && (CourseRole is "teacher" or "ta" || CourseRole==null && accessor.HttpContext is { } c && IsStaff(c.User));
    public bool CanUseFormative=>accessor.HttpContext?.User.Identity?.IsAuthenticated==true;
}
