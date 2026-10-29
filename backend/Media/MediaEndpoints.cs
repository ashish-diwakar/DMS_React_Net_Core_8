using Diwakar.Cms.Api.Authorization;

namespace Diwakar.Cms.Api.Media;

public sealed record MediaTitleInput(string? Title);

public static class MediaEndpoints
{
    public const long UploadBodyLimit = 110L * 1024 * 1024;
    private static bool Allowed(HttpContext context) =>
        ((RbacSnapshot)context.Items[typeof(RbacSnapshot)]!).Allows("media.manage");
    private static IResult Denied() => Results.Json(new { message = "You do not have permission to manage media." }, statusCode: 403);
    private static IResult Bad(string message, int status = 400) => Results.Json(new { message }, statusCode: status);

    public static void MapMedia(this WebApplication app, RouteGroupBuilder admin)
    {
        app.MapGet("/api/media/{id}/file", async (string id, MediaStore store, CancellationToken ct) =>
        {
            if (!ulong.TryParse(id, out var key) || key == 0) return Results.NotFound();
            var media = await store.FileAsync(key, ct);
            return media is not null && File.Exists(media.Value.Path)
                ? Results.File(media.Value.Path, media.Value.Mime, enableRangeProcessing: true)
                : Results.NotFound();
        });
        admin.MapGet("/media", async (HttpContext context, MediaStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            var page = int.TryParse(context.Request.Query["page"], out var parsed) ? parsed : 1;
            try { return Results.Ok(await store.ListAsync(page, context.Request.Query["search"], context.Request.Query["type"], ct)); }
            catch (ArgumentException) { return Bad("Enter a valid search and filter."); }
        });
        admin.MapPost("/media", async (HttpContext context, MediaStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            if (!context.Request.HasFormContentType) return Bad("Choose JPEG, PNG, WebP, or PDF files.");
            IFormCollection form;
            try { form = await context.Request.ReadFormAsync(ct); }
            catch (InvalidDataException) { return Bad("The upload is too large or invalid.", 413); }
            var files = form.Files.ToList();
            var result = await MediaUploadValidator.ValidateAsync(files, ct);
            if (!result.IsValid) return Bad(result.Error!);
            var items = await store.UploadAsync(result.Files, ct);
            return Results.Json(new { message = items.Count == 1 ? "Media uploaded." : $"{items.Count} files uploaded.", items }, statusCode: 201);
        });
        admin.MapPatch("/media/{id}", async (string id, MediaTitleInput input, HttpContext context, MediaStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            if (!ulong.TryParse(id, out var key) || key == 0) return Bad("Invalid media item.");
            var title = input.Title?.Trim();
            if (title?.EnumerateRunes().Count() > 255 || title?.Any(char.IsControl) == true)
                return Bad("Display Name must be no more than 255 characters.");
            var item = await store.SetTitleAsync(key, string.IsNullOrEmpty(title) ? null : title, ct);
            return item is null ? Results.NotFound() : Results.Ok(new { message = "Display Name updated.", item });
        });
        admin.MapDelete("/media/{id}", async (string id, HttpContext context, MediaStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            if (!ulong.TryParse(id, out var key) || key == 0) return Bad("Invalid media item.");
            var result = await store.DeleteAsync(key, ct);
            return result.Status switch
            {
                "deleted" => Results.Ok(new { message = "Media deleted." }),
                "referenced" => Bad("This media item is used by a page section and cannot be deleted.", 409),
                "unsafe" => Bad("This media item has an unsupported storage path and cannot be deleted.", 409),
                _ => Results.NotFound()
            };
        });
    }
}

