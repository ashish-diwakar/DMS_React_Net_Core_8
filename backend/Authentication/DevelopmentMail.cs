namespace Diwakar.Cms.Api.Authentication;

public sealed class DevelopmentMail(IWebHostEnvironment environment, IConfiguration configuration)
{
    public async Task SendAsync(string recipient, string subject, string link, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) throw new InvalidOperationException("No approved email delivery is configured.");
        if (!AuthValidation.Email(recipient)) throw new InvalidOperationException("Invalid mail recipient.");
        var directory = Path.GetFullPath(configuration["Authentication:MailDirectory"] ?? Path.Combine(environment.ContentRootPath, ".local", "mail"));
        Directory.CreateDirectory(directory);
        // Files are private developer mail artifacts, never static assets or logs.
        var message = $"To: {recipient}\r\nSubject: {subject}\r\nMIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nOpen this link in the CMS. It expires after 60 minutes.\r\n{link}\r\n";
        await File.WriteAllTextAsync(Path.Combine(directory, Guid.NewGuid().ToString("N") + ".eml"), message, ct);
    }
}
