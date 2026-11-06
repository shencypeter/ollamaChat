using System.Net;
using System.Security.Claims;
using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
namespace AiTeachingAssistant.Tests;
public class OllamaRequestInspectionTests
{
    [Fact] public void StaffStudentExperienceDoesNotExposeStaffDiagnostics()
    {
        var accessor=Context("Professor");accessor.HttpContext!.Request.Headers[LearnerExperience.Header]="student";
        var inspection=new OllamaRequestInspection(accessor,new LearnerExperience(accessor));inspection.Capture("not captured",null,"assessment");Assert.Null(inspection.Current);
    }
    private static HttpContextAccessor Context(string? role)=>new(){HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(
        new ClaimsIdentity(role is null?[]:[new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString()),new Claim(ClaimTypes.Role,role),new Claim("can_teach",(role is "Professor" or "TeachingAssistant").ToString())],role is null?null:"test"))}};
    [Theory][InlineData("Professor")][InlineData("TeachingAssistant")]
    public async Task AuthorizedStaffReceivesExactlyAssembledAssessment(string role)
    {
        var inspection=new OllamaRequestInspection(Context(role));var transport=new Capture();
        var client=new OllamaClient(new HttpClient(transport){BaseAddress=new Uri("http://localhost:11434/")},
            Microsoft.Extensions.Options.Options.Create(new OllamaOptions{Model="test",SystemPrompt="繁體中文"}),NullLogger<OllamaClient>.Instance,inspection:inspection);
        await client.AssessAsync([new("system",AnalysisStageInstructions.Get(AnalysisStage.Premorbid)),new("user",AssessmentContract.BuildInput("合成測試答案",null,"合成案例"))],false,default);
        var request=Assert.Single(inspection.Current!.Requests);
        Assert.Equal("assessment",request.Purpose);Assert.Equal("http://localhost:11434/api/chat",request.Url);
        using var actual=JsonDocument.Parse(transport.Body!);
        Assert.Equal(actual.RootElement.ToString(),request.Payload.ToString());
        Assert.Contains("合成測試答案",request.Payload.GetProperty("messages")[1].GetProperty("content").GetString());
        Assert.True(request.Payload.TryGetProperty("format",out _));
        Assert.Equal(200,request.ResponseStatusCode);
        Assert.Equal("{}",request.RawResponse!.Value.GetProperty("message").GetProperty("content").GetString());
        Assert.False(request.RawResponse.Value.GetProperty("message").TryGetProperty("thinking",out _));
    }
    [Theory][InlineData(null)][InlineData("Student")][InlineData("teacher")][InlineData("admin")]
    public void StudentAnonymousAndUnrecognizedRolesNeverCapturePayload(string? role)
    {
        var inspector=new OllamaRequestInspection(Context(role));
        inspector.Capture("not even parsed for unauthorized users",null,"chat");
        Assert.Null(inspector.Current);
        var serialized=JsonSerializer.Serialize(new AiFailure("error",inspector.Current),new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("requestInspection",serialized);
    }
    [Fact] public void RequestScopesCannotReadOtherUsersRequests()
    {
        var first=new OllamaRequestInspection(Context("Professor"));var second=new OllamaRequestInspection(Context("Professor"));
        first.Capture("""{"model":"test","messages":[{"role":"user","content":"private synthetic content"}]}""",null,"chat");
        Assert.Null(second.Current);Assert.Single(first.Current!.Requests);
    }
    [Fact] public void PrivateThinkingAndUrlCredentialsAreExcluded()
    {
        var inspection=new OllamaRequestInspection(Context("Professor"));
        var index=inspection.Capture("""{"messages":[{"role":"assistant","content":"public","thinking":"secret"}]}""",new Uri("http://user:pass@localhost:11434/api/chat?token=secret"),"chat");
        inspection.CaptureResponse(index,"""{"message":{"role":"assistant","content":"public response","thinking":"nested secret"},"thinking":"top-level secret"}""",200);
        var request=Assert.Single(inspection.Current!.Requests);
        Assert.DoesNotContain("secret",request.Payload.ToString());Assert.DoesNotContain("secret",request.RawResponse!.Value.ToString());Assert.DoesNotContain("pass",request.Url);Assert.DoesNotContain("token",request.Url);
        Assert.Contains("public response",request.RawResponse.Value.ToString());
    }
    [Fact] public void NonJsonResponseIsNotExposedAsUnredactedText()
    {
        var inspection=new OllamaRequestInspection(Context("Professor"));
        var index=inspection.Capture("""{"messages":[]}""",null,"chat");
        inspection.CaptureResponse(index,"not json and could contain thinking: secret",502);
        var request=Assert.Single(inspection.Current!.Requests);
        Assert.Equal(502,request.ResponseStatusCode);Assert.Null(request.RawResponse);
    }
    [Fact] public void ChangedRoleCannotRetrieveCapturedData()
    {
        var accessor=Context("Professor");var inspection=new OllamaRequestInspection(accessor);
        inspection.Capture("""{"messages":[]}""",null,"chat");accessor.HttpContext!.User=Context("Student").HttpContext!.User;
        Assert.Null(inspection.Current);
    }
    [Fact] public void SummaryAndMainRequestsArePreservedInOrder()
    {
        var inspection=new OllamaRequestInspection(Context("Professor"));
        inspection.Capture("""{"messages":[]}""",null,"memory-summary");inspection.Capture("""{"messages":[]}""",null,"chat");
        Assert.Equal(new[]{"memory-summary","chat"},inspection.Current!.Requests.Select(r=>r.Purpose));
    }
    private sealed class Capture:HttpMessageHandler
    {
        public string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {Body=await request.Content!.ReadAsStringAsync(ct);return new(HttpStatusCode.OK){Content=new StringContent("""{"message":{"role":"assistant","content":"{}","thinking":"private synthetic reasoning"}}""")};}
    }
}

