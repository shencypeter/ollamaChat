using AiTeachingAssistant.Services;
namespace AiTeachingAssistant.Tests;
public class AssignmentPolicyTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-11-15T04:00:00Z");
    [Theory]
    [InlineData("teacher")] [InlineData("ta")]
    public void CourseStaffCanManageAssignments(string role)=>AssignmentPolicy.RequireStaff(role);
    [Theory]
    [InlineData("student")] [InlineData("Professor")] [InlineData("")]
    public void GlobalRoleOrStudentIsNotCourseStaff(string role)=>Assert.Equal(403,Assert.Throws<AssignmentWorkflowException>(()=>AssignmentPolicy.RequireStaff(role)).StatusCode);
    [Fact]
    public void StudentMayWriteAtOpeningTime()=>AssignmentPolicy.RequireWritable("student","published","active",Now,Now.AddHours(1),Now);
    [Fact]
    public void StudentMayWriteWithoutDatesInDraftCourse()=>AssignmentPolicy.RequireWritable("student","published","draft",null,null,Now);
    [Theory]
    [InlineData("teacher","published","active",403)]
    [InlineData("ta","published","active",403)]
    [InlineData("student","draft","active",409)]
    [InlineData("student","closed","active",409)]
    [InlineData("student","published","archived",409)]
    public void StaffAndUnavailableAssignmentsCannotAcceptAnswers(string role,string status,string course,int expected)=>Assert.Equal(expected,Assert.Throws<AssignmentWorkflowException>(()=>AssignmentPolicy.RequireWritable(role,status,course,null,null,Now)).StatusCode);
    [Fact]
    public void DeadlineIsExclusiveEvenAtExactInstant()=>Assert.Equal(409,Assert.Throws<AssignmentWorkflowException>(()=>AssignmentPolicy.RequireWritable("student","published","active",null,Now,Now)).StatusCode);
    [Fact]
    public void FutureOpeningRejectsEarlySave()=>Assert.Equal(409,Assert.Throws<AssignmentWorkflowException>(()=>AssignmentPolicy.RequireWritable("student","published","active",Now.AddSeconds(1),null,Now)).StatusCode);
}
