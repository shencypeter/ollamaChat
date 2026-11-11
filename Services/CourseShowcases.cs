using AiTeachingAssistant.Models;
using Npgsql;
using NpgsqlTypes;
namespace AiTeachingAssistant.Services;

public sealed class CourseShowcases(NpgsqlDataSource source)
{
 private static void Add(NpgsqlCommand c,object v)=>c.Parameters.AddWithValue(v);
 private static void NullableId(NpgsqlCommand c,Guid? v)=>c.Parameters.Add(new(){NpgsqlDbType=NpgsqlDbType.Uuid,Value=(object?)v??DBNull.Value});
 private static AssignmentWorkflowException Error(int code,string text)=>new(code,text);
 private sealed record Item(Guid Id,Guid Course,int Version,int Draft,int? Published,string Status,Guid? Snapshot,Guid? Question,Guid? Report,DateTimeOffset? PublishedAt);
 private sealed record Source(string Title,string Background,string Answer,string Student,bool Closed);
 public async Task<string> Access(NpgsqlConnection db,Guid course,Guid actor,bool practice,bool write,CancellationToken ct)
 {
  await using var c=new NpgsqlCommand("SELECT m.course_role,c.status FROM course_members m JOIN courses c ON c.id=m.course_id WHERE m.course_id=$1 AND m.user_id=$2 AND m.status='active'"+(write?" FOR SHARE OF c":""),db);Add(c,course);Add(c,actor);
  await using var r=await c.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw Error(403,"你不是本課有效成員。");
  var role=practice?"student":r.GetString(0);
  if(write&&(role is not ("teacher" or "ta")||r.GetString(1)=="archived"))throw Error(403,"本課目前不允許此編輯操作。");return role;
 }
 private static async Task<Item> Load(NpgsqlConnection db,Guid course,Guid id,bool locked,CancellationToken ct)
 {
  await using var c=new NpgsqlCommand("SELECT version,draft_revision,published_revision,status,source_snapshot_id,source_question_id,source_report_submission_id,published_at FROM course_showcases WHERE course_id=$1 AND id=$2"+(locked?" FOR UPDATE":""),db);Add(c,course);Add(c,id);await using var r=await c.ExecuteReaderAsync(ct);
  if(!await r.ReadAsync(ct))throw Error(404,"找不到觀摩項目。");return new(id,course,r.GetInt32(0),r.GetInt32(1),r.IsDBNull(2)?null:r.GetInt32(2),r.GetString(3),r.IsDBNull(4)?null:r.GetGuid(4),r.IsDBNull(5)?null:r.GetGuid(5),r.IsDBNull(6)?null:r.GetGuid(6),r.IsDBNull(7)?null:r.GetFieldValue<DateTimeOffset>(7));
 }
 private static void Version(Item item,int expected){if(expected<1||item.Version!=expected)throw Error(409,"觀摩版本已變更，請重新載入後確認。");}
 private static async Task<Source> SourceContent(NpgsqlConnection db,Guid course,Guid? snapshot,Guid? question,Guid? report,bool requireClosed,bool locked,CancellationToken ct)
 {
  if((snapshot.HasValue&&question.HasValue&&!report.HasValue)==false && (!snapshot.HasValue&&!question.HasValue&&report.HasValue)==false)throw Error(400,"請選擇一個申論作答版本或PDF提交來源。");
  string sql=report.HasValue?"""
   SELECT a.title,a.instructions,''::text,u.display_name,a.status='closed'
   FROM report_submissions s JOIN report_assignments a ON a.id=s.report_id JOIN app_users u ON u.id=s.student_id
   JOIN course_members m ON m.user_id=u.id AND m.course_id=a.course_id AND m.course_role='student' AND m.status='active'
   WHERE s.id=$1 AND a.course_id=$2 AND NOT s.is_practice AND s.group_id IS NULL AND s.published_score IS NOT NULL AND u.status='active'
   """:"""
   SELECT a.title,q.prompt_text,v->>'text',u.display_name,a.status='closed' AND NOT a.allow_revisions
   FROM submission_revisions r JOIN submissions s ON s.id=r.submission_id JOIN assignments a ON a.id=s.assignment_id
   JOIN course_grade_items gi ON gi.source_assignment_id=a.id
   JOIN app_users u ON u.id=s.student_user_id JOIN course_members m ON m.user_id=u.id AND m.course_id=a.course_id AND m.course_role='student' AND m.status='active'
   JOIN LATERAL jsonb_array_elements(r.answers) v ON true JOIN assignment_questions q ON q.id=(v->>'questionId')::uuid AND q.assignment_id=a.id
   JOIN LATERAL (SELECT released_at FROM grades WHERE submission_revision_id=r.id ORDER BY graded_at DESC,id DESC LIMIT 1) g ON g.released_at IS NOT NULL
   WHERE r.id=$1 AND a.course_id=$2 AND q.id=$3 AND q.question_type='essay' AND s.status='submitted' AND NOT s.is_practice AND u.status='active'
     AND NOT EXISTS(SELECT 1 FROM submission_revisions newer WHERE newer.submission_id=s.id AND newer.revision_no>r.revision_no)
   """;
  await using var c=new NpgsqlCommand(sql+(locked?(report.HasValue?" FOR SHARE OF a":" FOR SHARE OF a"):""),db);Add(c,report??snapshot!.Value);Add(c,course);if(!report.HasValue)Add(c,question!.Value);
  await using var r=await c.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw Error(404,"來源不是本課有效的正式已評分提交，或已被更新／撤銷。");
  var result=new Source(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetBoolean(4));if(requireClosed&&!result.Closed)throw Error(409,"來源評量須關閉且不可再作答，才能公開觀摩。");return result;
 }
 private static async Task Audit(NpgsqlConnection db,Guid actor,Guid id,string action,CancellationToken ct)
 {
  await using var c=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata) SELECT $1,'course_showcase',id,$3,jsonb_build_object('version',version,'draftRevision',draft_revision,'publishedRevision',published_revision) FROM course_showcases WHERE id=$2",db);Add(c,actor);Add(c,id);Add(c,action);await c.ExecuteNonQueryAsync(ct);
 }
 public async Task<object> Sources(Guid course,Guid actor,bool practice,CancellationToken ct)
 {
  await using var db=await source.OpenConnectionAsync(ct);var role=await Access(db,course,actor,practice,false,ct);if(role is not ("teacher" or "ta"))throw Error(403,"只有本課教學人員可選來源。");
  var candidates=new List<(Guid? Snapshot,Guid? Question,Guid? Report)>();
  await using(var c=new NpgsqlCommand("SELECT r.id,q.id FROM submission_revisions r JOIN submissions s ON s.id=r.submission_id JOIN assignments a ON a.id=s.assignment_id JOIN assignment_questions q ON q.assignment_id=a.id AND q.question_type='essay' WHERE a.course_id=$1 AND NOT s.is_practice AND s.status='submitted' ORDER BY r.submitted_at DESC,r.id,q.sort_order LIMIT 200",db)){Add(c,course);await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))candidates.Add((r.GetGuid(0),r.GetGuid(1),null));}
  await using(var c=new NpgsqlCommand("SELECT s.id FROM report_submissions s JOIN report_assignments a ON a.id=s.report_id WHERE a.course_id=$1 AND NOT s.is_practice AND s.group_id IS NULL AND s.published_score IS NOT NULL ORDER BY s.submitted_at DESC,s.id LIMIT 200",db)){Add(c,course);await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))candidates.Add((null,null,r.GetGuid(0)));}
  var rows=new List<object>();foreach(var c in candidates){try{var v=await SourceContent(db,course,c.Snapshot,c.Question,c.Report,false,false,ct);rows.Add(new{snapshotId=c.Snapshot,questionId=c.Question,reportSubmissionId=c.Report,kind=c.Report.HasValue?"pdf":"essay",title=v.Title,studentName=v.Student,canPublish=v.Closed});}catch(AssignmentWorkflowException e)when(e.StatusCode==404){}}
  return rows;
 }
 public async Task<Guid> Create(Guid course,Guid actor,bool practice,ShowcaseCreateInput input,CancellationToken ct)
 {
  await using var db=await source.OpenConnectionAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);await Access(db,course,actor,practice,true,ct);
  var content=await SourceContent(db,course,input.SnapshotId,input.QuestionId,input.ReportSubmissionId,false,true,ct);var id=Guid.NewGuid();
  await using(var c=new NpgsqlCommand("INSERT INTO course_showcases(id,course_id,source_snapshot_id,source_question_id,source_report_submission_id,created_by) VALUES($1,$2,$3,$4,$5,$6)",db)){Add(c,id);Add(c,course);NullableId(c,input.SnapshotId);NullableId(c,input.QuestionId);NullableId(c,input.ReportSubmissionId);Add(c,actor);await c.ExecuteNonQueryAsync(ct);}
  await using(var c=new NpgsqlCommand("INSERT INTO course_showcase_versions(showcase_id,revision,title,background,answer,created_by) VALUES($1,1,'優良作答範例',$2,$3,$4)",db)){Add(c,id);Add(c,content.Background);Add(c,content.Answer);Add(c,actor);await c.ExecuteNonQueryAsync(ct);}
  await Audit(db,actor,id,"showcase.draft_created",ct);await tx.CommitAsync(ct);return id;
 }
 private static async Task<ShowcaseContentInput> ReadContent(NpgsqlConnection db,Item item,int revision,CancellationToken ct)
 {
  await using var c=new NpgsqlCommand("SELECT title,background,answer,notes FROM course_showcase_versions WHERE showcase_id=$1 AND revision=$2",db);Add(c,item.Id);Add(c,revision);await using var r=await c.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw Error(404,"找不到觀摩版本。");return new(){Title=r.GetString(0),Background=r.GetString(1),Answer=r.GetString(2),Notes=r.GetString(3),ExpectedVersion=item.Version};
 }
 private static async Task<bool> HasPdf(NpgsqlConnection db,Item item,int rev,CancellationToken ct){await using var c=new NpgsqlCommand("SELECT pdf IS NOT NULL FROM course_showcase_versions WHERE showcase_id=$1 AND revision=$2",db);Add(c,item.Id);Add(c,rev);return await c.ExecuteScalarAsync(ct) is true;}
 private static async Task<ShowcasePublic> Public(NpgsqlConnection db,Item item,int rev,CancellationToken ct){var v=await ReadContent(db,item,rev,ct);return new(item.Id,v.Title,v.Background,v.Answer,v.Notes,await HasPdf(db,item,rev,ct),item.PublishedAt);}
 public async Task<object> Get(Guid course,Guid id,Guid actor,bool practice,bool preview,CancellationToken ct)
 {
  await using var db=await source.OpenConnectionAsync(ct);var role=await Access(db,course,actor,practice,false,ct);var item=await Load(db,course,id,false,ct);var staff=role is "teacher" or "ta";
  if(preview&&!staff)throw Error(403,"只有本課教學人員可預覽草稿。");
  if(staff&&!preview)return new ShowcaseDraft(id,item.Version,item.Draft,item.Published,item.Status,item.Report.HasValue?"pdf":"essay",await ReadContent(db,item,item.Draft,ct),await HasPdf(db,item,item.Draft,ct),item.PublishedAt);
  if(preview)return await Public(db,item,item.Draft,ct);
  if(item.Status!="published"||!item.Published.HasValue)throw Error(404,"找不到可閱覽的觀摩。");
  try{await SourceContent(db,course,item.Snapshot,item.Question,item.Report,true,false,ct);}catch(AssignmentWorkflowException){throw Error(404,"找不到可閱覽的觀摩。");}
  return await Public(db,item,item.Published.Value,ct);
 }
 public async Task<object> List(Guid course,Guid actor,bool practice,CancellationToken ct)
 {
  await using var db=await source.OpenConnectionAsync(ct);var role=await Access(db,course,actor,practice,false,ct);var staff=role is "teacher" or "ta";var ids=new List<Guid>();
  await using(var c=new NpgsqlCommand("SELECT id FROM course_showcases WHERE course_id=$1 AND ($2 OR status='published') ORDER BY created_at DESC,id",db)){Add(c,course);Add(c,staff);await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))ids.Add(r.GetGuid(0));}
  await db.CloseAsync();
  var rows=new List<object>();foreach(var id in ids){try{rows.Add(await Get(course,id,actor,practice,false,ct));}catch(AssignmentWorkflowException e)when(!staff&&e.StatusCode==404){}}return new{staff,canPublish=role=="teacher",items=rows};
 }
 public async Task Save(Guid course,Guid id,Guid actor,bool practice,ShowcaseContentInput input,byte[]? pdf,CancellationToken ct)
 {
  if(string.IsNullOrWhiteSpace(input.Title)||input.Title.Length>200||input.Background==null||input.Background.Length>24000||input.Answer==null||input.Answer.Length>64000||input.Notes==null||input.Notes.Length>16000)throw Error(400,"觀摩內容長度或標題無效。");
  await using var db=await source.OpenConnectionAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);await Access(db,course,actor,practice,true,ct);var item=await Load(db,course,id,true,ct);Version(item,input.ExpectedVersion);
  if(pdf!=null&&item.Report==null)throw Error(400,"只有PDF報告觀摩可上傳展示副本。");
  var next=item.Version+1;
  await using(var c=new NpgsqlCommand("INSERT INTO course_showcase_versions(showcase_id,revision,title,background,answer,notes,pdf,created_by) SELECT showcase_id,$3,$4,$5,$6,$7,CASE WHEN $8::bytea IS NULL THEN pdf ELSE $8 END,$9 FROM course_showcase_versions WHERE showcase_id=$1 AND revision=$2",db)){Add(c,id);Add(c,item.Draft);Add(c,next);Add(c,input.Title.Trim());Add(c,input.Background.Trim());Add(c,input.Answer.Trim());Add(c,input.Notes.Trim());c.Parameters.Add(new(){NpgsqlDbType=NpgsqlDbType.Bytea,Value=(object?)pdf??DBNull.Value});Add(c,actor);await c.ExecuteNonQueryAsync(ct);}
  await using(var c=new NpgsqlCommand("UPDATE course_showcases SET version=$2,draft_revision=$2 WHERE id=$1",db)){Add(c,id);Add(c,next);await c.ExecuteNonQueryAsync(ct);}
  await Audit(db,actor,id,pdf==null?"showcase.draft_saved":"showcase.display_pdf_uploaded",ct);await tx.CommitAsync(ct);
 }
 public async Task Upload(Guid course,Guid id,Guid actor,bool practice,int expected,byte[] pdf,CancellationToken ct)
 {
  await using(var check=await source.OpenConnectionAsync(ct)){await Access(check,course,actor,practice,true,ct);}
  if(pdf.Length is <1 or >20971520||!pdf.AsSpan().StartsWith("%PDF-"u8))throw Error(400,"請上傳20MB內的有效PDF展示副本。");
  if(await Get(course,id,actor,practice,false,ct) is not ShowcaseDraft draft)throw Error(403,"只有本課教學人員可上傳展示副本。");draft.Content.ExpectedVersion=expected;await Save(course,id,actor,practice,draft.Content,pdf,ct);
 }
 public async Task Publish(Guid course,Guid id,Guid actor,bool practice,ShowcasePublishInput input,CancellationToken ct)
 {
  if(!input.Anonymized||!input.Authorized||!input.NoAnswerLeak||string.IsNullOrWhiteSpace(input.AuthorizationReference)||input.AuthorizationReference.Length>500)throw Error(400,"請確認去識別、使用授權、防洩題與授權依據後發布。");
  await using var db=await source.OpenConnectionAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);var role=await Access(db,course,actor,practice,true,ct);if(role!="teacher")throw Error(403,"只有本課教師可最終發布觀摩。");var item=await Load(db,course,id,true,ct);Version(item,input.ExpectedVersion);
  await SourceContent(db,course,item.Snapshot,item.Question,item.Report,true,true,ct);
  var content=await ReadContent(db,item,item.Draft,ct);if(item.Report.HasValue?!await HasPdf(db,item,item.Draft,ct):string.IsNullOrWhiteSpace(content.Answer))throw Error(400,"請先準備已去識別的展示作答或另外上傳PDF副本。");
  await using(var approval=new NpgsqlCommand("INSERT INTO course_showcase_publications(showcase_id,revision,approved_by,authorization_reference,anonymized,authorized,no_answer_leak) VALUES($1,$2,$3,$4,true,true,true)",db)){Add(approval,id);Add(approval,item.Draft);Add(approval,actor);Add(approval,input.AuthorizationReference.Trim());await approval.ExecuteNonQueryAsync(ct);}
  await using(var c=new NpgsqlCommand("UPDATE course_showcases SET status='published',version=version+1,published_revision=draft_revision,published_at=now(),withdrawn_at=NULL,approved_by=$2,authorization_reference=$3 WHERE id=$1",db)){Add(c,id);Add(c,actor);Add(c,input.AuthorizationReference.Trim());await c.ExecuteNonQueryAsync(ct);}
  await Audit(db,actor,id,"showcase.published",ct);await tx.CommitAsync(ct);
 }
 public async Task Withdraw(Guid course,Guid id,Guid actor,bool practice,int expected,CancellationToken ct)
 {
  await using var db=await source.OpenConnectionAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);var role=await Access(db,course,actor,practice,false,ct);if(role is not ("teacher" or "ta"))throw Error(403,"只有本課教學人員可撤下觀摩。");var item=await Load(db,course,id,true,ct);
  if(item.Status!="withdrawn"){Version(item,expected);await using var c=new NpgsqlCommand("UPDATE course_showcases SET status='withdrawn',version=version+1,withdrawn_at=now() WHERE id=$1",db);Add(c,id);await c.ExecuteNonQueryAsync(ct);await Audit(db,actor,id,"showcase.withdrawn",ct);}await tx.CommitAsync(ct);
 }
 public async Task<byte[]> Attachment(Guid course,Guid id,Guid actor,bool practice,bool draft,CancellationToken ct)
 {
  await using var db=await source.OpenConnectionAsync(ct);var role=await Access(db,course,actor,practice,false,ct);var staff=role is "teacher" or "ta";
  if(draft&&!staff)throw Error(403,"無法讀取草稿附件。");var item=await Load(db,course,id,false,ct);int rev;
  if(draft)rev=item.Draft;
  else{if(item.Status!="published"||!item.Published.HasValue)throw Error(404,"找不到觀摩附件。");try{await SourceContent(db,course,item.Snapshot,item.Question,item.Report,true,false,ct);}catch(AssignmentWorkflowException){throw Error(404,"找不到觀摩附件。");}rev=item.Published.Value;}
  await using var c=new NpgsqlCommand("SELECT pdf FROM course_showcase_versions WHERE showcase_id=$1 AND revision=$2",db);Add(c,id);Add(c,rev);return await c.ExecuteScalarAsync(ct) as byte[]??throw Error(404,"找不到展示附件。");
 }
}
