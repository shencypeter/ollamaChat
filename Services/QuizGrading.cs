using System.Text.Json;
using AiTeachingAssistant.Models;
using Npgsql;
using NpgsqlTypes;
namespace AiTeachingAssistant.Services;

public sealed record QuizMark(Guid QuestionId,string QuestionType,decimal Points,decimal? AutoScore,bool? Correct);
public sealed record QuizGradingResult(IReadOnlyList<QuizMark> Marks,decimal? ObjectiveScore,int PendingEssays,bool Complete);
public static class QuizGrading
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public static decimal[] Allocate(decimal max,IReadOnlyList<decimal?> points)
    {
        if(points.Count==0||points.Any(p=>p.HasValue&&(p<=0||Math.Round(p.Value,2)!=p)))throw new AssignmentWorkflowException(400,"每題配分須為正數，最多兩位小數。");
        var count=points.Count(p=>!p.HasValue);var remaining=max-points.Sum(p=>p??0);
        if((count==0&&remaining!=0)||(count>0&&remaining<count*.01m))throw new AssignmentWorkflowException(400,"題目配分合計須等於測驗滿分，未設定題目需保留足夠配分。");
        var each=count==0?0:Math.Floor(remaining/count*100)/100;var result=new List<decimal>();
        foreach(var p in points){if(p.HasValue)result.Add(p.Value);else{count--;var value=count==0?remaining:each;result.Add(value);remaining-=value;}}return result.ToArray();
    }
    public static bool Correct(string answer,IReadOnlyList<int> key)
    {
        try{var selected=JsonSerializer.Deserialize<int[]>(answer);return selected!=null&&selected.Length==selected.Distinct().Count()&&selected.Order().SequenceEqual(key.Order());}catch(JsonException){return false;}
    }
    public static decimal Total(IReadOnlyList<QuizMark> marks,IReadOnlyDictionary<Guid,decimal> essays)
    {
        var expected=marks.Where(m=>m.QuestionType=="essay").ToArray();
        if(essays.Count!=expected.Length||essays.Keys.Any(id=>expected.All(m=>m.QuestionId!=id)))throw new AssignmentWorkflowException(400,"請評完所有申論題，不能修改客觀題分數。");
        foreach(var m in expected)if(!essays.TryGetValue(m.QuestionId,out var value)||value<0||value>m.Points||Math.Round(value,2)!=value)throw new AssignmentWorkflowException(400,"申論題分數須在該題配分內，最多兩位小數。");
        return marks.Sum(m=>m.AutoScore??essays[m.QuestionId]);
    }
    public static async Task<IReadOnlyList<QuizMark>?> Read(NpgsqlConnection conn,Guid snapshot,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("SELECT findings::text FROM quiz_grading WHERE snapshot_id=$1",conn);cmd.Parameters.AddWithValue(snapshot);
        return await cmd.ExecuteScalarAsync(ct) is string json?JsonSerializer.Deserialize<List<QuizMark>>(json,Json):null;
    }
    public static async Task Capture(NpgsqlConnection conn,Guid assignment,Guid snapshot,bool practice,CancellationToken ct)
    {
        decimal max;
        await using(var cmd=new NpgsqlCommand("SELECT a.max_score FROM assignments a JOIN course_grade_items i ON i.source_assignment_id=a.id WHERE a.id=$1",conn)){cmd.Parameters.AddWithValue(assignment);if(await cmd.ExecuteScalarAsync(ct) is not decimal value)return;max=value;}
        var raw=new List<(Guid Id,string Type,decimal? Points,int[] Key,string Answer)>();
        await using(var cmd=new NpgsqlCommand("SELECT q.id,q.question_type,q.max_score,q.standard_answer::text,v->>'text' FROM submission_revisions r CROSS JOIN LATERAL jsonb_array_elements(r.answers) v JOIN assignment_questions q ON q.id=(v->>'questionId')::uuid AND q.assignment_id=$2 WHERE r.id=$1 ORDER BY q.sort_order",conn)){
            cmd.Parameters.AddWithValue(snapshot);cmd.Parameters.AddWithValue(assignment);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))raw.Add((r.GetGuid(0),r.GetString(1),r.IsDBNull(2)?null:r.GetDecimal(2),JsonSerializer.Deserialize<int[]>(r.GetString(3))!,r.GetString(4)));
        }
        var points=Allocate(max,raw.Select(q=>q.Points).ToArray());var marks=raw.Select((q,i)=>new QuizMark(q.Id,q.Type,points[i],q.Type=="essay"?null:Correct(q.Answer,q.Key)?points[i]:0,q.Type=="essay"?null:Correct(q.Answer,q.Key))).ToArray();
        await using(var cmd=new NpgsqlCommand("INSERT INTO quiz_grading(snapshot_id,findings) VALUES($1,$2)",conn)){cmd.Parameters.AddWithValue(snapshot);cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb,JsonSerializer.Serialize(marks,Json));await cmd.ExecuteNonQueryAsync(ct);}
        if(marks.All(m=>m.QuestionType!="essay")){
            await using var grade=new NpgsqlCommand("INSERT INTO grades(submission_revision_id,score,feedback,released_at) VALUES($1,$2,'客觀題已依標準答案自動批改；多選採全對得分。',CASE WHEN $3 THEN now() ELSE NULL END)",conn);grade.Parameters.AddWithValue(snapshot);grade.Parameters.AddWithValue(marks.Sum(m=>m.AutoScore!.Value));grade.Parameters.AddWithValue(practice);await grade.ExecuteNonQueryAsync(ct);
        }
    }
    public static Dictionary<Guid,decimal> Essays(IReadOnlyList<QuizMark> marks,Dictionary<Guid,decimal> supplied,decimal legacyTotal)
    {
        // Backward-compatible single-essay clients; objective points remain server controlled.
        if(supplied.Count==0&&marks.Count(m=>m.QuestionType=="essay")==1)return new(){[marks.Single(m=>m.QuestionType=="essay").QuestionId]=legacyTotal-marks.Sum(m=>m.AutoScore??0)};
        return supplied;
    }
}
