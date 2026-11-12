using System.Text.Json;
using AiTeachingAssistant.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Npgsql;
using AiTeachingAssistant.Services;
var root=Path.GetFullPath(args[0]);var file=Path.Combine(root,".codex-build","portal-smoke.json");
if(args.Contains("--group-upgrade")){
    await GroupUpgrade.Run(root);return;
}
if(args.Contains("--drop-test-schema")){
    await GroupUpgrade.Drop();return;
}
if(args.Contains("--assessed-demo")){
    await AssessedDemo.Seed(root);return;
}
if(args.Contains("--assessed-demo-audit")){
    await AssessedDemo.Audit();return;
}
if(args.Contains("--submitted-assignment-demo")){
    await SubmittedAssignmentDemo.Seed();return;
}
if(args.Contains("--submitted-assignment-demo-audit")){
    await SubmittedAssignmentDemo.Audit();return;
}
if(args.Contains("--publish-demo-grades")){
    await DemoGradePublisher.Publish();return;
}
if(args.Contains("--publish-demo-grades-audit")){
    await DemoGradePublisher.Audit();return;
}
if(args.Contains("--activate-demo-classmates")){
    await DemoClassmateAccounts.Activate(root);return;
}
if(args.Contains("--rename-demo-classmates")){
    await DemoClassmateNames.Rename();return;
}
if(args.Contains("--rename-demo-classmates-audit")){
    await DemoClassmateNames.Audit();return;
}
if(args.Contains("--course-demo"))
{
    await CourseDemo.Seed();
    return;
}
if(args.Contains("--discussion-demo"))
{
    await DiscussionDemo.Seed();
    return;
}
if(args.Contains("--discussion-demo-audit"))
{
    await DiscussionDemo.Audit();
    return;
}
using var config=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"appsettings.Local.json")));
await using var db=NpgsqlDataSource.Create(config.RootElement.GetProperty("ConnectionStrings").GetProperty("Portal").GetString()!);
if(args.Contains("--cleanup"))
{
    using var state=JsonDocument.Parse(await File.ReadAllTextAsync(file));
    var ids=state.RootElement.GetProperty("accounts").EnumerateArray().Select(a=>a.GetProperty("id").GetGuid()).ToArray();
    var cleanupRun=state.RootElement.GetProperty("run").GetString()!;
    await using var conn=await db.OpenConnectionAsync();await using var tx=await conn.BeginTransactionAsync();
    await using(var check=new NpgsqlCommand("SELECT count(*) FROM app_users WHERE id=ANY($1) AND email LIKE $2",conn,tx))
    {check.Parameters.AddWithValue(ids);check.Parameters.AddWithValue("qa-"+cleanupRun+"-%@example.invalid");if((long)(await check.ExecuteScalarAsync())! != ids.Length)throw new Exception("Cleanup IDs do not match this synthetic run.");}
    // Answer revisions and their deferred current pointers need explicit, scoped cleanup.
    // Every target is reached through courses owned by this run's verified synthetic users.
    const string scope="SELECT a.id FROM assignments a JOIN courses c ON c.id=a.course_id WHERE c.owner_user_id=ANY($1)";
    await using(var check=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM submissions WHERE assignment_id IN ("+scope+") AND (course_group_id IS NOT NULL OR NOT(student_user_id=ANY($1))))",conn,tx))
    {check.Parameters.AddWithValue(ids);if(await check.ExecuteScalarAsync() is true)throw new Exception("Cleanup would touch a non-QA submission; refusing.");}
    foreach(var guardSql in new[]{
        "SELECT EXISTS(SELECT 1 FROM course_questions q JOIN courses c ON c.id=q.course_id WHERE c.owner_user_id=ANY($1) AND (NOT(q.author_id=ANY($1)) OR (q.answered_by IS NOT NULL AND NOT(q.answered_by=ANY($1)))))",
        "SELECT EXISTS(SELECT 1 FROM resource_likes l JOIN course_resources r ON r.id=l.resource_id JOIN courses c ON c.id=r.course_id WHERE c.owner_user_id=ANY($1) AND NOT(l.user_id=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM resource_comments m JOIN course_resources r ON r.id=m.resource_id JOIN courses c ON c.id=r.course_id WHERE c.owner_user_id=ANY($1) AND NOT(m.author_id=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM quiz_learning_starts s JOIN assignments a ON a.id=s.assignment_id JOIN courses c ON c.id=a.course_id WHERE c.owner_user_id=ANY($1) AND NOT(s.user_id=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM learning_time_sessions s JOIN course_resources r ON r.id=s.resource_id JOIN courses c ON c.id=r.course_id WHERE c.owner_user_id=ANY($1) AND NOT(s.user_id=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM report_submissions s JOIN report_assignments r ON r.id=s.report_id JOIN courses c ON c.id=r.course_id WHERE c.owner_user_id=ANY($1) AND (NOT(s.student_id=ANY($1)) OR (s.graded_by IS NOT NULL AND NOT(s.graded_by=ANY($1)))))",
        "SELECT EXISTS(SELECT 1 FROM report_assignments r JOIN courses c ON c.id=r.course_id WHERE c.owner_user_id=ANY($1) AND NOT(r.created_by=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM studio_attempts a JOIN studio_case_versions v ON v.id=a.case_version_id JOIN studio_cases s ON s.id=v.case_id JOIN courses c ON c.id=s.course_id WHERE c.owner_user_id=ANY($1) AND NOT(a.student_id=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM studio_case_versions v JOIN studio_cases s ON s.id=v.case_id JOIN courses c ON c.id=s.course_id WHERE c.owner_user_id=ANY($1) AND NOT(v.created_by=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM studio_feedback_runs f JOIN studio_templates t ON t.id=f.template_id JOIN studio_answer_versions v ON v.id=f.answer_version_id JOIN studio_attempts a ON a.id=v.attempt_id WHERE t.created_by=ANY($1) AND NOT(a.student_id=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM course_grade_scores s JOIN courses c ON c.id=s.course_id WHERE c.owner_user_id=ANY($1) AND (NOT(s.student_id=ANY($1)) OR NOT(s.updated_by=ANY($1))))",
        "SELECT EXISTS(SELECT 1 FROM course_grade_items i JOIN courses c ON c.id=i.course_id WHERE c.owner_user_id=ANY($1) AND NOT(i.created_by=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM exam_attempts a JOIN exams e ON e.id=a.exam_id JOIN courses c ON c.id=e.course_id WHERE c.owner_user_id=ANY($1) AND NOT(a.student_id=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM course_showcases s JOIN courses c ON c.id=s.course_id WHERE c.owner_user_id=ANY($1) AND NOT(s.created_by=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM course_showcase_publications p JOIN course_showcases s ON s.id=p.showcase_id JOIN courses c ON c.id=s.course_id WHERE c.owner_user_id=ANY($1) AND NOT(p.approved_by=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM course_showcase_versions v JOIN course_showcases s ON s.id=v.showcase_id JOIN courses c ON c.id=s.course_id WHERE c.owner_user_id=ANY($1) AND NOT(v.created_by=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM course_resources r JOIN courses c ON c.id=r.course_id WHERE c.owner_user_id=ANY($1) AND NOT(r.uploaded_by=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM assignment_discussions d WHERE d.assignment_id IN ("+scope+") AND NOT(d.author_id=ANY($1)))",
        "SELECT EXISTS(SELECT 1 FROM course_group_members m JOIN course_groups g ON g.id=m.group_id JOIN courses c ON c.id=g.course_id WHERE c.owner_user_id=ANY($1) AND NOT(m.user_id=ANY($1)))"})
    {await using var check=new NpgsqlCommand(guardSql,conn,tx);check.Parameters.AddWithValue(ids);if(await check.ExecuteScalarAsync() is true)throw new Exception("Cleanup would touch a non-QA course-tool record; refusing.");}
    foreach(var sql in new[]{
        "DELETE FROM notification_reads WHERE user_id=ANY($1)",
        "DELETE FROM course_questions WHERE course_id IN (SELECT id FROM courses WHERE owner_user_id=ANY($1))",
        "DELETE FROM course_showcases WHERE course_id IN (SELECT id FROM courses WHERE owner_user_id=ANY($1))",
        "DELETE FROM quiz_learning_starts WHERE assignment_id IN ("+scope+")",
        "DELETE FROM quiz_learning_requirements WHERE assignment_id IN ("+scope+")",
        "DELETE FROM learning_time_sessions WHERE resource_id IN (SELECT r.id FROM course_resources r JOIN courses c ON c.id=r.course_id WHERE c.owner_user_id=ANY($1))",
        "DELETE FROM evaluation_runs WHERE answer_revision_id IN (SELECT r.id FROM answer_revisions r JOIN submission_answers sa ON sa.id=r.answer_id WHERE sa.assignment_id IN ("+scope+"))",
        "DELETE FROM grades WHERE submission_revision_id IN (SELECT r.id FROM submission_revisions r JOIN submissions s ON s.id=r.submission_id WHERE s.assignment_id IN ("+scope+"))",
        "DELETE FROM quiz_grading WHERE snapshot_id IN (SELECT r.id FROM submission_revisions r JOIN submissions s ON s.id=r.submission_id WHERE s.assignment_id IN ("+scope+"))",
        "DELETE FROM submission_revisions WHERE submission_id IN (SELECT id FROM submissions WHERE assignment_id IN ("+scope+"))",
        "UPDATE assignments SET rubric_id=NULL WHERE id IN ("+scope+")",
        "DELETE FROM rubrics WHERE course_id IN (SELECT id FROM courses WHERE owner_user_id=ANY($1))",
        "UPDATE submission_answers SET current_revision_id=NULL WHERE assignment_id IN ("+scope+")",
        "DELETE FROM answer_revisions WHERE answer_id IN (SELECT id FROM submission_answers WHERE assignment_id IN ("+scope+"))",
        "DELETE FROM submission_answers WHERE assignment_id IN ("+scope+")",
        "DELETE FROM submissions WHERE assignment_id IN ("+scope+")",
        "DELETE FROM assignment_discussions WHERE assignment_id IN ("+scope+")",
        "DELETE FROM course_resources WHERE course_id IN (SELECT id FROM courses WHERE owner_user_id=ANY($1))",
        "DELETE FROM exam_attempts WHERE exam_id IN (SELECT e.id FROM exams e JOIN courses c ON c.id=e.course_id WHERE c.owner_user_id=ANY($1))",
        "DELETE FROM exams WHERE course_id IN (SELECT id FROM courses WHERE owner_user_id=ANY($1))",
        "DELETE FROM report_submissions WHERE report_id IN (SELECT r.id FROM report_assignments r JOIN courses c ON c.id=r.course_id WHERE c.owner_user_id=ANY($1))",
        "DELETE FROM course_grade_scores WHERE course_id IN (SELECT id FROM courses WHERE owner_user_id=ANY($1))",
        "DELETE FROM course_grade_items WHERE course_id IN (SELECT id FROM courses WHERE owner_user_id=ANY($1))",
        "DELETE FROM studio_feedback_runs WHERE answer_version_id IN (SELECT av.id FROM studio_answer_versions av JOIN studio_attempts a ON a.id=av.attempt_id JOIN studio_case_versions v ON v.id=a.case_version_id JOIN studio_cases s ON s.id=v.case_id JOIN courses c ON c.id=s.course_id WHERE c.owner_user_id=ANY($1))",
        "DELETE FROM studio_answer_versions WHERE attempt_id IN (SELECT a.id FROM studio_attempts a JOIN studio_case_versions v ON v.id=a.case_version_id JOIN studio_cases s ON s.id=v.case_id JOIN courses c ON c.id=s.course_id WHERE c.owner_user_id=ANY($1))",
        "DELETE FROM studio_attempts WHERE case_version_id IN (SELECT v.id FROM studio_case_versions v JOIN studio_cases s ON s.id=v.case_id JOIN courses c ON c.id=s.course_id WHERE c.owner_user_id=ANY($1))",
        "UPDATE studio_cases SET active_version_id=NULL,draft_version_id=NULL WHERE course_id IN (SELECT id FROM courses WHERE owner_user_id=ANY($1))",
        "DELETE FROM studio_case_versions WHERE case_id IN (SELECT s.id FROM studio_cases s JOIN courses c ON c.id=s.course_id WHERE c.owner_user_id=ANY($1))",
        "DELETE FROM report_assignments WHERE course_id IN (SELECT id FROM courses WHERE owner_user_id=ANY($1))",
        "DELETE FROM studio_cases WHERE course_id IN (SELECT id FROM courses WHERE owner_user_id=ANY($1))",
        "DELETE FROM studio_templates WHERE created_by=ANY($1)",
        "DELETE FROM studio_profiles WHERE user_id=ANY($1)",
        "UPDATE studio_settings SET updated_by=NULL WHERE updated_by=ANY($1)",
        "DELETE FROM course_groups WHERE course_id IN (SELECT id FROM courses WHERE owner_user_id=ANY($1))",
        
        
        "DELETE FROM courses WHERE owner_user_id=ANY($1)","DELETE FROM audit_events WHERE actor_user_id=ANY($1)","DELETE FROM portal_credentials WHERE user_id=ANY($1)","DELETE FROM course_members WHERE user_id=ANY($1)","DELETE FROM app_users WHERE id=ANY($1)"})
    {await using var cmd=new NpgsqlCommand(sql,conn,tx);cmd.Parameters.AddWithValue(ids);await cmd.ExecuteNonQueryAsync();}
    await tx.CommitAsync();File.Delete(file);Console.WriteLine("Synthetic QA accounts and courses removed.");return;
}
if(File.Exists(file))throw new InvalidOperationException("QA state already exists; clean the previous run before preparing new accounts.");
var run=Guid.NewGuid().ToString("N")[..12];var password=Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24))+"aA1!";
var hasher=new PasswordHasher<PortalUser>(Options.Create(new PasswordHasherOptions{IterationCount=210000}));
var accounts=new List<PortalUser>();
await using(var conn=await db.OpenConnectionAsync())
await using(var tx=await conn.BeginTransactionAsync())
{
    // Legacy labels identify distinct synthetic personas only; persisted permissions use Administrator/Member.
    foreach(var role in new[]{"Professor","TeachingAssistant","Student"})
    {
        var user=new PortalUser(Guid.NewGuid(),role=="Professor"?"測試教授":role=="TeachingAssistant"?"測試助教":"測試學生",$"qa-{run}-{role.ToLowerInvariant()}@example.invalid",role);
        await using(var cmd=new NpgsqlCommand("INSERT INTO app_users(id,display_name,email) VALUES($1,$2,$3)",conn,tx))
        {cmd.Parameters.AddWithValue(user.Id);cmd.Parameters.AddWithValue(user.DisplayName);cmd.Parameters.AddWithValue(user.Email);await cmd.ExecuteNonQueryAsync();}
        await using(var cmd=new NpgsqlCommand("INSERT INTO portal_credentials(user_id,normalized_email,password_hash,platform_role,can_create_courses) VALUES($1,$2,$3,$4,$5)",conn,tx))
        {cmd.Parameters.AddWithValue(user.Id);cmd.Parameters.AddWithValue(user.Email.ToUpperInvariant());cmd.Parameters.AddWithValue(hasher.HashPassword(user,password));cmd.Parameters.AddWithValue(role=="Professor"?PortalRoles.Administrator:PortalRoles.Member);cmd.Parameters.AddWithValue(role=="Professor");await cmd.ExecuteNonQueryAsync();}
        accounts.Add(user);
    }
    await tx.CommitAsync();
}
Guid? demoCourseId=null,demoAssignmentId=null;
// Persist cleanup ownership before optional demo creation, so failures remain recoverable.
await File.WriteAllTextAsync(file,JsonSerializer.Serialize(new{run,password,accounts},new JsonSerializerOptions(JsonSerializerDefaults.Web)));
if(args.Contains("--demo"))
{
    var teacher=accounts.Single(a=>a.Role=="Professor");demoCourseId=Guid.NewGuid();
    await using(var conn=await db.OpenConnectionAsync())
    await using(var tx=await conn.BeginTransactionAsync())
    {
        await using(var cmd=new NpgsqlCommand("INSERT INTO courses(id,code,title,term_code,owner_user_id,status) VALUES($1,$2,$3,'115-1',$4,'active')",conn,tx))
        {cmd.Parameters.AddWithValue(demoCourseId.Value);cmd.Parameters.AddWithValue("DEMO-"+run);cmd.Parameters.AddWithValue("11/15 示範課程（合成資料）");cmd.Parameters.AddWithValue(teacher.Id);await cmd.ExecuteNonQueryAsync();}
        foreach(var user in accounts)
        {await using var cmd=new NpgsqlCommand("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,$3)",conn,tx);cmd.Parameters.AddWithValue(demoCourseId.Value);cmd.Parameters.AddWithValue(user.Id);cmd.Parameters.AddWithValue(user.Role=="Professor"?"teacher":user.Role=="TeachingAssistant"?"ta":"student");await cmd.ExecuteNonQueryAsync();}
        await tx.CommitAsync();
    }
    var workflow=new AssignmentWorkflow(db,TimeProvider.System);
    var rubric=await workflow.SaveRubric(demoCourseId.Value,null,teacher.Id,new SaveRubricRequest{Title="示範：證據與推理",Criteria=[new(){Title="證據品質",Description="依提供資料限制結論",Weight=1,Levels=["缺乏依據","部分依據","大致適切","完整適切"]}]},default);
    var assignment=await workflow.Create(demoCourseId.Value,teacher.Id,new SaveAssignmentRequest{Title="示範：修訂與教師回饋",Instructions="合成情境：個案在提示下完成指定任務，未提供其他功能資料。請區分事實與推論。",DueAt=DateTimeOffset.UtcNow.AddDays(30),AllowRevisions=true,MaxScore=100,RubricId=rubric.Id,Questions=[new(){PromptText="請分析已知表現與推論限制。"}]},default);
    demoAssignmentId=assignment.Id;
}
await File.WriteAllTextAsync(file,JsonSerializer.Serialize(new{run,password,accounts,demoCourseId,demoAssignmentId},new JsonSerializerOptions(JsonSerializerDefaults.Web)));
Console.WriteLine("Synthetic QA accounts prepared in ignored local state file.");
