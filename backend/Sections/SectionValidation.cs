using System.Globalization;
using System.Text.Json;
using System.Text;

namespace Diwakar.Cms.Api.Sections;

public sealed record SectionChange(string Type, string Heading, string? Body, bool IsEnabled,
    ulong? MediaId, string? ImageAlt, string? ButtonLabel, string? ButtonUrl);

public static class SectionValidation
{
    private static readonly HashSet<string> CreateFields = new(StringComparer.Ordinal)
        { "type", "heading", "body", "isEnabled", "mediaId", "imageAlt", "buttonLabel", "buttonUrl" };
    private static readonly HashSet<string> UpdateFields = new(StringComparer.Ordinal)
        { "heading", "body", "isEnabled", "mediaId", "imageAlt", "buttonLabel", "buttonUrl" };
    public static bool ValidType(string? type) => type is "hero" or "text" or "image_text";

    private static bool Text(JsonElement input, string field, out string? value, out bool present)
    {
        value = null; present = input.TryGetProperty(field, out var item);
        if (!present) return true;
        if (item.ValueKind == JsonValueKind.Null) return true;
        if (item.ValueKind != JsonValueKind.String) return false;
        value = item.GetString()?.Trim();
        if (value == "") value = null;
        return true;
    }

    private static bool Plain(string? value, int maximum, bool required) =>
        (!required || !string.IsNullOrEmpty(value)) &&
        (value is null || (value.EnumerateRunes().Count() <= maximum &&
         !value.Contains('<') && !value.Contains('>') && !value.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t')));

    public static bool TryParse(JsonElement input, string? existingType, out SectionChange? change,
        out Dictionary<string, string> errors)
    {
        errors = new(StringComparer.Ordinal); change = null;
        if (input.ValueKind != JsonValueKind.Object)
        {
            errors["section"] = "Provide a valid section.";
            return false;
        }
        var updating = existingType is not null;
        foreach (var property in input.EnumerateObject())
            if (!(updating ? UpdateFields : CreateFields).Contains(property.Name))
                errors[property.Name] = "This field is not editable.";

        var type = existingType;
        if (!updating)
        {
            if (!Text(input, "type", out type, out _) || !ValidType(type))
                errors["type"] = "Choose Hero, Text Content, or Image with Text.";
        }
        type ??= "";

        if (!Text(input, "heading", out var heading, out _) || !Plain(heading, 200, true))
            errors["heading"] = "Enter a plain-text heading of no more than 200 characters.";
        if (!Text(input, "body", out var body, out _) ||
            !Plain(body, type == "text" ? 10000 : type == "image_text" ? 5000 : 500, type != "hero"))
            errors["body"] = "Enter valid plain text within this section type's limit.";

        var enabled = true;
        if (input.TryGetProperty("isEnabled", out var enabledElement))
        {
            if (enabledElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
                enabled = enabledElement.GetBoolean();
            else errors["isEnabled"] = "Choose whether this section is enabled.";
        }
        else if (updating) errors["isEnabled"] = "Choose whether this section is enabled.";

        ulong? mediaId = null;
        var mediaPresent = input.TryGetProperty("mediaId", out var mediaElement);
        if (type == "text" && mediaPresent) errors["mediaId"] = "Text Content cannot use an image.";
        else if (mediaPresent && mediaElement.ValueKind is not JsonValueKind.Null)
        {
            if (mediaElement.ValueKind != JsonValueKind.String ||
                !ulong.TryParse(mediaElement.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed == 0)
                errors["mediaId"] = "Choose an existing image.";
            else mediaId = parsed;
        }
        if (type == "image_text" && mediaId is null) errors["mediaId"] = "Choose an existing image.";

        var altPresent = input.TryGetProperty("imageAlt", out _);
        if (type == "text" && altPresent) errors["imageAlt"] = "Text Content cannot use an image description.";
        if (!Text(input, "imageAlt", out var imageAlt, out _) || !Plain(imageAlt, 255, mediaId is not null))
            errors["imageAlt"] = "Enter a plain-text image description of no more than 255 characters.";
        if (mediaId is null && imageAlt is not null) errors["imageAlt"] = "Choose an image before adding a description.";

        var labelPresent = input.TryGetProperty("buttonLabel", out _);
        var urlPresent = input.TryGetProperty("buttonUrl", out _);
        if (type != "hero" && (labelPresent || urlPresent))
        {
            if (labelPresent) errors["buttonLabel"] = "Only Hero sections can have a button.";
            if (urlPresent) errors["buttonUrl"] = "Only Hero sections can have a button.";
        }
        if (!Text(input, "buttonLabel", out var label, out _) || !Plain(label, 80, false))
            errors["buttonLabel"] = "Enter a plain-text button label of no more than 80 characters.";
        if (!Text(input, "buttonUrl", out var url, out _) || (url is not null && (url.Length > 2048 || !SafeUrl(url))))
            errors["buttonUrl"] = "Use a root-relative path or a valid HTTP/HTTPS URL.";
        if (type == "hero" && (label is null) != (url is null))
        {
            errors[label is null ? "buttonLabel" : "buttonUrl"] = "Provide both button label and destination, or leave both blank.";
        }

        if (errors.Count != 0) return false;
        change = new(type, heading!, body, enabled, mediaId, imageAlt, label, url);
        return true;
    }

    public static bool SafeUrl(string value)
    {
        if (value.Any(char.IsWhiteSpace) || value.Any(char.IsControl) || value.Contains('\\')) return false;
        var decoded = value;
        for (var index = 0; index < value.Length; index++)
        {
            if (decoded.StartsWith("//", StringComparison.Ordinal) || decoded.Contains('\\') ||
                decoded.Any(char.IsControl) || decoded.Any(char.IsWhiteSpace)) return false;
            var next = Uri.UnescapeDataString(decoded);
            if (next == decoded) break;
            decoded = next;
        }
        if (decoded.StartsWith("//", StringComparison.Ordinal)) return false;
        if (value.StartsWith("/", StringComparison.Ordinal))
            return !value.StartsWith("//", StringComparison.Ordinal) && !Uri.TryCreate(value, UriKind.Absolute, out _);
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https" && !string.IsNullOrEmpty(uri.Host) &&
            string.IsNullOrEmpty(uri.UserInfo);
    }
}
