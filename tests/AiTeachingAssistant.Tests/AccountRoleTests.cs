using AiTeachingAssistant.Services;
using AiTeachingAssistant.Models;
using Npgsql;
namespace AiTeachingAssistant.Tests;
public class AccountRoleTests
{
    [FormativeDatabaseFact] public async Task RoleChangesAreAuthorizedVersionedAndAudited()
    {
        await using var db=NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("FORMATIVE_TEST_CONNECTION")!);
        var actor=Guid.NewGuid();var target=Guid.NewGuid();
        try {
            foreach(var (id,role) in new[]{(actor,PortalRoles.Administrator),(target,PortalRoles.Member)}) {
                await using(var cmd=db.CreateCommand("INSERT INTO app_users(id,display_name,email) VALUES($1,'Synthetic role test',$2)")){cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue($"role-{id}@example.invalid");await cmd.ExecuteNonQueryAsync();}
                await using(var cmd=db.CreateCommand("INSERT INTO portal_credentials(user_id,normalized_email,password_hash,platform_role) VALUES($1,$2,'not-a-login-hash',$3)")){cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue($"role-{id}@example.invalid".ToUpperInvariant());cmd.Parameters.AddWithValue(role);await cmd.ExecuteNonQueryAsync();}
            }
            var manager=new AccountRoleManager(db);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>manager.Change(target,actor,PortalRoles.Member,PortalRoles.Administrator,default));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>manager.Change(actor,actor,PortalRoles.Member,PortalRoles.Administrator,default));
            await manager.Change(actor,target,PortalRoles.Administrator,PortalRoles.Member,default);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>manager.Change(actor,target,PortalRoles.Administrator,PortalRoles.Member,default));
            await Assert.ThrowsAsync<ArgumentException>(()=>manager.Change(actor,target,"admin",PortalRoles.Administrator,default));
            await using(var check=db.CreateCommand("SELECT platform_role FROM portal_credentials WHERE user_id=$1")){check.Parameters.AddWithValue(target);Assert.Equal(PortalRoles.Administrator,await check.ExecuteScalarAsync());}
            await using(var check=db.CreateCommand("SELECT count(*) FROM audit_events WHERE entity_id=$1 AND action='account.permissions_changed'")){check.Parameters.AddWithValue(target);Assert.Equal(1L,await check.ExecuteScalarAsync());}
            await manager.Change(actor,target,PortalRoles.Member,PortalRoles.Administrator,default);
        } finally {
            foreach(var id in new[]{target,actor}) foreach(var sql in new[]{"DELETE FROM audit_events WHERE entity_id=$1 OR actor_user_id=$1","DELETE FROM portal_credentials WHERE user_id=$1","DELETE FROM app_users WHERE id=$1"}) {await using var cmd=db.CreateCommand(sql);cmd.Parameters.AddWithValue(id);await cmd.ExecuteNonQueryAsync();}
        }
    }
}
