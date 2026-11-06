using AiTeachingAssistant.Models;
using Npgsql;

namespace AiTeachingAssistant.Services;

public sealed class AccountRoleManager(NpgsqlDataSource source)
{
    public async Task Change(Guid actor, Guid target, string role, string expectedRole, CancellationToken ct,bool canCreateCourses=false,bool? expectedCanCreateCourses=null)
    {
        if (!PortalRoles.IsValid(role) || !PortalRoles.IsValid(expectedRole)) throw new ArgumentException("請選擇有效的平台角色。");
        await using var conn=await source.OpenConnectionAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(ct);
        await using(var gate=new NpgsqlCommand("SELECT pg_advisory_xact_lock(741026002)",conn,tx)) await gate.ExecuteNonQueryAsync(ct);
        await using(var check=new NpgsqlCommand("SELECT c.platform_role FROM portal_credentials c JOIN app_users u ON u.id=c.user_id WHERE c.user_id=$1 AND u.status='active'",conn,tx))
        {
            check.Parameters.AddWithValue(actor);
            if(await check.ExecuteScalarAsync(ct) is not string actorRole || actorRole!=PortalRoles.Administrator) throw new UnauthorizedAccessException("只有管理員可以調整平台權限。");
        }
        await using var read=new NpgsqlCommand("SELECT platform_role,can_create_courses FROM portal_credentials WHERE user_id=$1 FOR UPDATE",conn,tx);
        read.Parameters.AddWithValue(target);
        string? current;bool priorCreate=false;await using(var r=await read.ExecuteReaderAsync(ct)){current=await r.ReadAsync(ct)?r.GetString(0):null;if(current!=null)priorCreate=r.GetBoolean(1); }
        if(current is null) throw new KeyNotFoundException("找不到此帳號。");
        if(current!=expectedRole || (expectedCanCreateCourses.HasValue && priorCreate!=expectedCanCreateCourses.Value)) throw new InvalidOperationException("角色已由其他操作更新，請重新載入後再試。");
        if(actor==target && role!=PortalRoles.Administrator) throw new InvalidOperationException("不能在此降低自己的管理權限，請由另一位管理員操作。");
        if(current==PortalRoles.Administrator && role!=PortalRoles.Administrator)
        {
            await using var count=new NpgsqlCommand("SELECT count(*) FROM portal_credentials c JOIN app_users u ON u.id=c.user_id WHERE c.platform_role='Administrator' AND u.status='active'",conn,tx);
            if((long)(await count.ExecuteScalarAsync(ct))!<=1) throw new InvalidOperationException("必須保留至少一位有效管理員。");
        }
        if(current!=role || priorCreate!=canCreateCourses)
        {
            await using var update=new NpgsqlCommand("UPDATE portal_credentials SET platform_role=$2,can_create_courses=$3 WHERE user_id=$1",conn,tx);
            update.Parameters.AddWithValue(target);update.Parameters.AddWithValue(role);update.Parameters.AddWithValue(canCreateCourses);await update.ExecuteNonQueryAsync(ct);
            await using var audit=new NpgsqlCommand("INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata) VALUES($1,'app_users',$2,'account.permissions_changed',jsonb_build_object('before',$3::text,'after',$4::text,'canCreateBefore',$5::boolean,'canCreateAfter',$6::boolean))",conn,tx);
            audit.Parameters.AddWithValue(actor);audit.Parameters.AddWithValue(target);audit.Parameters.AddWithValue(current);audit.Parameters.AddWithValue(role);audit.Parameters.AddWithValue(priorCreate);audit.Parameters.AddWithValue(canCreateCourses);await audit.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
