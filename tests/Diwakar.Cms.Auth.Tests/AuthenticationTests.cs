using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Diwakar.Cms.Api.Authentication;
using static Diwakar.Cms.Auth.Tests.AuthFixture;

namespace Diwakar.Cms.Auth.Tests;

[Collection("Authentication")]
public sealed class AuthenticationTests(AuthFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Guests_cannot_access_identity_or_admin_and_registration_remains_absent()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        foreach (var path in new[] { "/api/auth/me", "/api/admin/dashboard" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        foreach (var path in new[] { "/register", "/api/auth/register" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(path, new { email = "test@example.test" })).StatusCode);
        }
    }

    [Fact]
    public async Task Existing_PHP_user_can_sign_in_without_mutating_password_and_can_sign_out()
    {
        var hash = await PasswordCompatibilityTests.PhpAsync(new { action = "hash", password = "existing-password" });
        var user = await fixture.SeedAsync(hash: hash);
        await fixture.GrantRoleAsync(user,"Super Admin");
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/dashboard")).StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(user.Email, me.GetProperty("email").GetString());
        Assert.False(me.GetProperty("emailVerified").GetBoolean());
        Assert.False(me.TryGetProperty("passwordHash", out _));
        using (var scope = factory.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<SharedUserStore>().FindByEmailAsync(user.Email);
            Assert.Equal(hash, stored!.PasswordHash);
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("existing@example.test", "wrong")]
    [InlineData("missing@example.test", "existing-password")]
    [InlineData("' OR 1=1 --", "existing-password")]
    public async Task Invalid_credentials_do_not_authenticate(string email, string password)
    {
        await fixture.SeedAsync();
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.CsrfAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Missing_or_tampered_CSRF_and_pre_login_tokens_are_rejected()
    {
        await fixture.SeedAsync();
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        var input = new { email = "existing@example.test", password = "existing-password" };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", input)).StatusCode);
        await Factory.CsrfAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", input)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
        await Factory.CsrfAsync(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "tampered");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
    }

    [Fact]
    public async Task Five_failed_logins_are_throttled_and_a_new_window_allows_login()
    {
        await fixture.SeedAsync();
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.CsrfAsync(client);
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "existing@example.test", password = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", new { email = "existing@example.test", password = "existing-password" })).StatusCode);
        factory.Clock.Now = factory.Clock.Now.AddMinutes(2);
        await Factory.LoginAsync(client);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cookies_are_HttpOnly_SameSite_and_persistent_only_when_requested(bool remember)
    {
        await fixture.SeedAsync();
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.CsrfAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "existing@example.test", password = "existing-password", remember });
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("Diwakar.Cms.Auth=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(remember, cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.True(response.Headers.CacheControl!.NoStore);
        var cookieValue = cookie.Split(';')[0]["Diwakar.Cms.Auth=".Length..];
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(AuthService.Scheme);
        var ticket = options.TicketDataFormat.Unprotect(Uri.UnescapeDataString(cookieValue));
        Assert.NotNull(ticket);
        Assert.Equal(remember ? TimeSpan.FromDays(400) : TimeSpan.FromMinutes(120), ticket.Properties.ExpiresUtc - ticket.Properties.IssuedUtc);
    }

    [Fact]
    public async Task Shared_password_changes_and_user_deletion_invalidate_existing_cookies()
    {
        var user = await fixture.SeedAsync();
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.LoginAsync(client);
        var phpHash = await PasswordCompatibilityTests.PhpAsync(new { action = "hash", password = "changed-in-laravel" });
        await fixture.ExecuteAsync("UPDATE users SET password = @hash WHERE id = @id", ("@hash", phpHash), ("@id", user.Id));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        await Factory.LoginAsync(client, password: "changed-in-laravel");
        await fixture.ExecuteAsync("DELETE FROM users WHERE id = @id", ("@id", user.Id));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Confirmation_expires_after_three_hours_and_password_change_requires_current_password()
    {
        await fixture.SeedAsync();
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.LoginAsync(client, remember: true);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/confirm-password", new { password = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/confirm-password", new { password = "existing-password" })).StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.True(me.GetProperty("passwordConfirmed").GetBoolean());
        factory.Clock.Now = factory.Clock.Now.AddHours(3).AddSeconds(1);
        me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.False(me.GetProperty("passwordConfirmed").GetBoolean());
        var bad = await client.PutAsJsonAsync("/api/auth/password", new { currentPassword = "wrong", password = "replacement", passwordConfirmation = "replacement" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var good = await client.PutAsJsonAsync("/api/auth/password", new { currentPassword = "existing-password", password = "replacement", passwordConfirmation = "replacement" });
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        await Factory.LoginAsync(client, password: "replacement");
    }

    private static async Task<string> TokenAsync(Factory factory, CmsUser user, string purpose)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<CmsUser>>().GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, purpose + user.Email);
    }

    [Fact]
    public async Task Recovery_is_generic_throttled_and_keeps_Laravel_reset_and_session_records()
    {
        var user = await fixture.SeedAsync();
        await fixture.ExecuteAsync("INSERT INTO password_reset_tokens VALUES (@email, 'laravel-token-placeholder', UTC_TIMESTAMP())", ("@email", user.Email));
        await fixture.ExecuteAsync("INSERT INTO sessions VALUES ('laravel-session', 'unchanged-payload')");
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.CsrfAsync(client);
        Directory.CreateDirectory(fixture.MailDirectory);
        var before = Directory.GetFiles(fixture.MailDirectory).ToHashSet();
        var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = user.Email });
        var missing = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "missing@example.test" });
        Assert.Equal(await known.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = user.Email });
        Assert.Equal(before.Count + 1, Directory.GetFiles(fixture.MailDirectory).Length);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/.local/mail/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/appsettings.Local.json")).StatusCode);
        var deliveredFile = Assert.Single(Directory.GetFiles(fixture.MailDirectory), file => !before.Contains(file));
        var link = new Uri(Assert.Single(await File.ReadAllLinesAsync(deliveredFile), line => line.StartsWith("http://", StringComparison.Ordinal)));
        Assert.Equal("/reset-password", link.AbsolutePath);
        var query = QueryHelpers.ParseQuery(link.Query);
        Assert.Equal(user.Email, query["email"].ToString());
        var token = query["token"].ToString();
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new { email = user.Email, token, password = "reset-password", passwordConfirmation = "reset-password" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT (SELECT COUNT(*) FROM password_reset_tokens WHERE token = 'laravel-token-placeholder') + (SELECT COUNT(*) FROM sessions WHERE payload = 'unchanged-payload')";
        Assert.Equal(2L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Reset_tokens_reject_tampering_wrong_accounts_mismatch_and_reuse_and_PHP_accepts_new_hash()
    {
        var user = await fixture.SeedAsync();
        var other = await fixture.SeedAsync("other@example.test");
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.CsrfAsync(client);
        var token = await TokenAsync(factory, user, "ResetPassword:");
        async Task<HttpResponseMessage> Reset(string email, string suppliedToken, string confirmation = "reset-password") =>
            await client.PostAsJsonAsync("/api/auth/reset-password", new { email, token = suppliedToken, password = "reset-password", passwordConfirmation = confirmation });
        Assert.Equal(HttpStatusCode.BadRequest, (await Reset(user.Email, token + "tamper")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Reset(other.Email, token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Reset(user.Email, token, "mismatch")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Reset(user.Email, token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Reset(user.Email, token)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var updated = await scope.ServiceProvider.GetRequiredService<SharedUserStore>().FindByEmailAsync(user.Email);
        Assert.Equal("yes", await PasswordCompatibilityTests.PhpAsync(new { action = "verify", password = "reset-password", hash = updated!.PasswordHash }));
    }

    [Fact]
    public async Task Expired_tokens_and_changed_email_bindings_are_rejected()
    {
        var user = await fixture.SeedAsync();
        using var factory = fixture.CreateFactory();
        factory.TokenLife = TimeSpan.FromSeconds(-1);
        using var client = factory.Browser();
        await Factory.CsrfAsync(client);
        var token = await TokenAsync(factory, user, "ResetPassword:");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset-password", new { email = user.Email, token, password = "reset-password", passwordConfirmation = "reset-password" })).StatusCode);
        using var freshFactory = fixture.CreateFactory();
        using var freshClient = freshFactory.Browser();
        await Factory.CsrfAsync(freshClient);
        token = await TokenAsync(freshFactory, user, "ResetPassword:");
        await fixture.ExecuteAsync("UPDATE users SET email = 'new@example.test' WHERE id = @id", ("@id", user.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await freshClient.PostAsJsonAsync("/api/auth/reset-password", new { email = "new@example.test", token, password = "reset-password", passwordConfirmation = "reset-password" })).StatusCode);
    }

    [Fact]
    public async Task Concurrent_reset_consumption_can_succeed_only_once()
    {
        var user = await fixture.SeedAsync();
        using var factory = fixture.CreateFactory();
        var token = await TokenAsync(factory, user, "ResetPassword:");
        async Task<bool> Consume()
        {
            using var scope = factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AuthService>().ResetAsync(user, token, "reset-password", CancellationToken.None);
        }
        var results = await Task.WhenAll(Consume(), Consume());
        Assert.Single(results, value => value);
    }

    [Fact]
    public async Task Verification_is_optional_account_bound_and_idempotent()
    {
        var user = await fixture.SeedAsync();
        var other = await fixture.SeedAsync("other@example.test");
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.LoginAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/dashboard")).StatusCode);
        var otherToken = await TokenAsync(factory, other, "VerifyEmail:");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/verify-email", new { token = otherToken })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/verify-email", new { token = "tampered" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/verification-notification", new { })).StatusCode);
        var token = await TokenAsync(factory, user, "VerifyEmail:");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/verify-email", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/verify-email", new { token })).StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.True(me.GetProperty("emailVerified").GetBoolean());
    }

    [Fact]
    public async Task Ordinary_authentication_expires_after_120_minutes()
    {
        await fixture.SeedAsync();
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.LoginAsync(client);
        factory.Clock.Now = factory.Clock.Now.AddMinutes(121);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Every_authenticated_write_requires_CSRF()
    {
        await fixture.SeedAsync();
        using var factory = fixture.CreateFactory();
        using var client = factory.Browser();
        await Factory.LoginAsync(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        foreach (var path in new[] { "/api/auth/logout", "/api/auth/confirm-password", "/api/auth/verification-notification", "/api/auth/verify-email" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { password = "existing-password", token = "placeholder" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/auth/password", new { currentPassword = "existing-password", password = "replacement", passwordConfirmation = "replacement" })).StatusCode);
    }

    [Fact]
    public async Task Verification_expiry_and_resend_limits_are_enforced()
    {
        var user = await fixture.SeedAsync();
        using var factory = fixture.CreateFactory();
        factory.TokenLife = TimeSpan.FromSeconds(-1);
        using var client = factory.Browser();
        await Factory.LoginAsync(client);
        var token = await TokenAsync(factory, user, "VerifyEmail:");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/verify-email", new { token })).StatusCode);
        for (var attempt = 0; attempt < 6; attempt++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/verification-notification", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/verification-notification", new { })).StatusCode);
    }

    [Fact]
    public async Task Production_cookies_are_secure_and_unconfigured_email_fails_closed()
    {
        await fixture.SeedAsync();
        using var factory = fixture.CreateFactory();
        factory.EnvironmentName = "Production";
        using var client = factory.Browser();
        await Factory.CsrfAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "existing@example.test", password = "existing-password" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("Diwakar.Cms.Auth=", StringComparison.Ordinal));
        Assert.Contains("; secure", cookie, StringComparison.OrdinalIgnoreCase);
        await Factory.CsrfAsync(client);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "existing@example.test" })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/auth/verification-notification", new { })).StatusCode);
    }
}
