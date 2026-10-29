using System.ComponentModel.DataAnnotations;
using MySqlConnector;

namespace Diwakar.Cms.Api.Settings;

public sealed record WebsiteSettingsInput(
    string? SiteName, string? Tagline, string? PublicEmail, string? PrimaryPhone,
    string? SecondaryPhone, string? Address, string? FacebookUrl, string? InstagramUrl,
    string? LinkedinUrl, string? YoutubeUrl, string? XTwitterUrl, string? FooterText);

public sealed record WebsiteSettingsData(
    string SiteName, string? Tagline, string? PublicEmail, string? PrimaryPhone,
    string? SecondaryPhone, string? Address, string? FacebookUrl, string? InstagramUrl,
    string? LinkedinUrl, string? YoutubeUrl, string? XTwitterUrl, string? FooterText)
{
    public static WebsiteSettingsData Empty { get; } = new("", null, null, null, null, null, null, null, null, null, null, null);
}

public sealed class WebsiteSettingsStore(MySqlDataSource source)
{
    public static bool TryValidate(WebsiteSettingsInput input, out WebsiteSettingsData? settings, out Dictionary<string, string> errors)
    {
        var fieldErrors = new Dictionary<string, string>(StringComparer.Ordinal);
        errors = fieldErrors;
        var siteName = input.SiteName?.Trim() ?? "";
        if (siteName.Length == 0) fieldErrors["siteName"] = "Enter a site name.";
        CheckLength("siteName", siteName, 120);
        var tagline = Optional("tagline", input.Tagline, 180);
        var email = Optional("publicEmail", input.PublicEmail, 255);
        var primaryPhone = Optional("primaryPhone", input.PrimaryPhone, 50);
        var secondaryPhone = Optional("secondaryPhone", input.SecondaryPhone, 50);
        var address = Optional("address", input.Address, 1000);
        var facebook = SocialUrl("facebookUrl", input.FacebookUrl);
        var instagram = SocialUrl("instagramUrl", input.InstagramUrl);
        var linkedin = SocialUrl("linkedinUrl", input.LinkedinUrl);
        var youtube = SocialUrl("youtubeUrl", input.YoutubeUrl);
        var xTwitter = SocialUrl("xTwitterUrl", input.XTwitterUrl);
        var footer = Optional("footerText", input.FooterText, 500);

        if (email is not null && !new EmailAddressAttribute().IsValid(email))
            fieldErrors["publicEmail"] = "Enter a valid public email address.";

        settings = fieldErrors.Count == 0
            ? new WebsiteSettingsData(siteName, tagline, email, primaryPhone, secondaryPhone,
                address, facebook, instagram, linkedin, youtube, xTwitter, footer)
            : null;
        return settings is not null;

        void CheckLength(string name, string value, int maximum)
        {
            if (value.EnumerateRunes().Count() > maximum)
                fieldErrors[name] = $"Use no more than {maximum} characters.";
        }

        string? Optional(string name, string? raw, int maximum)
        {
            var value = raw?.Trim();
            if (string.IsNullOrEmpty(value)) return null;
            CheckLength(name, value, maximum);
            return value;
        }

        string? SocialUrl(string name, string? raw)
        {
            var value = Optional(name, raw, 2048);
            if (value is null) return null;
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || string.IsNullOrEmpty(uri.Host) || uri.UserInfo.Length > 0
                || value.Any(char.IsControl))
                fieldErrors[name] = "Enter a valid HTTP or HTTPS URL.";
            return value;
        }
    }

    public async Task<WebsiteSettingsData> ReadAsync(CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT site_name, tagline, public_email, primary_phone, secondary_phone, address,
                   facebook_url, instagram_url, linkedin_url, youtube_url, x_twitter_url, footer_text
            FROM website_settings WHERE id=1 LIMIT 1
            """;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return WebsiteSettingsData.Empty;
        string? Text(int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
        return new WebsiteSettingsData(reader.GetString(0), Text(1), Text(2), Text(3), Text(4),
            Text(5), Text(6), Text(7), Text(8), Text(9), Text(10), Text(11));
    }

    public async Task SaveAsync(WebsiteSettingsData settings, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO website_settings
                (id, site_name, tagline, public_email, primary_phone, secondary_phone, address,
                 facebook_url, instagram_url, linkedin_url, youtube_url, x_twitter_url, footer_text,
                 created_at, updated_at)
            VALUES (1, @site_name, @tagline, @public_email, @primary_phone, @secondary_phone, @address,
                    @facebook_url, @instagram_url, @linkedin_url, @youtube_url, @x_twitter_url, @footer_text,
                    UTC_TIMESTAMP(), UTC_TIMESTAMP())
            ON DUPLICATE KEY UPDATE
                site_name=VALUES(site_name), tagline=VALUES(tagline), public_email=VALUES(public_email),
                primary_phone=VALUES(primary_phone), secondary_phone=VALUES(secondary_phone),
                address=VALUES(address), facebook_url=VALUES(facebook_url),
                instagram_url=VALUES(instagram_url), linkedin_url=VALUES(linkedin_url),
                youtube_url=VALUES(youtube_url), x_twitter_url=VALUES(x_twitter_url),
                footer_text=VALUES(footer_text), updated_at=UTC_TIMESTAMP()
            """;
        command.Parameters.AddWithValue("@site_name", settings.SiteName);
        command.Parameters.AddWithValue("@tagline", Db(settings.Tagline));
        command.Parameters.AddWithValue("@public_email", Db(settings.PublicEmail));
        command.Parameters.AddWithValue("@primary_phone", Db(settings.PrimaryPhone));
        command.Parameters.AddWithValue("@secondary_phone", Db(settings.SecondaryPhone));
        command.Parameters.AddWithValue("@address", Db(settings.Address));
        command.Parameters.AddWithValue("@facebook_url", Db(settings.FacebookUrl));
        command.Parameters.AddWithValue("@instagram_url", Db(settings.InstagramUrl));
        command.Parameters.AddWithValue("@linkedin_url", Db(settings.LinkedinUrl));
        command.Parameters.AddWithValue("@youtube_url", Db(settings.YoutubeUrl));
        command.Parameters.AddWithValue("@x_twitter_url", Db(settings.XTwitterUrl));
        command.Parameters.AddWithValue("@footer_text", Db(settings.FooterText));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static object Db(string? value) => value is null ? DBNull.Value : value;
}
