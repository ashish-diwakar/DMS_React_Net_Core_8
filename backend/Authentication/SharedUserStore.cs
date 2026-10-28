using Microsoft.AspNetCore.Identity;
using MySqlConnector;

namespace Diwakar.Cms.Api.Authentication;

// A narrow Identity token-provider adapter for the existing Laravel schema.
// Provisioning and generic Identity writes are deliberately unsupported.
public sealed class SharedUserStore(MySqlDataSource source) : IUserStore<CmsUser>, IUserSecurityStampStore<CmsUser>
{
    public async Task<CmsUser?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, email, password, email_verified_at FROM users WHERE email = @email LIMIT 1";
        command.Parameters.AddWithValue("@email", email);
        return await ReadAsync(command, ct);
    }

    public async Task<CmsUser?> FindByIdAsync(string id, CancellationToken ct)
    {
        if (!ulong.TryParse(id, out var userId)) return null;
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, email, password, email_verified_at FROM users WHERE id = @id";
        command.Parameters.AddWithValue("@id", userId);
        return await ReadAsync(command, ct);
    }

    private static async Task<CmsUser?> ReadAsync(MySqlCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new CmsUser(reader.GetUInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), !reader.IsDBNull(4))
            : null;
    }

    public async Task<bool> ChangePasswordAsync(CmsUser user, string hash, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE users SET password = @hash, remember_token = @remember, updated_at = UTC_TIMESTAMP()
            WHERE id = @id AND BINARY password = @original AND BINARY email = @email
            """;
        command.Parameters.AddWithValue("@hash", hash);
        command.Parameters.AddWithValue("@remember", Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(30)));
        command.Parameters.AddWithValue("@id", user.Id);
        command.Parameters.AddWithValue("@original", user.PasswordHash);
        command.Parameters.AddWithValue("@email", user.Email);
        // One atomic compare-and-update consumes all tokens bound to the old hash.
        // Laravel's separately managed reset-token and session rows are untouched.
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<bool> VerifyEmailAsync(CmsUser user, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE users SET email_verified_at = COALESCE(email_verified_at, UTC_TIMESTAMP()), updated_at = UTC_TIMESTAMP()
            WHERE id = @id AND BINARY email = @email AND BINARY password = @hash
            """;
        command.Parameters.AddWithValue("@id", user.Id);
        command.Parameters.AddWithValue("@email", user.Email);
        command.Parameters.AddWithValue("@hash", user.PasswordHash);
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    public Task<string> GetUserIdAsync(CmsUser user, CancellationToken ct) => Task.FromResult(user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public Task<string?> GetUserNameAsync(CmsUser user, CancellationToken ct) => Task.FromResult<string?>(user.Email);
    public Task<string?> GetNormalizedUserNameAsync(CmsUser user, CancellationToken ct) => Task.FromResult<string?>(user.Email.ToUpperInvariant());
    public Task<string?> GetSecurityStampAsync(CmsUser user, CancellationToken ct) => Task.FromResult<string?>(user.SecurityStamp);
    public Task<CmsUser?> FindByNameAsync(string name, CancellationToken ct) => FindByEmailAsync(name, ct);
    public Task SetUserNameAsync(CmsUser user, string? name, CancellationToken ct) => throw new NotSupportedException();
    public Task SetNormalizedUserNameAsync(CmsUser user, string? name, CancellationToken ct) => throw new NotSupportedException();
    public Task SetSecurityStampAsync(CmsUser user, string stamp, CancellationToken ct) => throw new NotSupportedException("Security stamps derive from the shared password hash.");
    public Task<IdentityResult> CreateAsync(CmsUser user, CancellationToken ct) => throw new NotSupportedException();
    public Task<IdentityResult> UpdateAsync(CmsUser user, CancellationToken ct) => throw new NotSupportedException("Use explicit conditional authentication writes.");
    public Task<IdentityResult> DeleteAsync(CmsUser user, CancellationToken ct) => throw new NotSupportedException();
    public void Dispose() { }
}
