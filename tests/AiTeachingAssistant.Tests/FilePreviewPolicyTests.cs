using AiTeachingAssistant.Services;
namespace AiTeachingAssistant.Tests;
public class FilePreviewPolicyTests
{
    [Theory]
    [InlineData("test.pdf","255044462D312E37","application/pdf")]
    [InlineData("test.PNG","89504E470D0A1A0A","image/png")]
    [InlineData("test.jpeg","FFD8FF","image/jpeg")]
    [InlineData("test.gif","474946383961","image/gif")]
    [InlineData("test.webp","524946460000000057454250","image/webp")]
    [InlineData("test.svg","3C737667",null)]
    [InlineData("fake.png","3C68746D6C",null)]
    [InlineData("test.html","255044462D",null)]
    [InlineData("test.pdf","",null)]
    public void RequiresAllowedExtensionAndMatchingSignature(string name,string hex,string? type)=>Assert.Equal(type,FilePreviewPolicy.ContentType(name,Convert.FromHexString(hex)));
}
