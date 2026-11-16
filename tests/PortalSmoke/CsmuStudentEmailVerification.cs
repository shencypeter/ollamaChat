using AiTeachingAssistant.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Npgsql;

internal static class CsmuStudentEmailVerification
{
    public static async Task Run()
    {
        var connection=Environment.GetEnvironmentVariable("ConnectionStrings__Portal")!;
        var settings=new NpgsqlConnectionStringBuilder(connection);
        if(!System.Text.RegularExpressions.Regex.IsMatch(settings.SearchPath??"","^group_test_[0-9a-f]{32}$"))throw new InvalidOperationException("Use an isolated test schema.");
        await using var db=NpgsqlDataSource.Create(connection);
        async Task<object?> Sql(string sql,params object[] args){await using var cmd=db.CreateCommand(sql);foreach(var arg in args)cmd.Parameters.AddWithValue(arg);return await cmd.ExecuteScalarAsync();}
        var student=Guid.NewGuid();var faculty=Guid.NewGuid();var collision=Guid.NewGuid();
        var password=Guid.NewGuid().ToString("N");
        var hasher=new PasswordHasher<PortalUser>(Options.Create(new PasswordHasherOptions()));
        var user=new PortalUser(student,"合成學生","email-check@old.invalid",PortalRoles.Member);
        var hash=hasher.HashPassword(user,password);
        await Sql("INSERT INTO app_users(id,display_name,email,student_number) VALUES($1,'合成學生','email-check@old.invalid','EMAIL-CHECK'),($2,'合成教師','faculty-check@old.invalid',NULL),($3,'衝突帳號','email-check@csmu.edu.tw',NULL)",student,faculty,collision);
        await Sql("INSERT INTO portal_credentials(user_id,normalized_email,password_hash,platform_role,can_create_courses) VALUES($1,'EMAIL-CHECK@OLD.INVALID',$3,'Member',false),($2,'FACULTY-CHECK@OLD.INVALID',$3,'Member',true)",student,faculty,hash);
        try
        {
            var rejected=false;
            try{await CsmuStudentEmails.Run(false,false);}catch(InvalidOperationException e)when(e.Message.Contains("already used")){rejected=true;}
            if(!rejected||!Equals(await Sql("SELECT email FROM app_users WHERE id=$1",student),user.Email))throw new Exception("Collision must reject atomically.");
            await Sql("DELETE FROM app_users WHERE id=$1",collision);
            await CsmuStudentEmails.Run(true,false);
            if(!Equals(await Sql("SELECT email FROM app_users WHERE id=$1",student),user.Email))throw new Exception("Preview wrote data.");
            await CsmuStudentEmails.Run(false,false);
            await CsmuStudentEmails.Run(false,true);
            await CsmuStudentEmails.Run(false,false);
            if(!Equals(await Sql("SELECT email FROM app_users WHERE id=$1",faculty),"faculty-check@old.invalid"))throw new Exception("Faculty changed.");
            if(!Equals(await Sql("SELECT normalized_email FROM portal_credentials WHERE user_id=$1",student),"EMAIL-CHECK@CSMU.EDU.TW"))throw new Exception("Login email did not migrate.");
            var stored=(string)(await Sql("SELECT password_hash FROM portal_credentials WHERE user_id=$1",student))!;
            if(stored!=hash||hasher.VerifyHashedPassword(user,stored,password)==PasswordVerificationResult.Failed)throw new Exception("Existing password no longer works.");
            Console.WriteLine("Student domain checks passed: collision rollback, preview, login identity/password, faculty exclusion and idempotence.");
        }
        finally{await Sql("DELETE FROM audit_events WHERE entity_id IN ($1,$2,$3)",student,faculty,collision);await Sql("DELETE FROM portal_credentials WHERE user_id IN ($1,$2)",student,faculty);await Sql("DELETE FROM app_users WHERE id IN ($1,$2,$3)",student,faculty,collision);}
    }
}
