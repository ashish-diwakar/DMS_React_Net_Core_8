using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diwakar.Cms.Api.Sections;
using static Diwakar.Cms.Auth.Tests.AuthFixture;

namespace Diwakar.Cms.Auth.Tests;

[Collection("Authentication")]
public sealed class SectionTests(AuthFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<string> Page(string slug)
    {
        await fixture.ExecuteAsync(
            "INSERT INTO pages(title,slug,status,created_at,updated_at) VALUES(@title,@slug,'draft',UTC_TIMESTAMP(),UTC_TIMESTAMP())",
            ("@title", slug), ("@slug", slug));
        return Convert.ToString(await fixture.ScalarAsync("SELECT id FROM pages WHERE slug=@slug", ("@slug", slug)))!;
    }

    [Fact]
    public async Task Sections_require_pages_permission_and_csrf()
    {
        var actor = await fixture.SeedAsync(); await fixture.GrantRoleAsync(actor, "Admin");
        var page = await Page("restricted");
        using var factory = fixture.CreateFactory(); using var client = factory.Browser();
        var basePath = "/api/admin/pages/" + page + "/sections";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(basePath)).StatusCode);
        await Factory.LoginAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(basePath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(basePath + "/images")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(basePath, new { type = "text", heading = "Denied", body = "Body" })).StatusCode);
        await fixture.ExecuteAsync("INSERT INTO model_has_permissions(permission_id,model_type,model_id) SELECT id,@model,@id FROM permissions WHERE name='pages.manage' AND guard_name='web'",
            ("@model", @"App\Models\User"), ("@id", actor.Id));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(basePath)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(basePath + "/images")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(basePath, new { type = "text", heading = "Denied", body = "Body" })).StatusCode);
        Assert.Equal(0L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM page_sections")));
    }

    [Fact]
    public async Task Create_order_move_toggle_and_delete_stay_scoped_to_page()
    {
        var actor = await fixture.SeedAsync(); await fixture.GrantRoleAsync(actor, "Super Admin");
        var page = await Page("first"); var another = await Page("second");
        using var factory = fixture.CreateFactory(); using var client = factory.Browser(); await Factory.LoginAsync(client);
        var path = "/api/admin/pages/" + page + "/sections";
        var first = await client.PostAsJsonAsync(path, new { type = "text", heading = "One", body = "First line\nSecond line", isEnabled = true });
        var second = await client.PostAsJsonAsync(path, new { type = "text", heading = "Two", body = "Second body", isEnabled = true });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("section").GetProperty("id").GetString()!;
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("section").GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync("/api/admin/pages/" + page)).StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal("One", list.GetProperty("sections")[0].GetProperty("heading").GetString());
        Assert.Equal(1, list.GetProperty("sections")[0].GetProperty("sortOrder").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin/pages/" + another + "/sections/" + firstId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync("/api/admin/pages/" + another + "/sections/" + firstId,
            new { heading = "No", body = "No", isEnabled = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path + "/" + secondId + "/move", new { direction = "up" })).StatusCode);
        list = await client.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal("Two", list.GetProperty("sections")[0].GetProperty("heading").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/" + secondId + "/move", new { direction = "up" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path + "/" + firstId + "/toggle", new { })).StatusCode);
        Assert.False((await client.GetFromJsonAsync<JsonElement>(path + "/" + firstId)).GetProperty("isEnabled").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync(path + "/" + secondId)).StatusCode);
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT sort_order FROM page_sections WHERE id=@id", ("@id", firstId))));
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync(path + "/" + firstId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync("/api/admin/pages/" + page)).StatusCode);
    }

    [Fact]
    public async Task Type_validation_media_picker_and_references_are_enforced()
    {
        var actor = await fixture.SeedAsync(); await fixture.GrantRoleAsync(actor, "Super Admin");
        var page = await Page("images");
        await fixture.ExecuteAsync("INSERT INTO media(original_name,title,disk,path,mime_type,size,created_at,updated_at) VALUES('picture.png','Picture','public','media/2026/10/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png','image/png',50,UTC_TIMESTAMP(),UTC_TIMESTAMP())");
        await fixture.ExecuteAsync("INSERT INTO media(original_name,title,disk,path,mime_type,size,created_at,updated_at) VALUES('document.pdf',NULL,'public','media/2026/10/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.pdf','application/pdf',50,UTC_TIMESTAMP(),UTC_TIMESTAMP())");
        var imageId = Convert.ToString(await fixture.ScalarAsync("SELECT id FROM media WHERE original_name='picture.png'"))!;
        var documentId = Convert.ToString(await fixture.ScalarAsync("SELECT id FROM media WHERE original_name='document.pdf'"))!;
        using var factory = fixture.CreateFactory(); using var client = factory.Browser(); await Factory.LoginAsync(client);
        var path = "/api/admin/pages/" + page + "/sections";
        var picker = await client.GetFromJsonAsync<JsonElement>(path + "/images?search=picture");
        Assert.Equal(1, picker.GetProperty("total").GetInt32());
        Assert.Equal(imageId, picker.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.Equal("Picture", picker.GetProperty("items")[0].GetProperty("name").GetString());
        Assert.False(picker.GetProperty("items")[0].TryGetProperty("mimeType", out _));
        async Task AssertInvalid(object input, string field)
        {
            var response = await client.PostAsJsonAsync(path, input);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty(field, out _));
        }
        await AssertInvalid(new { type = "text", heading = "<b>Bad</b>", body = "Body" }, "heading");
        await AssertInvalid(new { type = "image_text", heading = "Missing", body = "Body" }, "mediaId");
        await AssertInvalid(new { type = "image_text", heading = "Document", body = "Body", mediaId = documentId, imageAlt = "Alt" }, "mediaId");
        await AssertInvalid(new { type = "hero", heading = "Bad link", buttonLabel = "Visit", buttonUrl = "javascript:alert(1)" }, "buttonUrl");
        await AssertInvalid(new { type = "text", heading = "Extra", body = "Body", buttonUrl = "/contact" }, "buttonUrl");
        var create = await client.PostAsJsonAsync(path, new { type = "image_text", heading = "Image", body = "Body",
            mediaId = imageId, imageAlt = "Description", isEnabled = true });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("section").GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync("/api/admin/media/" + imageId)).StatusCode);
        var changedType = await client.PatchAsJsonAsync(path + "/" + id, new { type = "hero", heading = "Image", body = "Body",
            mediaId = imageId, imageAlt = "Description", isEnabled = true });
        Assert.Equal(HttpStatusCode.BadRequest, changedType.StatusCode);
        Assert.Equal("image_text", await fixture.ScalarAsync("SELECT type FROM page_sections WHERE id=@id", ("@id", id)));
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync(path + "/" + id)).StatusCode);
    }

    [Fact]
    public async Task Text_body_accepts_multiline_content_up_to_ten_thousand_characters()
    {
        var actor = await fixture.SeedAsync(); await fixture.GrantRoleAsync(actor, "Super Admin");
        var page = await Page("long-text");
        using var factory = fixture.CreateFactory(); using var client = factory.Browser(); await Factory.LoginAsync(client);
        var path = "/api/admin/pages/" + page + "/sections";
        var body = "First line\n" + new string('a', 9989);
        Assert.Equal(10000, body.Length);
        var created = await client.PostAsJsonAsync(path, new { type = "text", heading = "Long text", body, isEnabled = true });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var invalid = await client.PostAsJsonAsync(path, new { type = "text", heading = "Too long", body = body + "x", isEnabled = true });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM page_sections WHERE page_id=@page", ("@page", page))));
    }
    [Theory]
    [InlineData("/contact", true)]
    [InlineData("https://example.com/contact", true)]
    [InlineData("//evil.test", false)]
    [InlineData("/%2f%2fevil.test", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("https://user:pass@example.com", false)]
    [InlineData("/bad\\path", false)]
    public void Button_urls_are_safe(string url, bool allowed) => Assert.Equal(allowed, SectionValidation.SafeUrl(url));
}
