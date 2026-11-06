using AiTeachingAssistant.Controllers;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace AiTeachingAssistant.Tests;

public class SwaggerDocumentationTests
{
    [Fact]
    public async Task GeneratesAllControllersIncludingFileUploadsAndProtectedEndpoints()
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions {
            EnvironmentName="Development",ApplicationName=typeof(AuthController).Assembly.GetName().Name
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddPortalSwagger();
        await using var app=builder.Build();
        app.MapControllers();
        var document=app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        Assert.Equal("教學平台 API",document.Info.Title);
        Assert.Contains("/api/auth/login",document.Paths.Keys);
        Assert.Contains("/api/courses",document.Paths.Keys);
        Assert.Contains("/api/learning-history",document.Paths.Keys);
        var courses=document.Paths["/api/courses"].Operations[Microsoft.OpenApi.Models.OperationType.Get];
        Assert.NotEmpty(courses.Security);
        var login=document.Paths["/api/auth/login"].Operations[Microsoft.OpenApi.Models.OperationType.Post];
        Assert.True(login.Security is null || login.Security.Count==0);
        Assert.Contains("X-CSRF-TOKEN",login.Description);
        var upload=document.Paths["/api/courses/{course}/resources"].Operations[Microsoft.OpenApi.Models.OperationType.Post];
        Assert.Contains("multipart/form-data",upload.RequestBody.Content.Keys);
        var schema=upload.RequestBody.Content["multipart/form-data"].Schema;
        Assert.Equal("binary",schema.Properties["file"].Format);
        var report=document.Paths["/api/reports/{id}/submit"].Operations[Microsoft.OpenApi.Models.OperationType.Post];
        Assert.Contains("multipart/form-data",report.RequestBody.Content.Keys);
        Assert.All(document.Paths.Keys,path=>Assert.StartsWith("/api/",path));
    }
}
