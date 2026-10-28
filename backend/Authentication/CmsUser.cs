using System.Security.Cryptography;
using System.Text;

namespace Diwakar.Cms.Api.Authentication;

public sealed record CmsUser(ulong Id, string Name, string Email, string PasswordHash, bool EmailVerified)
{
    // A password change in either application invalidates our cookies and tokens.
    public string SecurityStamp => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(PasswordHash)));
}

public sealed record LoginInput(string? Email, string? Password, bool Remember = false);
public sealed record EmailInput(string? Email);
public sealed record PasswordInput(string? Password);
public sealed record ResetInput(string? Email, string? Token, string? Password, string? PasswordConfirmation);
public sealed record ChangeInput(string? CurrentPassword, string? Password, string? PasswordConfirmation);
public sealed record VerificationInput(string? Token);

public static class AuthValidation
{
    public static bool Email(string? value) => value is { Length: > 0 and <= 255 }
        && System.Net.Mail.MailAddress.TryCreate(value, out var email) && email.Address == value;

    public static bool NewPassword(string? value, string? confirmation) =>
        value is not null && value.EnumerateRunes().Count() >= 8
        && !value.Contains('\0') && value == confirmation;
}
