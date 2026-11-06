using System.ComponentModel.DataAnnotations;
namespace AiTeachingAssistant.Models;

public static class PortalRoles
{
    public const string Administrator="Administrator", Member="Member";
    public static bool IsValid(string role)=>role is Administrator or Member;
}
public sealed record PortalUser(Guid Id, string DisplayName, string Email, string Role, string? StudentNumber = null, bool CanCreateCourses=false, bool CanTeach=false, bool HasStudentCourses=false);
public sealed record PortalAccount(PortalUser User, string PasswordHash, string Status);
public class LoginRequest
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(128)] public string Password { get; set; } = "";
}
public class CreateAccountRequest : LoginRequest
{
    [StringLength(40)] public string? StudentNumber { get; set; }
    [Required, StringLength(100, MinimumLength=1)] public string DisplayName { get; set; } = "";
    [Required] public string Role { get; set; } = PortalRoles.Member;
    public bool CanCreateCourses {get;set;}
}
public sealed class BootstrapRequest : CreateAccountRequest
{
    [Required, StringLength(200)] public string SetupToken { get; set; } = "";
}
public sealed class ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; set; } = "";
    [Required, StringLength(128, MinimumLength=12)] public string NewPassword { get; set; } = "";
}
public sealed class CreateCourseRequest
{
    [Required, StringLength(40)] public string Code { get; set; } = "";
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [Required, StringLength(40)] public string TermCode { get; set; } = "";
}
public sealed record CourseSummary(Guid Id, string Code, string Title, string TermCode, string Status, string CourseRole, int MemberCount, int AssignmentCount, int ResourceCount=0, int StudentCount=0, int QuizCount=0, int ReportCount=0, bool IsOwner=false,bool CanPractice=false);
