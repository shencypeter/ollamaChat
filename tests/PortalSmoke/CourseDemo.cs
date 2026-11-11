using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Npgsql;

internal static class CourseDemo
{
    private static Guid Id(string name) => new(SHA256.HashData(Encoding.UTF8.GetBytes("nsysu-review-demo-v1/"+name))[..16]);

    public static async Task Seed(bool groups=true)
    {
        if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development")
            throw new InvalidOperationException("The course demo requires Development.");
        var connection=Environment.GetEnvironmentVariable("ConnectionStrings__Portal");
        var password=Environment.GetEnvironmentVariable("PORTAL_DEMO_PASSWORD");
        if(string.IsNullOrEmpty(connection)||password is null||password.Length<12)
            throw new InvalidOperationException("Specify a development ConnectionStrings__Portal and a PORTAL_DEMO_PASSWORD of at least 12 characters.");
        await using var db=NpgsqlDataSource.Create(connection);
        await using var conn=await db.OpenConnectionAsync();
        await using var tx=await conn.BeginTransactionAsync();
        async Task<object?> Sql(string sql,params object[] args){
            await using var cmd=new NpgsqlCommand(sql,conn,tx);
            foreach(var arg in args)cmd.Parameters.AddWithValue(arg);
            return await cmd.ExecuteScalarAsync();
        }
        await Sql("SELECT pg_advisory_xact_lock(741026110)");
        if(await Sql("SELECT EXISTS(SELECT 1 FROM audit_events WHERE entity_id=$1 AND action='demo.course_portal.v1')",Id("seed")) is true){
            if(groups)await SeedGroups(conn,tx,password);
            await tx.CommitAsync();Console.WriteLine("Existing course demo verified.");return;
        }
        var hasher=new PasswordHasher<PortalUser>(Options.Create(new PasswordHasherOptions{IterationCount=210000}));
        var people=new[]{"faculty1","faculty2","ta"}.Concat(Enumerable.Range(1,10).Select(i=>$"student{i:00}")).ToArray();
        foreach(var key in people){
            var number=key.StartsWith("student")?"D115"+key[7..]:null;
            var name=key switch{"faculty1"=>"林示範教授","faculty2"=>"陳示範教授","ta"=>"周示範助教",_=>"示範學員 "+key[7..]};
            var user=new PortalUser(Id(key),name,key+"@portal-demo.example.invalid",PortalRoles.Member);
            await Sql("INSERT INTO app_users(id,display_name,email,student_number) VALUES($1,$2,$3,NULLIF($4,''))",user.Id,user.DisplayName,user.Email,number??"");
            await Sql("INSERT INTO portal_credentials(user_id,normalized_email,password_hash,platform_role,can_create_courses) VALUES($1,$2,$3,'Member',$4)",user.Id,user.Email.ToUpperInvariant(),hasher.HashPassword(user,password),key.StartsWith("faculty"));
        }
        string[] titles=["研究方法","學習心理學","教學評量"];
        for(int courseNo=0;courseNo<titles.Length;courseNo++){
            var course=Id("course"+courseNo);var teacher=Id(courseNo==2?"faculty2":"faculty1");
            await Sql("INSERT INTO courses(id,code,title,term_code,owner_user_id,status,class_label) VALUES($1,$2,$3,'115-1',$4,'active','示範班')",course,"DEMO-"+(courseNo+1),titles[courseNo]+"（示範）",teacher);
            await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,'teacher'),($1,$3,'ta')",course,teacher,Id("ta"));
            var students=Enumerable.Range(1,10).Where(n=>courseNo==0 || (n+courseNo)%3!=0).Select(n=>Id($"student{n:00}")).ToArray();
            foreach(var student in students)await Sql("INSERT INTO course_members(course_id,user_id,course_role) VALUES($1,$2,'student')",course,student);
            var pdf=Convert.FromBase64String("JVBERi0xLjQKMSAwIG9iago8PCAvVHlwZSAvQ2F0YWxvZyAvUGFnZXMgMiAwIFIgPj4KZW5kb2JqCjIgMCBvYmoKPDwgL1R5cGUgL1BhZ2VzIC9LaWRzIFszIDAgUl0gL0NvdW50IDEgPj4KZW5kb2JqCjMgMCBvYmoKPDwgL1R5cGUgL1BhZ2UgL1BhcmVudCAyIDAgUiAvTWVkaWFCb3ggWzAgMCAzMDAgMTYwXSAvUmVzb3VyY2VzIDw8IC9Gb250IDw8IC9GMSA0IDAgUiA+PiA+PiAvQ29udGVudHMgNSAwIFIgPj4KZW5kb2JqCjQgMCBvYmoKPDwgL1R5cGUgL0ZvbnQgL1N1YnR5cGUgL1R5cGUxIC9CYXNlRm9udCAvSGVsdmV0aWNhID4+CmVuZG9iago1IDAgb2JqCjw8IC9MZW5ndGggNTIgPj4Kc3RyZWFtCkJUIC9GMSAxNCBUZiAyMCA4MCBUZCAoQ291cnNlIHJlYWRpbmcgb3V0bGluZSkgVGogRVQKZW5kc3RyZWFtCmVuZG9iagp4cmVmCjAgNgowMDAwMDAwMDAwIDY1NTM1IGYgCjAwMDAwMDAwMDkgMDAwMDAgbiAKMDAwMDAwMDA1OCAwMDAwMCBuIAowMDAwMDAwMTE1IDAwMDAwIG4gCjAwMDAwMDAyNDEgMDAwMDAgbiAKMDAwMDAwMDMxMSAwMDAwMCBuIAp0cmFpbGVyCjw8IC9TaXplIDYgL1Jvb3QgMSAwIFIgPj4Kc3RhcnR4cmVmCjQxMwolJUVPRgo=");
            await Sql("INSERT INTO course_resources(id,course_id,file_name,content,uploaded_by,teaching_date,summary) VALUES($1,$2,'outline.pdf',$3,$4,'2026-11-10','本週閱讀重點與討論題目。'),($5,$2,'notes.md',$6,$4,NULL,'課前筆記')",Id("pdf"+courseNo),course,pdf,teacher,Id("notes"+courseNo),"# Synthetic notes"u8.ToArray());
            var thread=Id("thread"+courseNo);
            await Sql("INSERT INTO course_questions(id,course_id,author_id,question) VALUES($1,$2,$3,'如何區別觀察與推論？')",thread,course,students[0]);
            await Sql("INSERT INTO course_question_replies(id,question_id,author_id,body) VALUES($1,$2,$3,'先記錄可觀察到的行為，再解釋可能原因。')",Id("reply"+courseNo),thread,teacher);
            var quiz=Id("quiz"+courseNo);var report=Id("report"+courseNo);
            await Sql("INSERT INTO assignments(id,course_id,title,status,max_score,requires_start,time_limit_minutes) VALUES($1,$2,'單元測驗','published',60,true,40)",quiz,course);
            await Sql("INSERT INTO report_assignments(id,course_id,title,status,max_score,created_by) VALUES($1,$2,'閱讀報告','published',80,$3)",report,course,teacher);
            await Sql("INSERT INTO course_grade_items(id,course_id,title,category,max_score,weight,created_by,source_assignment_id) VALUES($1,$2,'單元測驗','quiz',60,2,$3,$4)",Id("quizItem"+courseNo),course,teacher,quiz);
            await Sql("INSERT INTO course_grade_items(id,course_id,title,category,max_score,weight,created_by,source_report_id) VALUES($1,$2,'閱讀報告','assignment',80,1,$3,$4)",Id("reportItem"+courseNo),course,teacher,report);
            var draft=Id("draft"+courseNo);
            await Sql("INSERT INTO assignments(id,course_id,title,status,requires_start) VALUES($1,$2,'期末測驗草稿','draft',true)",draft,course);
            await Sql("INSERT INTO course_grade_items(id,course_id,title,category,max_score,weight,created_by,source_assignment_id) VALUES($1,$2,'期末測驗草稿','quiz',100,1,$3,$4)",Id("draftItem"+courseNo),course,teacher,draft);
            await Sql("INSERT INTO assignment_questions(id,assignment_id,prompt_text,sort_order) VALUES($1,$2,'以實例解釋本學期的一個概念。',1)",Id("draftQuestion"+courseNo),draft);
            var objective=Id("objective"+courseNo);var essay=Id("essay"+courseNo);
            await Sql("""
                INSERT INTO assignment_questions(id,assignment_id,prompt_text,sort_order,question_type,options,standard_answer,max_score,section_title)
                VALUES($1,$2,'觀察紀錄應描述可觀察的行為。',1,'true_false','["是","非"]','[0]',20,'基本概念'),
                ($3,$2,'請用一句話說明觀察與推論的差異。',2,'essay','[]','[]',40,'應用')
                """,objective,quiz,essay);
            for(int n=0;n<3;n++){
                var learner=students[n];var submission=Id($"work{courseNo}-{n}");var snapshot=Id($"snapshot{courseNo}-{n}");
                await Sql("INSERT INTO quiz_learning_starts(assignment_id,user_id,is_practice,started_at,evidence) VALUES($1,$2,false,now()-interval '10 minutes','[]')",quiz,learner);
                await Sql("INSERT INTO submissions(id,assignment_id,student_user_id,attempt_no,status,submitted_at) VALUES($1,$2,$3,1,'submitted',now())",submission,quiz,learner);
                var rows=new List<object>();
                foreach(var (qid,answer) in new[]{(objective,n==2?"[1]":"[0]"),(essay,"觀察是行為紀錄；推論是對原因的解釋。")}){
                    var aid=Id($"answer{courseNo}-{n}-{qid}");var revision=Id($"rev{aid}");
                    await Sql("INSERT INTO submission_answers(id,submission_id,assignment_id,question_id) VALUES($1,$2,$3,$4)",aid,submission,quiz,qid);
                    await Sql("INSERT INTO answer_revisions(id,answer_id,revision_no,answer_text,created_by_user_id,is_submitted) VALUES($1,$2,1,$3,$4,true)",revision,aid,answer,learner);
                    await Sql("UPDATE submission_answers SET current_revision_id=$2 WHERE id=$1",aid,revision);
                    rows.Add(new{questionId=qid,revisionId=revision,text=answer});
                }
                await Sql("INSERT INTO submission_revisions(id,submission_id,revision_no,author_user_id,answers) VALUES($1,$2,1,$3,$4::jsonb)",snapshot,submission,learner,JsonSerializer.Serialize(rows));
                await QuizGrading.Capture(conn,quiz,snapshot,false,default);
                if(n<2){
                    await Sql("INSERT INTO grades(submission_revision_id,grader_user_id,score,essay_scores,feedback,released_at) VALUES($1,$2,50,$3::jsonb,'有區別兩者的依據。',CASE WHEN $4 THEN now() END)",snapshot,teacher,JsonSerializer.Serialize(new Dictionary<Guid,int>{{essay,30}}),n==0);
                    if(n==0)await AssessmentGradeSync.ReleaseQuiz(conn,quiz,snapshot,50,teacher,default);
                    await Sql("INSERT INTO report_submissions(id,report_id,student_id,file_name,content) VALUES($1,$2,$3,'report.pdf',$4)",Id($"pdfwork{courseNo}-{n}"),report,learner,pdf);
                }
            }
        }
        await Sql("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action) VALUES($1,'demo',$2,'demo.course_portal.v1')",Id("faculty1"),Id("seed"));
        if(groups)await SeedGroups(conn,tx,password);
        await tx.CommitAsync();
        Console.WriteLine("Prepared 3 courses, 2 professors, 1 TA and 10 students.");
    }
    private static async Task SeedGroups(NpgsqlConnection conn,NpgsqlTransaction tx,string password)
    {
        async Task<object?> Sql(string sql,params object[] values){
            await using var cmd=new NpgsqlCommand(sql,conn,tx);foreach(var v in values)cmd.Parameters.AddWithValue(v);
            return await cmd.ExecuteScalarAsync();
        }
        if(await Sql("SELECT EXISTS(SELECT 1 FROM audit_events WHERE entity_id=$1 AND action='demo.group_reports.v1')",Id("seed")) is true)return;
        var ta=Id("ta2");var email="ta2@portal-demo.example.invalid";
        var user=new PortalUser(ta,"評量示範助教",email,PortalRoles.Member);
        await Sql("INSERT INTO app_users(id,display_name,email) VALUES($1,$2,$3)",ta,user.DisplayName,email);
        await Sql("INSERT INTO portal_credentials(user_id,normalized_email,password_hash,platform_role) VALUES($1,$2,$3,'Member')",ta,email.ToUpperInvariant(),new PasswordHasher<PortalUser>().HashPassword(user,password));
        for(int c=0;c<3;c++){
            var course=Id("course"+c);var teacher=Id(c==2?"faculty2":"faculty1");
            await Sql("INSERT INTO course_members(course_id,user_id,course_role,ta_restrictions) VALUES($1,$2,'ta',ARRAY['grading'])",course,ta);
            var students=Enumerable.Range(1,10).Where(n=>c==0 || (n+c)%3!=0).Select(n=>Id($"student{n:00}")).ToArray();
            var groups=new[]{students.Take(students.Length/2).ToArray(),students.Skip(students.Length/2).ToArray()};
            var report=Id("groupReport"+c);var item=Id("groupItem"+c);
            await Sql("INSERT INTO report_assignments(id,course_id,title,status,created_by,max_score,submission_mode,evaluation_mode,allow_late,due_at) VALUES($1,$2,'小組觀察報告','published',$3,100,'group','consensus',true,now()-interval '1 day')",report,course,teacher);
            await Sql("INSERT INTO course_grade_items(id,course_id,title,category,max_score,weight,created_by,source_report_id) VALUES($1,$2,'小組觀察報告','assignment',100,1,$3,$4)",item,course,teacher,report);
            for(int g=0;g<groups.Length;g++){
                var members=groups[g];var leader=members[(c+g)%members.Length];var group=Id($"group{c}-{g}");
                await Sql("INSERT INTO course_groups(id,course_id,name) VALUES($1,$2,$3)",group,course,"觀察 "+(g+1)+" 組");
                foreach(var member in members)await Sql("INSERT INTO course_group_members(group_id,user_id,is_leader) VALUES($1,$2,$3)",group,member,member==leader);
                await Sql("INSERT INTO report_groups VALUES($1,$2,$3)",report,course,group);
                if(c==1 && g==1)continue;
                bool released=c==0;var submission=Id($"groupSubmission{c}-{g}");
                await Sql("""
                    INSERT INTO report_submissions(id,report_id,student_id,file_name,content,group_id,group_name,score,published_score,returned,submitted_at)
                    SELECT $1,$2,$3,'group.pdf',content,$4,$5,80,CASE WHEN $6 THEN 80 END,$7,now()-CASE WHEN $8 THEN interval '2 days' ELSE interval '1 hour' END
                    FROM course_resources WHERE id=$9
                    """,submission,report,leader,group,"觀察 "+(g+1)+" 組",released,c==2&&g==0,g==0,Id("pdf"+c));
                foreach(var m in members){
                    bool adjusted=g==1&&m==leader;
                    await Sql("""
                        INSERT INTO report_snapshot(submission_id,report_id,student_id,display_name,is_leader,override_score,reason,published_score)
                        SELECT $1,$2,id,display_name,$4,CASE WHEN $5 THEN 85 END,CASE WHEN $5 THEN '負責整合報告與修訂' ELSE '' END,
                         CASE WHEN $6 THEN CASE WHEN $5 THEN 85 ELSE 80 END END FROM app_users WHERE id=$3
                        """,submission,report,m,m==leader,adjusted,released);
                    if(released)await AssessmentGradeSync.SetScore(conn,item,course,m,adjusted?85:80,teacher,default);
                    if(c==1 && m==leader)continue;
                    await Sql("INSERT INTO report_votes VALUES($1,$2,$3,'')",submission,m,adjusted?"review":"agree");
                }
                await Sql("""
                    INSERT INTO report_peer_reviews SELECT a.submission_id,a.student_id,b.student_id,ARRAY[4,4,4,4],''
                    FROM report_snapshot a JOIN report_snapshot b ON a.submission_id=b.submission_id AND a.student_id<>b.student_id
                    WHERE a.submission_id=$1 AND ($2 OR NOT a.is_leader)
                    """,submission,c!=1);
                await Sql("INSERT INTO report_grade_events(submission_id,actor,details) VALUES($1,$2,jsonb_build_object('demo',true,'base',80,'published',$3::boolean))",submission,teacher,released);
            }
        }
        await Sql("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action) VALUES($1,'demo',$2,'demo.group_reports.v1')",Id("faculty1"),Id("seed"));
    }
}
