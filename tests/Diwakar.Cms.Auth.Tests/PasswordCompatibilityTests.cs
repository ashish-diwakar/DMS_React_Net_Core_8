using System.Diagnostics;
using System.Text.Json;
using Diwakar.Cms.Api.Authentication;

namespace Diwakar.Cms.Auth.Tests;

public sealed class PasswordCompatibilityTests
{
    public static async Task<string> PhpAsync(object input)
    {
        var executable = Environment.GetEnvironmentVariable("CMS_TEST_PHP") ?? "C:/xampp/php/php.exe";
        var start = new ProcessStartInfo(executable) { RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "php-bcrypt.php"));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("PHP test runtime unavailable.");
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(input));
        process.StandardInput.Close();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
        return output;
    }

    [Theory]
    [InlineData("ordinary-password")]
    [InlineData("पानी-प्रबंधन-é🔒")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaLONG")]
    public async Task Bcrypt_is_compatible_in_both_directions(string password)
    {
        var hasher = new PasswordHasher();
        var phpHash = await PhpAsync(new { action = "hash", password });
        Assert.StartsWith("$2y$12$", phpHash);
        Assert.True(hasher.Verify(password, phpHash));
        var dotnetHash = hasher.Hash(password);
        Assert.StartsWith("$2y$12$", dotnetHash);
        Assert.Equal("yes", await PhpAsync(new { action = "verify", password, hash = dotnetHash }));
    }

    [Fact]
    public void Malformed_unsupported_and_null_passwords_fail_safely()
    {
        var hasher = new PasswordHasher();
        Assert.False(hasher.Verify("anything", "$argon2id$unsupported"));
        Assert.False(hasher.Verify("anything", "$2y$bad"));
        Assert.False(hasher.Verify("null\0byte", null));
    }
}
