using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diwakar.Cms.Api.Authentication;
using static Diwakar.Cms.Auth.Tests.AuthFixture;

namespace Diwakar.Cms.Auth.Tests;

[Collection("Authentication")]
public sealed class DashboardTests(AuthFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Super_admin_receives_global_web_catalog_totals()
    {
        var actor = await fixture.SeedAsync();
        await fixture.SeedAsync("other@example.test");
        await fixture.GrantRoleAsync(actor, "Super Admin");
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/dashboard")).StatusCode);
        await Factory.LoginAsync(client);

        var response = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard");
        Assert.Equal("global", response.GetProperty("scope").GetString());
        Assert.Equal(Convert.ToUInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM users")), response.GetProperty("userCount").GetUInt64());
        Assert.Equal(Convert.ToUInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM roles WHERE guard_name='web'")), response.GetProperty("roleCount").GetUInt64());
        Assert.Equal(Convert.ToUInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM permissions WHERE guard_name='web'")), response.GetProperty("permissionCount").GetUInt64());
        Assert.False(response.TryGetProperty("effectivePermissionCount", out _));
    }

    [Fact]
    public async Task Admin_receives_own_web_grants_and_authorized_user_total()
    {
        var actor = await fixture.SeedAsync();
        await fixture.SeedAsync("other@example.test");
        await fixture.GrantRoleAsync(actor, "Admin");
        await fixture.ExecuteAsync("INSERT IGNORE INTO permissions(name,guard_name) VALUES('dashboard.extra','web'),('dashboard.api-extra','api')");
        await fixture.ExecuteAsync("INSERT IGNORE INTO model_has_permissions(permission_id,model_type,model_id) SELECT id,@model,@id FROM permissions WHERE (name='dashboard.extra' AND guard_name='web') OR (name='dashboard.api-extra' AND guard_name='api') OR (name='users.view' AND guard_name='web')", ("@model", "App\\Models\\User"), ("@id", actor.Id));
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.LoginAsync(client);

        var authz = await client.GetFromJsonAsync<JsonElement>("/api/auth/authorization");
        Assert.Contains(authz.GetProperty("permissions").EnumerateArray(), p => p.GetString() == "dashboard.extra");
        Assert.DoesNotContain(authz.GetProperty("permissions").EnumerateArray(), p => p.GetString() == "dashboard.api-extra");
        var response = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard");
        Assert.Equal("personal", response.GetProperty("scope").GetString());
        Assert.Equal("Admin", response.GetProperty("primaryRole").GetString());
        Assert.Equal(authz.GetProperty("permissions").GetArrayLength(), response.GetProperty("effectivePermissionCount").GetInt32());
        Assert.Equal(Convert.ToUInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM users")), response.GetProperty("userCount").GetUInt64());
        Assert.False(response.TryGetProperty("roleCount", out _));
        Assert.False(response.TryGetProperty("permissionCount", out _));
    }

    [Fact]
    public async Task Admin_without_users_view_receives_no_user_total()
    {
        var actor = await fixture.SeedAsync();
        await fixture.GrantRoleAsync(actor, "Admin");
        var hadGrant = Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM role_has_permissions rp JOIN roles r ON r.id=rp.role_id JOIN permissions p ON p.id=rp.permission_id WHERE r.name='Admin' AND r.guard_name='web' AND p.name='users.view' AND p.guard_name='web'")) > 0;
        await fixture.ExecuteAsync("DELETE rp FROM role_has_permissions rp JOIN roles r ON r.id=rp.role_id JOIN permissions p ON p.id=rp.permission_id WHERE r.name='Admin' AND r.guard_name='web' AND p.name='users.view' AND p.guard_name='web'");
        try
        {
            using var factory = fixture.CreateFactory();
            using var client = factory.Browser();
            await Factory.LoginAsync(client);
            var response = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard");
            Assert.Equal("personal", response.GetProperty("scope").GetString());
            Assert.Equal("Admin", response.GetProperty("primaryRole").GetString());
            Assert.False(response.TryGetProperty("userCount", out _));
            Assert.False(response.TryGetProperty("roleCount", out _));
            Assert.False(response.TryGetProperty("permissionCount", out _));
        }
        finally
        {
            if (hadGrant) await fixture.ExecuteAsync("INSERT IGNORE INTO role_has_permissions(permission_id,role_id) SELECT p.id,r.id FROM permissions p JOIN roles r ON r.name='Admin' AND r.guard_name='web' WHERE p.name='users.view' AND p.guard_name='web'");
        }
    }
}
