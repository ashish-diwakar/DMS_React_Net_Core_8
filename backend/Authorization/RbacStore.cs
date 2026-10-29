using System.Globalization;
using Diwakar.Cms.Api.Authentication;
using MySqlConnector;

namespace Diwakar.Cms.Api.Authorization;

public sealed record RbacSnapshot(bool IsSuperAdmin, IReadOnlySet<string> Permissions, IReadOnlyList<string> Roles)
{
    public bool Allows(string permission) => permission == "super-admin.manage" ? IsSuperAdmin : IsSuperAdmin || Permissions.Contains(permission);
}

public enum CreateAdminStatus { Created, Invalid, Duplicate, Forbidden }
public sealed record CreateAdminResult(CreateAdminStatus Status, ulong? Id = null, string? Name = null, string? Email = null);

public sealed class RbacStore(MySqlDataSource source, PasswordHasher hasher)
{
    public static readonly string[] ManagedPermissions = ["admin.access", "users.view", "users.assign_roles", "roles.view", "roles.manage_permissions", "settings.manage", "media.manage", "pages.manage"];
    private const string Model = "App\\Models\\User";

    public async Task<RbacSnapshot> GetAsync(ulong userId, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.name FROM model_has_roles m JOIN roles r ON r.id=m.role_id
            WHERE m.model_type=@model AND m.model_id=@id AND r.guard_name='web'
            """;
        command.Parameters.AddWithValue("@model", Model); command.Parameters.AddWithValue("@id", userId);
        var roles = new List<string>(); await using (var reader = await command.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct)) roles.Add(reader.GetString(0));
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = """
                SELECT DISTINCT p.name FROM permissions p WHERE p.guard_name='web' AND (
                  EXISTS(SELECT 1 FROM model_has_permissions mp WHERE mp.permission_id=p.id AND mp.model_type=@model AND mp.model_id=@id)
                  OR EXISTS(SELECT 1 FROM model_has_roles mr JOIN roles r ON r.id=mr.role_id AND r.guard_name='web' JOIN role_has_permissions rp ON rp.role_id=mr.role_id
                    WHERE mr.model_type=@model AND mr.model_id=@id AND rp.permission_id=p.id))
                """;
            c.Parameters.AddWithValue("@model", Model); c.Parameters.AddWithValue("@id", userId);
            await using var reader = await c.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) permissions.Add(reader.GetString(0));
        }
        return new RbacSnapshot(roles.Contains("Super Admin", StringComparer.Ordinal), permissions, roles);
    }

    public async Task<object> DashboardAsync(RbacSnapshot actor, CancellationToken ct)
    {
        if (!actor.IsSuperAdmin && !actor.Allows("users.view"))
            return new { scope = "personal", primaryRole = actor.Roles.FirstOrDefault(), effectivePermissionCount = actor.Permissions.Count };

        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        if (actor.IsSuperAdmin)
        {
            command.CommandText = "SELECT (SELECT COUNT(*) FROM users),(SELECT COUNT(*) FROM roles WHERE guard_name='web'),(SELECT COUNT(*) FROM permissions WHERE guard_name='web')";
            await using var reader = await command.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            return new { scope = "global", userCount = reader.GetUInt64(0), roleCount = reader.GetUInt64(1), permissionCount = reader.GetUInt64(2) };
        }

        command.CommandText = "SELECT COUNT(*) FROM users";
        var userCount = Convert.ToUInt64(await command.ExecuteScalarAsync(ct));
        return new { scope = "personal", primaryRole = actor.Roles.FirstOrDefault(), effectivePermissionCount = actor.Permissions.Count, userCount };
    }
    public async Task<object> UsersAsync(CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct); await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT u.id,u.name,u.email,u.email_verified_at,
              (SELECT GROUP_CONCAT(r.name ORDER BY r.name SEPARATOR ', ') FROM model_has_roles mr JOIN roles r ON r.id=mr.role_id
               WHERE mr.model_type=@model AND mr.model_id=u.id AND r.guard_name='web')
            FROM users u ORDER BY u.name,u.id LIMIT 500
            """; command.Parameters.AddWithValue("@model", Model);
        var rows = new List<object>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new { id=reader.GetUInt64(0).ToString(CultureInfo.InvariantCulture), name=reader.GetString(1), email=reader.GetString(2), emailVerified=!reader.IsDBNull(3), role=reader.IsDBNull(4)?null:reader.GetString(4) });
        return rows;
    }

    public async Task<object> AssignmentRolesAsync(CancellationToken ct)
    {
        await using var connection=await source.OpenConnectionAsync(ct);await using var command=connection.CreateCommand();command.CommandText="SELECT id,name FROM roles WHERE guard_name='web' ORDER BY name";var rows=new List<object>();await using var reader=await command.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))rows.Add(new{id=reader.GetUInt64(0).ToString(CultureInfo.InvariantCulture),name=reader.GetString(1)});return rows;
    }
    public async Task<object> RolesAsync(CancellationToken ct)
    {
        await using var connection=await source.OpenConnectionAsync(ct);
        var rows=new List<object>();
        await using(var command=connection.CreateCommand())
        {
            command.CommandText="SELECT r.id,r.name,GROUP_CONCAT(p.name ORDER BY p.name SEPARATOR ', ') FROM roles r LEFT JOIN role_has_permissions rp ON rp.role_id=r.id LEFT JOIN permissions p ON p.id=rp.permission_id AND p.guard_name='web' WHERE r.guard_name='web' GROUP BY r.id,r.name ORDER BY r.name";
            await using var reader=await command.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))rows.Add(new{id=reader.GetUInt64(0).ToString(CultureInfo.InvariantCulture),name=reader.GetString(1),permissions=reader.IsDBNull(2)?Array.Empty<string>():reader.GetString(2).Split(", ",StringSplitOptions.RemoveEmptyEntries)});
        }
        var managed=new List<string>();
        await using(var command=connection.CreateCommand())
        {
            command.CommandText="SELECT name FROM permissions WHERE guard_name='web' AND name IN (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7) ORDER BY name";
            for(var i=0;i<ManagedPermissions.Length;i++)command.Parameters.AddWithValue("@p"+i,ManagedPermissions[i]);
            await using var reader=await command.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))managed.Add(reader.GetString(0));
        }
        return new{roles=rows,managedPermissions=managed};
    }
    public async Task<CreateAdminResult> CreateAdminAsync(ulong actor, string? rawName, string? rawEmail,
        string? password, string? confirmation, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await LockSuperAsync(connection, transaction, ct);
        var actorState = await GetInTransactionAsync(connection, transaction, actor, ct);
        if (!actorState.IsSuperAdmin) return new(CreateAdminStatus.Forbidden);

        var name = rawName?.Trim();
        var email = rawEmail?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.Any(char.IsControl)
            || !AuthValidation.Email(email) || !AuthValidation.NewPassword(password, confirmation)
            || password!.Length > 1024)
            return new(CreateAdminStatus.Invalid);

        ulong adminRoleId;
        await using (var role = connection.CreateCommand())
        {
            role.Transaction = transaction;
            role.CommandText = "SELECT id FROM roles WHERE name='Admin' AND guard_name='web' FOR UPDATE";
            var value = await role.ExecuteScalarAsync(ct);
            if (value is null) throw new InvalidOperationException("The existing web Admin role is missing.");
            adminRoleId = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
        }

        ulong newId;
        try
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO users(name,email,password,created_at,updated_at) VALUES(@name,@email,@password,UTC_TIMESTAMP(),UTC_TIMESTAMP())";
            insert.Parameters.AddWithValue("@name", name);
            insert.Parameters.AddWithValue("@email", email);
            insert.Parameters.AddWithValue("@password", hasher.Hash(password));
            await insert.ExecuteNonQueryAsync(ct);
            newId = Convert.ToUInt64(insert.LastInsertedId, CultureInfo.InvariantCulture);
        }
        catch (MySqlException error) when (error.Number == 1062)
        {
            return new(CreateAdminStatus.Duplicate);
        }

        await using (var assign = connection.CreateCommand())
        {
            assign.Transaction = transaction;
            assign.CommandText = "INSERT INTO model_has_roles(role_id,model_type,model_id) VALUES(@role,@model,@user)";
            assign.Parameters.AddWithValue("@role", adminRoleId);
            assign.Parameters.AddWithValue("@model", Model);
            assign.Parameters.AddWithValue("@user", newId);
            await assign.ExecuteNonQueryAsync(ct);
        }
        await InvalidateCacheAsync(connection, transaction, ct);
        await transaction.CommitAsync(ct);
        return new(CreateAdminStatus.Created, newId, name, email);
    }

    public async Task<bool> AssignRoleAsync(ulong actor, ulong target, ulong roleId, CancellationToken ct)
    {
        await using var c=await source.OpenConnectionAsync(ct); await using var tx=await c.BeginTransactionAsync(ct);
        await LockSuperAsync(c,tx,ct);
        var actorSnap=await GetInTransactionAsync(c,tx,actor,ct); if(!actorSnap.Allows("users.assign_roles")) return false;
        await using(var exists=c.CreateCommand()){exists.Transaction=tx;exists.CommandText="SELECT COUNT(*) FROM users WHERE id=@id";exists.Parameters.AddWithValue("@id",target);if(Convert.ToInt32(await exists.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture)!=1)return false;}
        var targetSuper=await HasSuperRoleAsync(c,tx,target,ct); var roleName=await RoleNameAsync(c,tx,roleId,ct);
        if(roleName is null || (roleName=="Super Admin" || targetSuper) && !actorSnap.IsSuperAdmin) return false;
        if(targetSuper && roleName!="Super Admin" && await CountSuperAsync(c,tx,ct)<=1) return false;
        await using(var cmd=c.CreateCommand()) { cmd.Transaction=tx; cmd.CommandText="DELETE FROM model_has_roles WHERE model_type=@model AND model_id=@id AND role_id IN (SELECT id FROM roles WHERE guard_name='web')"; cmd.Parameters.AddWithValue("@model",Model); cmd.Parameters.AddWithValue("@id",target); await cmd.ExecuteNonQueryAsync(ct); }
        await using(var cmd=c.CreateCommand()) { cmd.Transaction=tx; cmd.CommandText="INSERT INTO model_has_roles (role_id,model_type,model_id) VALUES (@role,@model,@id)"; cmd.Parameters.AddWithValue("@role",roleId); cmd.Parameters.AddWithValue("@model",Model); cmd.Parameters.AddWithValue("@id",target); await cmd.ExecuteNonQueryAsync(ct); }
        await InvalidateCacheAsync(c,tx,ct); await tx.CommitAsync(ct); return true;
    }

    public async Task<bool> UpdateRolePermissionsAsync(ulong actor, ulong roleId, string[] names, CancellationToken ct)
    {
        if(names.Length>ManagedPermissions.Length || names.Distinct(StringComparer.Ordinal).Count()!=names.Length || names.Any(n=>!ManagedPermissions.Contains(n,StringComparer.Ordinal))) return false;
        await using var c=await source.OpenConnectionAsync(ct); await using var tx=await c.BeginTransactionAsync(ct); await LockSuperAsync(c,tx,ct);
        var snapshot=await GetInTransactionAsync(c,tx,actor,ct); if(!snapshot.Allows("roles.manage_permissions")) return false;
        string? roleName=null; await using(var cmd=c.CreateCommand()) { cmd.Transaction=tx; cmd.CommandText="SELECT name FROM roles WHERE id=@id AND guard_name='web' FOR UPDATE"; cmd.Parameters.AddWithValue("@id",roleId); roleName=(string?)await cmd.ExecuteScalarAsync(ct); }
        if(roleName is null || roleName=="Super Admin") return false;
        var ids=new List<ulong>(); foreach(var name in names) { await using var cmd=c.CreateCommand(); cmd.Transaction=tx; cmd.CommandText="SELECT id FROM permissions WHERE name=@name AND guard_name='web'"; cmd.Parameters.AddWithValue("@name",name); var value=await cmd.ExecuteScalarAsync(ct); if(value is not null) ids.Add(Convert.ToUInt64(value,CultureInfo.InvariantCulture)); }
        if(ids.Count!=names.Length)return false;
        await using(var cmd=c.CreateCommand()) { cmd.Transaction=tx; cmd.CommandText="DELETE rp FROM role_has_permissions rp JOIN permissions p ON p.id=rp.permission_id WHERE rp.role_id=@id AND p.guard_name='web' AND p.name IN (@a,@b,@c,@d,@e,@f,@g,@h)"; cmd.Parameters.AddWithValue("@id",roleId); for(var i=0;i<ManagedPermissions.Length;i++)cmd.Parameters.AddWithValue("@"+"abcdefgh"[i],ManagedPermissions[i]); await cmd.ExecuteNonQueryAsync(ct); }
        foreach(var id in ids) { await using var cmd=c.CreateCommand(); cmd.Transaction=tx; cmd.CommandText="INSERT INTO role_has_permissions(permission_id,role_id) VALUES(@p,@r)"; cmd.Parameters.AddWithValue("@p",id); cmd.Parameters.AddWithValue("@r",roleId); await cmd.ExecuteNonQueryAsync(ct); }
        await InvalidateCacheAsync(c,tx,ct); await tx.CommitAsync(ct); return true;
    }

    private static async Task<RbacSnapshot> GetInTransactionAsync(MySqlConnection c,MySqlTransaction tx,ulong user,CancellationToken ct)
    {
        var roles=new List<string>(); var perms=new HashSet<string>(StringComparer.Ordinal);
        await using(var cmd=c.CreateCommand()){cmd.Transaction=tx;cmd.CommandText="SELECT r.name FROM model_has_roles mr JOIN roles r ON r.id=mr.role_id WHERE mr.model_type=@m AND mr.model_id=@id AND r.guard_name='web'";cmd.Parameters.AddWithValue("@m",Model);cmd.Parameters.AddWithValue("@id",user);await using var rd=await cmd.ExecuteReaderAsync(ct);while(await rd.ReadAsync(ct))roles.Add(rd.GetString(0));}
        await using(var cmd=c.CreateCommand()){cmd.Transaction=tx;cmd.CommandText="SELECT DISTINCT p.name FROM permissions p WHERE p.guard_name='web' AND (EXISTS(SELECT 1 FROM model_has_permissions mp WHERE mp.permission_id=p.id AND mp.model_type=@m AND mp.model_id=@id) OR EXISTS(SELECT 1 FROM model_has_roles mr JOIN roles r ON r.id=mr.role_id AND r.guard_name='web' JOIN role_has_permissions rp ON rp.role_id=mr.role_id WHERE mr.model_type=@m AND mr.model_id=@id AND rp.permission_id=p.id))";cmd.Parameters.AddWithValue("@m",Model);cmd.Parameters.AddWithValue("@id",user);await using var rd=await cmd.ExecuteReaderAsync(ct);while(await rd.ReadAsync(ct))perms.Add(rd.GetString(0));}
        return new(roles.Contains("Super Admin",StringComparer.Ordinal),perms,roles);
    }
    private static async Task LockSuperAsync(MySqlConnection c,MySqlTransaction tx,CancellationToken ct){await using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText="SELECT id FROM roles WHERE name='Super Admin' AND guard_name='web' FOR UPDATE";if(await cmd.ExecuteScalarAsync(ct) is null)throw new InvalidOperationException("Required Super Admin role is missing.");}
    private static async Task<bool> HasSuperRoleAsync(MySqlConnection c,MySqlTransaction tx,ulong id,CancellationToken ct){await using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText="SELECT COUNT(*) FROM model_has_roles mr JOIN roles r ON r.id=mr.role_id WHERE mr.model_type=@m AND mr.model_id=@id AND r.name='Super Admin' AND r.guard_name='web'";cmd.Parameters.AddWithValue("@m",Model);cmd.Parameters.AddWithValue("@id",id);return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture)>0;}
    private static async Task<int> CountSuperAsync(MySqlConnection c,MySqlTransaction tx,CancellationToken ct){await using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText="SELECT COUNT(DISTINCT mr.model_id) FROM model_has_roles mr JOIN roles r ON r.id=mr.role_id WHERE mr.model_type=@m AND r.name='Super Admin' AND r.guard_name='web'";cmd.Parameters.AddWithValue("@m",Model);return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture);}
    private static async Task<string?> RoleNameAsync(MySqlConnection c,MySqlTransaction tx,ulong id,CancellationToken ct){await using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText="SELECT name FROM roles WHERE id=@id AND guard_name='web'";cmd.Parameters.AddWithValue("@id",id);return (string?)await cmd.ExecuteScalarAsync(ct);}
    private static async Task InvalidateCacheAsync(MySqlConnection c,MySqlTransaction tx,CancellationToken ct){await using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText="DELETE FROM cache WHERE `key`='laravel-cache-spatie.permission.cache'";if(await cmd.ExecuteNonQueryAsync(ct)>1)throw new InvalidOperationException("Unexpected permission-cache key multiplicity.");}
}
