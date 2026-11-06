using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
namespace AiTeachingAssistant.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(IPortalAccountStore accounts,IConfiguration config,IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("csrf"), AllowAnonymous]
    public IActionResult Csrf() => Ok(new { token=antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    [HttpGet("session"), AllowAnonymous]
    public async Task<IActionResult> Session(CancellationToken ct) => Ok(new {
        user=User.Identity?.IsAuthenticated == true ? (await accounts.GetAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),ct))?.User : null,
        needsSetup=await accounts.NeedsSetupAsync(ct)
    });
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request,CancellationToken ct)
    {
        var user=await accounts.AuthenticateAsync(request.Email,request.Password,ct);
        if(user is null) return Unauthorized(new {error="帳號或密碼不正確，或帳號暫時無法登入。"});
        await SignIn(user);return Ok(user);
    }
    [HttpPost("bootstrap"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Bootstrap(BootstrapRequest request,CancellationToken ct)
    {
        var expected=config["Portal:BootstrapToken"];
        if(string.IsNullOrWhiteSpace(expected) || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),SHA256.HashData(Encoding.UTF8.GetBytes(request.SetupToken))))
            return StatusCode(403,new {error="初始化碼不正確。"});
        try {var user=await accounts.CreateAsync(request,true,ct);await SignIn(user);return Ok(user);}
        catch(InvalidOperationException e) {return Conflict(new {error=e.Message});}
        catch(ArgumentException e) {return BadRequest(new {error=e.Message});}
    }
    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(){HttpContext.Session.Clear();await HttpContext.SignOutAsync();return NoContent();}
    [HttpPost("password"), Authorize, EnableRateLimiting("login")]
    public async Task<IActionResult> Password(ChangePasswordRequest request,CancellationToken ct)
    {
        if(!await accounts.ChangePasswordAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),request.CurrentPassword,request.NewPassword,ct)) return BadRequest(new {error="目前密碼不正確。"});
        HttpContext.Session.Clear();await HttpContext.SignOutAsync();return NoContent();
    }
    private async Task SignIn(PortalUser user)
    {
        HttpContext.Session.Clear();
        var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),new Claim(ClaimTypes.Name,user.DisplayName),new Claim(ClaimTypes.Email,user.Email),new Claim(ClaimTypes.Role,user.Role),new Claim("can_create_courses",user.CanCreateCourses.ToString()),new Claim("can_teach",user.CanTeach.ToString())},CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(identity),new AuthenticationProperties{IsPersistent=false});
    }
}

[ApiController,Route("api/accounts"),Authorize(Roles=PortalRoles.Administrator)]
public sealed class AccountsController(IPortalAccountStore accounts,NpgsqlDataSource source):ControllerBase
{
    public sealed class RoleInput {
        [System.ComponentModel.DataAnnotations.Required] public string Role {get;set;}="";
        [System.ComponentModel.DataAnnotations.Required] public string ExpectedRole {get;set;}="";
        public bool CanCreateCourses{get;set;}
        [System.Text.Json.Serialization.JsonRequired] public bool ExpectedCanCreateCourses{get;set;}
    }
    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> Role(Guid id,RoleInput input,CancellationToken ct) {
        try { await new AccountRoleManager(source).Change(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),id,input.Role,input.ExpectedRole,ct,input.CanCreateCourses,input.ExpectedCanCreateCourses);return NoContent(); }
        catch(ArgumentException e){return BadRequest(new{error=e.Message});}
        catch(UnauthorizedAccessException e){return StatusCode(403,new{error=e.Message});}
        catch(KeyNotFoundException e){return NotFound(new{error=e.Message});}
        catch(InvalidOperationException e){return Conflict(new{error=e.Message});}
    }
    public sealed class StudentNumberInput { [System.ComponentModel.DataAnnotations.StringLength(40)]public string? StudentNumber{get;set;} }
    [HttpPut("{id:guid}/student-number")]
    public async Task<IActionResult> StudentNumber(Guid id,StudentNumberInput input,CancellationToken ct) {
        await using var cmd=source.CreateCommand("UPDATE app_users SET student_number=$2 WHERE id=$1");cmd.Parameters.AddWithValue(id);cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Text,Value=string.IsNullOrWhiteSpace(input.StudentNumber)?DBNull.Value:input.StudentNumber.Trim()});
        try{return await cmd.ExecuteNonQueryAsync(ct)==1?NoContent():NotFound();}catch(PostgresException e)when(e.SqlState==PostgresErrorCodes.UniqueViolation){return Conflict(new{error="此學號已使用。"});}
    }
    [HttpGet]public async Task<IActionResult> List(CancellationToken ct)=>Ok(await accounts.ListAsync(ct));
    [HttpPost]public async Task<IActionResult> Create(CreateAccountRequest request,CancellationToken ct)
    {
        try{return Ok(await accounts.CreateAsync(request,false,ct,Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)));}
        catch(ArgumentException e){return BadRequest(new{error=e.Message});}
        catch(PostgresException e) when(e.SqlState==PostgresErrorCodes.UniqueViolation){return Conflict(new{error="此電子郵件或學號已建立帳號。"});}
    }
}
