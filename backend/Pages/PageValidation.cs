using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Diwakar.Cms.Api.Pages;

public sealed record PageChange(string? Title, string? Slug, string? Status);

public static class PageValidation
{
    private static readonly Regex SafeInput = new(@"\A[\p{L}\p{M}\p{N} _-]*\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SafeSlug = new(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "admin", "login", "logout", "register", "storage", "api", "dashboard", "profile",
        "forgot-password", "reset-password", "verify-email", "email", "confirm-password",
        "password", "up", "build"
    };

    private static bool Field(JsonElement body, string name, out string? value)
    {
        value = null;
        if (!body.TryGetProperty(name, out var field)) return false;
        if (field.ValueKind == JsonValueKind.String) value = field.GetString();
        return true;
    }

    public static bool TryParse(JsonElement body, bool updating, out PageChange? change,
        out Dictionary<string, string> errors)
    {
        errors = new(StringComparer.Ordinal);
        change = null;
        if (body.ValueKind != JsonValueKind.Object)
        {
            errors["page"] = "Provide a valid Page.";
            return false;
        }

        var hasTitle = Field(body, "title", out var titleInput);
        var hasSlug = Field(body, "slug", out var slugInput);
        var hasStatus = Field(body, "status", out var statusInput);
        var title = hasTitle ? titleInput?.Trim() : null;
        string? slug = null;
        string? status = null;

        if ((!updating || hasTitle) &&
            (string.IsNullOrWhiteSpace(title) || title.EnumerateRunes().Count() > 200 ||
             title.IndexOfAny(['<', '>']) >= 0 || title.Any(char.IsControl)))
            errors["title"] = "Enter a plain-text title of no more than 200 characters.";

        if (!updating || hasSlug)
        {
            var raw = slugInput;
            if (!updating && string.IsNullOrWhiteSpace(raw)) raw = title;
            if (updating && string.IsNullOrWhiteSpace(raw))
                errors["slug"] = "Enter a slug; existing slugs cannot be blank.";
            else if (raw is null || (hasSlug && !string.IsNullOrWhiteSpace(slugInput) && !SafeInput.IsMatch(raw.Trim())))
                errors["slug"] = "Use a single slug without paths or URLs.";
            else
            {
                slug = Slugify(raw);
                if (slug.Length > 150 || !SafeSlug.IsMatch(slug))
                    errors["slug"] = "Enter a single slug of no more than 150 ASCII letters, numbers, and hyphens.";
                else if (Reserved.Contains(slug))
                    errors["slug"] = "This slug is reserved for the application.";
            }
        }

        if (!updating || hasStatus)
        {
            status = !updating && !hasStatus ? "draft" : statusInput;
            if (status is not ("draft" or "published"))
                errors["status"] = "Choose Draft or Published.";
        }

        if (errors.Count != 0) return false;
        change = new PageChange(title, slug, status);
        return true;
    }

    public static string Slugify(string value)
    {
        var output = new StringBuilder();
        var pendingDash = false;
        foreach (var rune in value.Trim().Normalize(NormalizationForm.FormD).EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark) continue;
            if (rune.Value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingDash && output.Length > 0) output.Append('-');
                output.Append(char.ToLowerInvariant((char)rune.Value));
                pendingDash = false;
            }
            else pendingDash = output.Length > 0;
        }
        return output.ToString();
    }
}
