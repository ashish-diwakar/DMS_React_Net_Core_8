namespace Diwakar.Cms.Api.Authentication;

public sealed class PasswordHasher
{
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("invalid-account-timing-placeholder", BCrypt.Net.BCrypt.GenerateSalt(12, 'y'));

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12, 'y'));

    public bool Verify(string? password, string? hash)
    {
        if (password is null || password.Contains('\0')) return false;
        var candidate = hash ?? DummyHash;
        if (!(candidate.StartsWith("$2y$", StringComparison.Ordinal) || candidate.StartsWith("$2b$", StringComparison.Ordinal)
            || candidate.StartsWith("$2a$", StringComparison.Ordinal))) return false;
        try { return BCrypt.Net.BCrypt.Verify(password, candidate) && hash is not null; }
        catch (ArgumentException) { return false; }
        catch (BCrypt.Net.SaltParseException) { return false; }
    }
}
