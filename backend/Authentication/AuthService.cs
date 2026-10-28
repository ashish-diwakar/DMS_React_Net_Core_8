using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Diwakar.Cms.Api.Authentication;

public sealed class AuthService(SharedUserStore users, PasswordHasher passwords, UserManager<CmsUser> manager,
    DevelopmentMail mail, IConfiguration configuration, TimeProvider clock)
{
    public const string Scheme = "CmsCookie";
    public const string StampClaim = "cms:password-stamp";
    public const string ConfirmedClaim = "cms:confirmed-at";

    public Task<CmsUser?> FindAsync(string email, CancellationToken ct) => users.FindByEmailAsync(email, ct);
    public bool Verify(string password, CmsUser? user) => passwords.Verify(password, user?.PasswordHash);

    public async Task SignInAsync(HttpContext context, CmsUser user, bool remember, string? confirmed = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.Name), new(StampClaim, user.SecurityStamp) };
        if (confirmed is not null) claims.Add(new(ConfirmedClaim, confirmed));
        var now = clock.GetUtcNow();
        var properties = new AuthenticationProperties { IsPersistent = remember, AllowRefresh = false, IssuedUtc = now,
            ExpiresUtc = now.Add(remember ? TimeSpan.FromDays(400) : TimeSpan.FromMinutes(120)) };
        await context.SignInAsync(Scheme, new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme)), properties);
    }

    public async Task SendResetAsync(CmsUser user, CancellationToken ct)
    {
        var token = await manager.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, "ResetPassword:" + user.Email);
        var link = Origin() + "/reset-password?email=" + Uri.EscapeDataString(user.Email) + "&token=" + Uri.EscapeDataString(token);
        await mail.SendAsync(user.Email, "Reset your CMS password", link, ct);
    }

    public async Task<bool> ResetAsync(CmsUser user, string token, string password, CancellationToken ct)
    {
        if (!await manager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, "ResetPassword:" + user.Email, token)) return false;
        return await users.ChangePasswordAsync(user, passwords.Hash(password), ct);
    }

    public Task<bool> ChangeAsync(CmsUser user, string password, CancellationToken ct) => users.ChangePasswordAsync(user, passwords.Hash(password), ct);

    public async Task SendVerificationAsync(CmsUser user, CancellationToken ct)
    {
        var token = await manager.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, "VerifyEmail:" + user.Email);
        await mail.SendAsync(user.Email, "Verify your CMS email", Origin() + "/verify-email?token=" + Uri.EscapeDataString(token), ct);
    }

    public async Task<bool> VerifyEmailAsync(CmsUser user, string token, CancellationToken ct) =>
        await manager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, "VerifyEmail:" + user.Email, token)
        && (user.EmailVerified || await users.VerifyEmailAsync(user, ct));

    private string Origin()
    {
        var origin = configuration["Authentication:FrontendOrigin"] ?? "";
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https")
            || uri.UserInfo.Length > 0 || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new InvalidOperationException("A valid frontend origin must be configured.");
        return origin.TrimEnd('/');
    }

    public static bool StampMatches(string? stamp, CmsUser user) => stamp is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stamp), Encoding.UTF8.GetBytes(user.SecurityStamp));
}
