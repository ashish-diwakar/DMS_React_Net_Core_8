using System.Globalization;
using System.Text.Json;
using Diwakar.Cms.Api.Authorization;
using MySqlConnector;

namespace Diwakar.Cms.Api.Pages;

public static class PageEndpoints
{
    private static bool Allowed(HttpContext context) =>
        ((RbacSnapshot)context.Items[typeof(RbacSnapshot)]!).Allows("pages.manage");
    private static IResult Denied() =>
        Results.Json(new { message = "You do not have permission to manage pages." }, statusCode: 403);
    private static IResult Invalid(Dictionary<string, string> errors) =>
        Results.Json(new { message = "Correct the highlighted fields.", errors }, statusCode: 400);
    private static IResult Duplicate() =>
        Invalid(new Dictionary<string, string> { ["slug"] = "This slug is already in use. Choose another slug." });
    private static bool Key(string id, out ulong key) =>
        ulong.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out key) && key > 0;

    public static void MapPages(this RouteGroupBuilder admin)
    {
        admin.MapGet("/pages", async (HttpContext context, PageStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            var page = int.TryParse(context.Request.Query["page"], out var requested) ? requested : 1;
            try { return Results.Ok(await store.ListAsync(page, context.Request.Query["search"], context.Request.Query["status"], ct)); }
            catch (ArgumentException) { return Invalid(new Dictionary<string, string> { ["filters"] = "Enter a valid search and status." }); }
        });
        admin.MapGet("/pages/{id}", async (string id, HttpContext context, PageStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            return Key(id, out var key) && await store.GetAsync(key, ct) is { } page ? Results.Ok(page) : Results.NotFound();
        });
        admin.MapPost("/pages", async (JsonElement input, HttpContext context, PageStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            if (!PageValidation.TryParse(input, false, out var change, out var errors)) return Invalid(errors);
            if (await store.SlugExistsAsync(change!.Slug!, null, ct)) return Duplicate();
            try
            {
                var page = await store.CreateAsync(change, ct);
                return Results.Json(new { message = "Page created successfully.", page }, statusCode: 201);
            }
            catch (MySqlException error) when (error.Number == 1062) { return Duplicate(); }
        });
        admin.MapPatch("/pages/{id}", async (string id, JsonElement input, HttpContext context, PageStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            if (!Key(id, out var key) || await store.GetAsync(key, ct) is null) return Results.NotFound();
            if (!PageValidation.TryParse(input, true, out var change, out var errors)) return Invalid(errors);
            if (change!.Slug is not null && await store.SlugExistsAsync(change.Slug, key, ct)) return Duplicate();
            try
            {
                var page = await store.UpdateAsync(key, change, ct);
                return page is null ? Results.NotFound() : Results.Ok(new { message = "Page updated successfully.", page });
            }
            catch (MySqlException error) when (error.Number == 1062) { return Duplicate(); }
        });
        admin.MapDelete("/pages/{id}", async (string id, HttpContext context, PageStore store, CancellationToken ct) =>
        {
            if (!Allowed(context)) return Denied();
            if (!Key(id, out var key)) return Results.NotFound();
            return await store.DeleteAsync(key, ct) switch
            {
                "deleted" => Results.Ok(new { message = "Page deleted successfully." }),
                "referenced" => Results.Json(new { message = "This Page contains sections. Remove its sections before deleting the Page." }, statusCode: 409),
                _ => Results.NotFound()
            };
        });
    }
}
