using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diwakar.Cms.Api.Authentication;
using static Diwakar.Cms.Auth.Tests.AuthFixture;

namespace Diwakar.Cms.Auth.Tests;

[Collection("Authentication")]
public sealed class WebsiteSettingsTests(AuthFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static object Valid(string siteName = "Example CMS") => new
    {
        siteName, tagline = "A useful site", publicEmail = "hello@example.test",
        primaryPhone = "+1 555 0100", secondaryPhone = (string?)null, address = "Main Street",
        facebookUrl = "https://facebook.com/example", instagramUrl = (string?)null,
        linkedinUrl = (string?)null, youtubeUrl = (string?)null, xTwitterUrl = (string?)null,
        footerText = "Copyright Example"
    };

    [Fact]
    public async Task Guest_and_default_admin_cannot_read_or_write_settings()
    {
        var actor = await fixture.SeedAsync();
        await fixture.GrantRoleAsync(actor, "Admin");
        using var factory = fixture.CreateFactory();
        using var guest = factory.Browser();
        await Factory.CsrfAsync(guest);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/admin/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PatchAsJsonAsync("/api/admin/settings", Valid())).StatusCode);

        using var client = factory.Browser();
        await Factory.LoginAsync(client);
        var authz = await client.GetFromJsonAsync<JsonElement>("/api/auth/authorization");
        Assert.False(authz.GetProperty("canManageSettings").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync("/api/admin/settings", Valid())).StatusCode);

        var withoutAdminAccess = await fixture.SeedAsync("limited@example.test");
        await fixture.ExecuteAsync("""
            INSERT INTO model_has_permissions(permission_id,model_type,model_id)
            SELECT id,@model,@id FROM permissions WHERE name='settings.manage' AND guard_name='web'
            """, ("@model", @"App\Models\User"), ("@id", withoutAdminAccess.Id));
        using var limited = factory.Browser();
        await Factory.LoginAsync(limited, "limited@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await limited.GetAsync("/api/admin/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await limited.PatchAsJsonAsync("/api/admin/settings", Valid())).StatusCode);
        Assert.Equal(0L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM website_settings")));
    }

    [Fact]
    public async Task Super_admin_reads_missing_row_and_creates_then_updates_singleton()
    {
        var actor = await fixture.SeedAsync();
        await fixture.GrantRoleAsync(actor, "Super Admin");
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.LoginAsync(client);

        var initial = await client.GetFromJsonAsync<JsonElement>("/api/admin/settings");
        Assert.Equal("", initial.GetProperty("siteName").GetString());
        Assert.Equal(JsonValueKind.Null, initial.GetProperty("publicEmail").ValueKind);
        Assert.Equal(0L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM website_settings")));

        var first = await client.PatchAsJsonAsync("/api/admin/settings", Valid());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("Example CMS", (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("settings").GetProperty("siteName").GetString());
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM website_settings WHERE id=1")));
        Assert.Equal("hello@example.test", await fixture.ScalarAsync("SELECT public_email FROM website_settings WHERE id=1"));

        var second = await client.PatchAsJsonAsync("/api/admin/settings", new
        {
            siteName = " Updated CMS ", tagline = "", publicEmail = "", primaryPhone = "",
            secondaryPhone = "", address = "", facebookUrl = "", instagramUrl = "",
            linkedinUrl = "", youtubeUrl = "", xTwitterUrl = "", footerText = "<script>alert(1)</script>",
            appKey = "do-not-save", databasePassword = "do-not-save", logoPath = "/not-approved.png"
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM website_settings")));
        var saved = await client.GetFromJsonAsync<JsonElement>("/api/admin/settings");
        Assert.Equal("Updated CMS", saved.GetProperty("siteName").GetString());
        Assert.Equal(JsonValueKind.Null, saved.GetProperty("publicEmail").ValueKind);
        Assert.Equal("<script>alert(1)</script>", saved.GetProperty("footerText").GetString());
        Assert.False(saved.TryGetProperty("appKey", out _));
        Assert.False(saved.TryGetProperty("databasePassword", out _));
        Assert.False(saved.TryGetProperty("logoPath", out _));
    }

    [Fact]
    public async Task Admin_with_explicit_web_permission_can_manage_settings()
    {
        var actor = await fixture.SeedAsync();
        await fixture.GrantRoleAsync(actor, "Admin");
        await fixture.ExecuteAsync("""
            INSERT INTO model_has_permissions(permission_id,model_type,model_id)
            SELECT id,@model,@id FROM permissions WHERE name='settings.manage' AND guard_name='web'
            """, ("@model", @"App\Models\User"), ("@id", actor.Id));
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.LoginAsync(client);
        var authz = await client.GetFromJsonAsync<JsonElement>("/api/auth/authorization");
        Assert.True(authz.GetProperty("canManageSettings").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync("/api/admin/settings", Valid("Admin Site"))).StatusCode);
        Assert.Equal("Admin Site", await fixture.ScalarAsync("SELECT site_name FROM website_settings WHERE id=1"));
    }

    [Fact]
    public async Task Validation_rejects_invalid_fields_without_changing_stored_values()
    {
        var actor = await fixture.SeedAsync();
        await fixture.GrantRoleAsync(actor, "Super Admin");
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync("/api/admin/settings", Valid())).StatusCode);

        var invalid = await client.PatchAsJsonAsync("/api/admin/settings", new
        {
            siteName = new string('X', 121), publicEmail = "not-an-email",
            facebookUrl = "javascript:alert(1)", youtubeUrl = "https://user:pass@example.test/",
            footerText = new string('F', 501)
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var body = await invalid.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var name in new[] { "siteName", "publicEmail", "facebookUrl", "youtubeUrl", "footerText" })
            Assert.True(body.GetProperty("errors").TryGetProperty(name, out _), name);
        Assert.Equal("Example CMS", await fixture.ScalarAsync("SELECT site_name FROM website_settings WHERE id=1"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync("/api/admin/settings", new { siteName = "  " })).StatusCode);
    }

    [Fact]
    public async Task Update_requires_csrf_and_concurrent_first_saves_keep_one_row()
    {
        var actor = await fixture.SeedAsync();
        await fixture.GrantRoleAsync(actor, "Super Admin");
        using var factory = fixture.CreateFactory();
        using var a = factory.Browser();
        using var b = factory.Browser();
        await Factory.LoginAsync(a);
        await Factory.LoginAsync(b);
        a.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PatchAsJsonAsync("/api/admin/settings", Valid())).StatusCode);
        Assert.Equal(0L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM website_settings")));

        await Factory.CsrfAsync(a);
        var responses = await Task.WhenAll(
            a.PatchAsJsonAsync("/api/admin/settings", Valid("First Site")),
            b.PatchAsJsonAsync("/api/admin/settings", Valid("Second Site")));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM website_settings")));
        var value = await fixture.ScalarAsync("SELECT site_name FROM website_settings WHERE id=1");
        Assert.Contains(value, new object[] { "First Site", "Second Site" });
    }
}
