using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diwakar.Cms.Api.Authentication;
using MySqlConnector;
using Microsoft.Extensions.DependencyInjection;
using static Diwakar.Cms.Auth.Tests.AuthFixture;

namespace Diwakar.Cms.Auth.Tests;

[Collection("Authentication")]
public sealed class RbacTests(AuthFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync()=>fixture.ResetAsync(); public Task DisposeAsync()=>Task.CompletedTask;
    private async Task<ulong> RoleIdAsync(string name){await using var c=new MySqlConnection(fixture.ConnectionString);await c.OpenAsync();await using var q=c.CreateCommand();q.CommandText="SELECT id FROM roles WHERE name=@n AND guard_name='web'";q.Parameters.AddWithValue("@n",name);return Convert.ToUInt64(await q.ExecuteScalarAsync());}
    [Fact]
    public async Task Guest_is_denied_and_default_admin_can_read_but_not_assign()
    {
        var actor=await fixture.SeedAsync();var target=await fixture.SeedAsync("target@example.test");await fixture.GrantRoleAsync(actor,"Admin");using var f=fixture.CreateFactory();using var client=f.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/admin/users")).StatusCode);await Factory.LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/admin/users")).StatusCode);
        var response=await client.GetFromJsonAsync<JsonElement>("/api/auth/authorization");Assert.True(response.GetProperty("canAccessAdmin").GetBoolean());Assert.False(response.GetProperty("canAssignRoles").GetBoolean());
        var patch=await client.PatchAsJsonAsync($"/api/admin/users/{target.Id}/role",new {roleId=(await RoleIdAsync("Admin")).ToString()});Assert.Equal(HttpStatusCode.Forbidden,patch.StatusCode);
        using var scope=f.Services.CreateScope();var current=await scope.ServiceProvider.GetRequiredService<Diwakar.Cms.Api.Authorization.RbacStore>().GetAsync(actor.Id,CancellationToken.None);Assert.True(current.Allows("users.view"));
    }
    [Fact]
    public async Task Last_super_admin_cannot_be_demoted_after_the_second_super_admin_is_assigned()
    {
        var actor=await fixture.SeedAsync();var target=await fixture.SeedAsync("target@example.test");await fixture.GrantRoleAsync(actor,"Super Admin");using var f=fixture.CreateFactory();using var client=f.Browser();await Factory.LoginAsync(client);
        var admin=(await RoleIdAsync("Admin")).ToString();var super=(await RoleIdAsync("Super Admin")).ToString();
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PatchAsJsonAsync($"/api/admin/users/{actor.Id}/role",new {roleId=admin})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PatchAsJsonAsync($"/api/admin/users/{target.Id}/role",new {roleId=super})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PatchAsJsonAsync($"/api/admin/users/{target.Id}/role",new {roleId=admin})).StatusCode);
    }
    [Fact]
    public async Task Super_admin_role_permissions_are_protected_and_updates_clear_only_exact_cache_key()
    {
        var actor=await fixture.SeedAsync();await fixture.GrantRoleAsync(actor,"Super Admin");using var f=fixture.CreateFactory();using var client=f.Browser();await Factory.LoginAsync(client);
        var roles=await client.GetFromJsonAsync<JsonElement>("/api/admin/roles");var admin=roles.GetProperty("roles").EnumerateArray().Single(r=>r.GetProperty("name").GetString()=="Admin").GetProperty("id").GetString();var super=roles.GetProperty("roles").EnumerateArray().Single(r=>r.GetProperty("name").GetString()=="Super Admin").GetProperty("id").GetString();
        await fixture.ExecuteAsync("INSERT INTO cache(`key`,value,expiration) VALUES('laravel-cache-spatie.permission.cache','cached',999999999),('unrelated-key','keep',999999999)");
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PatchAsJsonAsync($"/api/admin/roles/{super}/permissions",new {permissions=Array.Empty<string>()})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PatchAsJsonAsync($"/api/admin/roles/{admin}/permissions",new {permissions=new[]{"users.view"}})).StatusCode);
        await using var c=new MySqlConnection(fixture.ConnectionString);await c.OpenAsync();await using var q=c.CreateCommand();q.CommandText="SELECT COUNT(*) FROM cache WHERE `key`='laravel-cache-spatie.permission.cache'";Assert.Equal(0L,Convert.ToInt64(await q.ExecuteScalarAsync()));
        await using var q2=c.CreateCommand();q2.CommandText="SELECT value FROM cache WHERE `key`='unrelated-key'";Assert.Equal("keep",await q2.ExecuteScalarAsync());
    }
    [Fact]
    public async Task Wrong_model_and_guard_role_lookalikes_do_not_grant_web_access()
    {
        var user=await fixture.SeedAsync();var web=await RoleIdAsync("Super Admin");await fixture.ExecuteAsync("INSERT IGNORE INTO roles(name,guard_name) VALUES('Super Admin','api')");var api=await fixture.ScalarAsync("SELECT id FROM roles WHERE name='Super Admin' AND guard_name='api'");
        await fixture.ExecuteAsync("INSERT INTO model_has_roles(role_id,model_type,model_id) VALUES(@role,'App\\Models\\User',@id),(@web,'Other\\Models\\User',@id)",("@role",api ?? throw new InvalidOperationException("Missing API role id.")),("@web",web),("@id",user.Id));
        using var f=fixture.CreateFactory();using var client=f.Browser();await Factory.LoginAsync(client);Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/admin/dashboard")).StatusCode);
        var snapshot=await client.GetFromJsonAsync<JsonElement>("/api/auth/authorization");Assert.False(snapshot.GetProperty("isSuperAdmin").GetBoolean());Assert.False(snapshot.GetProperty("canAccessAdmin").GetBoolean());
    }
    [Fact]
    public async Task Concurrent_dotnet_demotions_leave_one_super_admin()
    {
        var first=await fixture.SeedAsync();var second=await fixture.SeedAsync("second@example.test");await fixture.GrantRoleAsync(first,"Super Admin");await fixture.GrantRoleAsync(second,"Super Admin");using var f=fixture.CreateFactory();using var a=f.Browser();using var b=f.Browser();await Factory.LoginAsync(a);await Factory.LoginAsync(b);var admin=(await RoleIdAsync("Admin")).ToString();
        var responses=await Task.WhenAll(a.PatchAsJsonAsync($"/api/admin/users/{first.Id}/role",new {roleId=admin}),b.PatchAsJsonAsync($"/api/admin/users/{second.Id}/role",new {roleId=admin}));Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Forbidden);
        var count=await fixture.ScalarAsync("SELECT COUNT(DISTINCT mr.model_id) FROM model_has_roles mr JOIN roles r ON r.id=mr.role_id WHERE mr.model_type=@model AND r.name='Super Admin' AND r.guard_name='web'",("@model","App\\Models\\User"));Assert.Equal(1L,Convert.ToInt64(count));
    }
    [Fact]
    public async Task Provisioning_preserves_existing_password_and_creates_only_explicit_valid_users()
    {
        var existing=await fixture.SeedAsync();using var f=fixture.CreateFactory();using(var scope=f.Services.CreateScope()){var p=scope.ServiceProvider.GetRequiredService<Diwakar.Cms.Api.Authorization.RbacProvisioner>();var id=await p.ProvisionAsync(existing.Email,null,null,null,CancellationToken.None);Assert.Equal(existing.Id,id);}
        var after=await fixture.ScalarAsync("SELECT password FROM users WHERE id=@id",("@id",existing.Id));Assert.Equal(existing.PasswordHash,after);
        using(var scope=f.Services.CreateScope()){var p=scope.ServiceProvider.GetRequiredService<Diwakar.Cms.Api.Authorization.RbacProvisioner>();await Assert.ThrowsAsync<ArgumentException>(()=>p.ProvisionAsync("new@example.test","New User","short","short",CancellationToken.None));var id=await p.ProvisionAsync("new@example.test","New User","secure-password","secure-password",CancellationToken.None);Assert.True(id>0);}
        Assert.Equal(1L,Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM users WHERE email='new@example.test'")));
    }    [Fact]
    public async Task Dashboard_returns_live_web_catalog_counts_and_user_count()
    {
        var actor=await fixture.SeedAsync();await fixture.SeedAsync("other@example.test");await fixture.GrantRoleAsync(actor,"Super Admin");using var f=fixture.CreateFactory();using var client=f.Browser();await Factory.LoginAsync(client);
        var response=await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard");
        Assert.Equal(Convert.ToUInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM users")),response.GetProperty("userCount").GetUInt64());
        Assert.Equal(Convert.ToUInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM roles WHERE guard_name='web'")),response.GetProperty("roleCount").GetUInt64());
        Assert.Equal(Convert.ToUInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM permissions WHERE guard_name='web'")),response.GetProperty("permissionCount").GetUInt64());
    }

    [Fact]
    public async Task Create_admin_requires_super_admin_and_csrf()
    {
        var actor = await fixture.SeedAsync();
        await fixture.GrantRoleAsync(actor, "Admin");
        using var factory = fixture.CreateFactory();
        using var guest = factory.Browser();
        await Factory.CsrfAsync(guest);
        var input = new { name = "New Admin", email = "new@example.test", password = "secure-password", passwordConfirmation = "secure-password" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/admin/users", input)).StatusCode);

        using var client = factory.Browser();
        await Factory.LoginAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/users", input)).StatusCode);
        await fixture.ExecuteAsync("INSERT IGNORE INTO permissions(name,guard_name) VALUES('super-admin.manage','web')");
        await fixture.ExecuteAsync("INSERT INTO model_has_permissions(permission_id,model_type,model_id) SELECT id,@model,@user FROM permissions WHERE name='super-admin.manage' AND guard_name='web'",
            ("@model", "App\\Models\\User"), ("@user", actor.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/users", input)).StatusCode);

        await fixture.GrantRoleAsync(actor, "Super Admin");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/users", input)).StatusCode);
        Assert.Equal(0L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM users WHERE email='new@example.test'")));
    }

    [Fact]
    public async Task Super_admin_creates_admin_with_existing_role_and_rejects_duplicate_or_invalid_input()
    {
        var actor = await fixture.SeedAsync();
        await fixture.GrantRoleAsync(actor, "Super Admin");
        await fixture.ExecuteAsync("INSERT INTO cache(`key`,value,expiration) VALUES('laravel-cache-spatie.permission.cache','cached',999999999),('unrelated-key','keep',999999999)");
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.LoginAsync(client);
        var input = new { name = " New Admin ", email = "New@Example.Test", password = "secure-password", passwordConfirmation = "secure-password" };
        var created = await client.PostAsJsonAsync("/api/admin/users", input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var user = body.GetProperty("user");
        Assert.Equal("New Admin", user.GetProperty("name").GetString());
        Assert.Equal("new@example.test", user.GetProperty("email").GetString());
        Assert.Equal("Admin", user.GetProperty("role").GetString());
        Assert.False(user.TryGetProperty("password", out _));
        var id = ulong.Parse(user.GetProperty("id").GetString()!);
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM model_has_roles mr JOIN roles r ON r.id=mr.role_id WHERE mr.model_type=@model AND mr.model_id=@id AND r.name='Admin' AND r.guard_name='web'", ("@model", "App\\Models\\User"), ("@id", id))));
        Assert.Equal(0L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM cache WHERE `key`='laravel-cache-spatie.permission.cache'")));
        Assert.Equal("keep", await fixture.ScalarAsync("SELECT value FROM cache WHERE `key`='unrelated-key'"));

        using var newClient = factory.Browser();
        await Factory.LoginAsync(newClient, "new@example.test", "secure-password");
        var authz = await newClient.GetFromJsonAsync<JsonElement>("/api/auth/authorization");
        Assert.Contains(authz.GetProperty("roles").EnumerateArray(), role => role.GetString() == "Admin");
        Assert.False(authz.GetProperty("isSuperAdmin").GetBoolean());

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/admin/users", input)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/users", new { name = "Invalid", email = "invalid@example.test", password = "short", passwordConfirmation = "short" })).StatusCode);
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM users WHERE email='new@example.test'")));
    }

    [Fact]
    public async Task Missing_admin_role_rolls_back_new_account()
    {
        var actor = await fixture.SeedAsync();
        await fixture.GrantRoleAsync(actor, "Super Admin");
        await fixture.ExecuteAsync("UPDATE roles SET name='Admin_unavailable' WHERE name='Admin' AND guard_name='web'");
        try
        {
            using var factory = fixture.CreateFactory();
            using var client = factory.Browser();
            await Factory.LoginAsync(client);
            var response = await client.PostAsJsonAsync("/api/admin/users", new { name = "New Admin", email = "new@example.test", password = "secure-password", passwordConfirmation = "secure-password" });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(0L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM users WHERE email='new@example.test'")));
        }
        finally
        {
            await fixture.ExecuteAsync("UPDATE roles SET name='Admin' WHERE name='Admin_unavailable' AND guard_name='web'");
        }
    }
}
