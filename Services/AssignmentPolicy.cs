namespace AiTeachingAssistant.Services;

public sealed class AssignmentWorkflowException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
public static class AssignmentPolicy
{
    public static bool IsStaff(string role) => role is "teacher" or "ta";
    public static void RequireStaff(string role)
    {
        if (!IsStaff(role)) throw new AssignmentWorkflowException(403, "只有本課程的教師或助教可以管理作業。");
    }
    public static void RequireWritable(string role, string assignmentStatus, string courseStatus,
        DateTimeOffset? opensAt, DateTimeOffset? dueAt, DateTimeOffset now)
    {
        if (role != "student") throw new AssignmentWorkflowException(403, "只有本課程的學生可以提交個人作答。");
        if (assignmentStatus != "published" || courseStatus == "archived") throw new AssignmentWorkflowException(409, "作業目前未開放作答。");
        if (opensAt.HasValue && now < opensAt.Value) throw new AssignmentWorkflowException(409, "尚未到作業開放時間。");
        if (dueAt.HasValue && now >= dueAt.Value) throw new AssignmentWorkflowException(409, "已超過作業截止時間，無法儲存或提交。");
    }
}
