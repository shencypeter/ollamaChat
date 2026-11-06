using AiTeachingAssistant.Models;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace AiTeachingAssistant.Services;

public interface IPortalAccountStore
{
    Task<bool> NeedsSetupAsync(CancellationToken ct);
    Task<PortalUser?> AuthenticateAsync(string email, string password, CancellationToken ct);
    Task<PortalAccount?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PortalUser>> ListAsync(CancellationToken ct);
    Task<PortalUser> CreateAsync(CreateAccountRequest request, bool bootstrap, CancellationToken ct, Guid? actorId = null);
    Task<bool> ChangePasswordAsync(Guid id, string current, string password, CancellationToken ct);
}
public sealed class PortalAccountStore(NpgsqlDataSource source, IPasswordHasher<PortalUser> hasher) : IPortalAccountStore
{
    public async Task<bool> NeedsSetupAsync(CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("SELECT NOT EXISTS(SELECT 1 FROM portal_credentials)");
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }
    public async Task<PortalAccount?> GetAsync(Guid id, CancellationToken ct) => await FindAsync("c.user_id=$1", id, ct);
    private async Task<PortalAccount?> FindAsync(string predicate, object value, CancellationToken ct)
    {
        await using var cmd = source.CreateCommand($"SELECT u.id,u.display_name,u.email,c.platform_role,c.password_hash,u.status,u.student_number,c.can_create_courses,EXISTS(SELECT 1 FROM course_members m WHERE m.user_id=u.id AND m.status='active' AND m.course_role IN ('teacher','ta')),EXISTS(SELECT 1 FROM course_members m WHERE m.user_id=u.id AND m.status='active' AND m.course_role='student') FROM app_users u JOIN portal_credentials c ON c.user_id=u.id WHERE {predicate}");
        cmd.Parameters.AddWithValue(value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.IsDBNull(6)?null:r.GetString(6),r.GetBoolean(7),r.GetBoolean(8),r.GetBoolean(9)),r.GetString(4),r.GetString(5)) : null;
    }
    public async Task<PortalUser?> AuthenticateAsync(string email, string password, CancellationToken ct)
    {
        var account = await FindAsync("c.normalized_email=$1", email.Trim().ToUpperInvariant(), ct);
        // Always run a password verification, including unknown addresses.
        var dummy = new PortalUser(Guid.Empty,"","",PortalRoles.Member);
        if (account is null) { hasher.VerifyHashedPassword(dummy, DummyHash.Value, password); return null; }
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using var locked = new NpgsqlCommand("SELECT locked_until > now() FROM portal_credentials WHERE user_id=$1 FOR UPDATE",conn,tx);
        locked.Parameters.AddWithValue(account.User.Id);
        var isLocked = await locked.ExecuteScalarAsync(ct) is true;
        var result = hasher.VerifyHashedPassword(account.User,account.PasswordHash,password);
        if (isLocked || account.Status != "active") return null;
        if (result == PasswordVerificationResult.Failed)
        {
            await using var fail = new NpgsqlCommand("UPDATE portal_credentials SET failed_login_count=failed_login_count+1,locked_until=CASE WHEN failed_login_count>=4 THEN now()+interval '15 minutes' ELSE NULL END WHERE user_id=$1",conn,tx);
            fail.Parameters.AddWithValue(account.User.Id); await fail.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct); return null;
        }
        await using var reset = new NpgsqlCommand("UPDATE portal_credentials SET failed_login_count=0,locked_until=NULL,password_hash=$2 WHERE user_id=$1",conn,tx);
        reset.Parameters.AddWithValue(account.User.Id);
        reset.Parameters.AddWithValue(result == PasswordVerificationResult.SuccessRehashNeeded ? hasher.HashPassword(account.User,password) : account.PasswordHash);
        await reset.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct); return account.User;
    }
    private static readonly Lazy<string> DummyHash = new(() => new PasswordHasher<PortalUser>().HashPassword(new(Guid.Empty,"","",PortalRoles.Member),Guid.NewGuid().ToString()));
    public async Task<IReadOnlyList<PortalUser>> ListAsync(CancellationToken ct)
    {
        await using var cmd = source.CreateCommand("SELECT u.id,u.display_name,u.email,c.platform_role,u.student_number,c.can_create_courses FROM app_users u JOIN portal_credentials c ON c.user_id=u.id ORDER BY u.created_at");
        await using var r=await cmd.ExecuteReaderAsync(ct); var users=new List<PortalUser>();
        while(await r.ReadAsync(ct)) users.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.IsDBNull(4)?null:r.GetString(4),r.GetBoolean(5)));
        return users;
    }
    public async Task<PortalUser> CreateAsync(CreateAccountRequest request, bool bootstrap, CancellationToken ct, Guid? actorId = null)
    {
        if (!PortalRoles.IsValid(request.Role) || request.Password.Length < 12 || string.IsNullOrWhiteSpace(request.DisplayName)) throw new ArgumentException("請使用有效角色、姓名與至少 12 字元密碼。");
        await using var conn=await source.OpenConnectionAsync(ct); await using var tx=await conn.BeginTransactionAsync(ct);
        // Serializes first-user setup across processes and also account creation.
        await using(var gate=new NpgsqlCommand("SELECT pg_advisory_xact_lock(741026002)",conn,tx)) await gate.ExecuteNonQueryAsync(ct);
        if(bootstrap)
        {
            await using var check=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM portal_credentials)",conn,tx);
            if(await check.ExecuteScalarAsync(ct) is true) throw new InvalidOperationException("平台已完成初始化。");
        }
        var user=new PortalUser(Guid.NewGuid(),request.DisplayName.Trim(),request.Email.Trim().ToLowerInvariant(),bootstrap?PortalRoles.Administrator:request.Role,string.IsNullOrWhiteSpace(request.StudentNumber)?null:request.StudentNumber.Trim(),bootstrap||request.CanCreateCourses);
        await using(var cmd=new NpgsqlCommand("INSERT INTO app_users(id,display_name,email,identity_provider,external_subject,student_number) VALUES($1,$2,$3,'local',$4,$5)",conn,tx))
        { cmd.Parameters.AddWithValue(user.Id);cmd.Parameters.AddWithValue(user.DisplayName);cmd.Parameters.AddWithValue(user.Email);cmd.Parameters.AddWithValue(user.Id.ToString());cmd.Parameters.Add(new(){NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Text,Value=(object?)user.StudentNumber??DBNull.Value});await cmd.ExecuteNonQueryAsync(ct); }
        await using(var cmd=new NpgsqlCommand("INSERT INTO portal_credentials(user_id,normalized_email,password_hash,platform_role,can_create_courses) VALUES($1,$2,$3,$4,$5)",conn,tx))
        { cmd.Parameters.AddWithValue(user.Id);cmd.Parameters.AddWithValue(user.Email.ToUpperInvariant());cmd.Parameters.AddWithValue(hasher.HashPassword(user,request.Password));cmd.Parameters.AddWithValue(user.Role);cmd.Parameters.AddWithValue(user.CanCreateCourses);await cmd.ExecuteNonQueryAsync(ct); }
        await using(var audit=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action) VALUES($1,'app_users',$2,'account.created')",conn,tx))
        {audit.Parameters.AddWithValue(actorId ?? user.Id);audit.Parameters.AddWithValue(user.Id);await audit.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct);return user;
    }
    public async Task<bool> ChangePasswordAsync(Guid id,string current,string password,CancellationToken ct)
    {
        var account=await GetAsync(id,ct);
        if(account is null || hasher.VerifyHashedPassword(account.User,account.PasswordHash,current)==PasswordVerificationResult.Failed) return false;
        await using var cmd=source.CreateCommand("UPDATE portal_credentials SET password_hash=$2 WHERE user_id=$1");cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(hasher.HashPassword(account.User,password));await cmd.ExecuteNonQueryAsync(ct);return true;
    }
}
