using System.Globalization;
using System.Text.Json;
using Diwakar.Cms.Api.Authorization;
using Diwakar.Cms.Api.Media;

namespace Diwakar.Cms.Api.Sections;

public static class SectionEndpoints
{
    private static bool Allowed(HttpContext context) =>
        ((RbacSnapshot)context.Items[typeof(RbacSnapshot)]!).Allows("pages.manage");
    private static IResult Denied() => Results.Json(new { message = "You do not have permission to manage pages." }, statusCode: 403);
    private static IResult Invalid(Dictionary<string, string> errors) =>
        Results.Json(new { message = "Correct the highlighted fields.", errors }, statusCode: 400);
    private static IResult InvalidImage() =>
        Invalid(new Dictionary<string, string> { ["mediaId"] = "Choose an existing public image." });
    private static bool Key(string value, out ulong key) =>
        ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out key) && key > 0;

    public static void MapSections(this RouteGroupBuilder admin)
    {
        admin.MapGet("/pages/{pageId}/sections", async (string pageId, HttpContext context, SectionStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            return Key(pageId, out var page) && await store.ListAsync(page, ct) is { } list
                ? Results.Ok(list) : Results.NotFound();
        });
        admin.MapGet("/pages/{pageId}/sections/images", async (string pageId, HttpContext context, SectionStore store,
            MediaStore media, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            if (!Key(pageId, out var page) || await store.ListAsync(page, ct) is null) return Results.NotFound();
            var pageNumber = int.TryParse(context.Request.Query["page"], out var requested) ? requested : 1;
            try
            {
                var images = await media.ListAsync(pageNumber, context.Request.Query["search"], "images", ct);
                return Results.Ok(new
                {
                    items = images.Items.Select(item => new
                    {
                        id = item.Id, name = item.Title ?? item.OriginalName, url = item.Url,
                        width = item.Width, height = item.Height
                    }).ToArray(),
                    images.Page, images.TotalPages, images.Total
                });
            }
            catch (ArgumentException) { return Invalid(new Dictionary<string, string> { ["search"] = "Enter a shorter search term." }); }
        });
        admin.MapGet("/pages/{pageId}/sections/{id}", async (string pageId, string id, HttpContext context,
            SectionStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            return Key(pageId, out var page) && Key(id, out var key) && await store.GetAsync(page, key, ct) is { } item
                ? Results.Ok(item) : Results.NotFound();
        });
        admin.MapPost("/pages/{pageId}/sections", async (string pageId, JsonElement input, HttpContext context,
            SectionStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            if (!Key(pageId, out var page)) return Results.NotFound();
            if (!SectionValidation.TryParse(input, null, out var change, out var errors)) return Invalid(errors);
            var result = await store.CreateAsync(page, change!, ct);
            return result.Status switch
            {
                "created" => Results.Json(new { message = "Section created successfully.", section = result.Item }, statusCode: 201),
                "invalid-media" => InvalidImage(),
                _ => Results.NotFound()
            };
        });
        admin.MapPatch("/pages/{pageId}/sections/{id}", async (string pageId, string id, JsonElement input,
            HttpContext context, SectionStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            if (!Key(pageId, out var page) || !Key(id, out var key)) return Results.NotFound();
            var item = await store.GetAsync(page, key, ct);
            if (item is null) return Results.NotFound();
            if (!SectionValidation.TryParse(input, item.Type, out var change, out var errors)) return Invalid(errors);
            var result = await store.UpdateAsync(page, key, change!, ct);
            return result.Status switch
            {
                "updated" => Results.Ok(new { message = "Section updated successfully.", section = result.Item }),
                "invalid-media" => InvalidImage(),
                _ => Results.NotFound()
            };
        });
        admin.MapPost("/pages/{pageId}/sections/{id}/move", async (string pageId, string id, MoveSectionInput input,
            HttpContext context, SectionStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            if (!Key(pageId, out var page) || !Key(id, out var key)) return Results.NotFound();
            if (input.Direction is not ("up" or "down"))
                return Invalid(new Dictionary<string, string> { ["direction"] = "Choose up or down." });
            return await store.MoveAsync(page, key, input.Direction, ct) switch
            {
                "moved" => Results.Ok(new { message = "Section order updated." }),
                "boundary" => Results.Json(new { message = "This section cannot move further." }, statusCode: 409),
                _ => Results.NotFound()
            };
        });
        admin.MapPost("/pages/{pageId}/sections/{id}/toggle", async (string pageId, string id,
            HttpContext context, SectionStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            if (!Key(pageId, out var page) || !Key(id, out var key)) return Results.NotFound();
            return await store.ToggleAsync(page, key, ct) == "updated"
                ? Results.Ok(new { message = "Section visibility updated." }) : Results.NotFound();
        });
        admin.MapDelete("/pages/{pageId}/sections/{id}", async (string pageId, string id,
            HttpContext context, SectionStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            if (!Key(pageId, out var page) || !Key(id, out var key)) return Results.NotFound();
            return await store.DeleteAsync(page, key, ct) == "deleted"
                ? Results.Ok(new { message = "Section deleted successfully." }) : Results.NotFound();
        });
    }
}
public sealed record MoveSectionInput(string? Direction);
