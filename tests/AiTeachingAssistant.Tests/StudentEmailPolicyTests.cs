using AiTeachingAssistant.Services;
namespace AiTeachingAssistant.Tests;
public class StudentEmailPolicyTests
{
    [Theory][InlineData("student@example.invalid","student@csmu.edu.tw")][InlineData("STUDENT@CSMU.EDU.TW","student@csmu.edu.tw")][InlineData(" learner.115+demo@school.invalid ","learner.115+demo@csmu.edu.tw")]
    public void PreservesLocalIdentityAndUsesCsmuDomain(string source,string expected)=>Assert.Equal(expected,StudentEmailPolicy.InstitutionalAddress(source));
    [Theory][InlineData("")][InlineData("no-domain")][InlineData("@invalid")]
    public void RejectsUnknownAddressesInsteadOfInventingIdentity(string source)=>Assert.Throws<ArgumentException>(()=>StudentEmailPolicy.InstitutionalAddress(source));
}
