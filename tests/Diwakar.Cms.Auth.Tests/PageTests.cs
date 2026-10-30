using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Diwakar.Cms.Auth.Tests.AuthFixture;

namespace Diwakar.Cms.Auth.Tests;

[Collection("Authentication")]
public sealed class PageTests(AuthFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Guest_and_default_admin_cannot_manage_pages()
    {
        var actor = await fixture.SeedAsync(); await fixture.GrantRoleAsync(actor, "Admin");
        await fixture.ExecuteAsync("INSERT INTO pages(title,slug,status,created_at,updated_at) VALUES('Existing','existing','draft',UTC_TIMESTAMP(),UTC_TIMESTAMP())");
        var id = Convert.ToUInt64(await fixture.ScalarAsync("SELECT id FROM pages WHERE slug='existing'"));
        using var factory = fixture.CreateFactory(); using var client = factory.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/pages")).StatusCode);
        await Factory.LoginAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/pages")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/pages/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/pages", new { title = "Attempt", slug = "attempt" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync("/api/admin/pages/" + id, new { title = "Changed" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync("/api/admin/pages/" + id)).StatusCode);
        Assert.Equal("Existing", await fixture.ScalarAsync("SELECT title FROM pages WHERE id=@id", ("@id", id)));
    }

    [Fact]
    public async Task Super_admin_can_create_browse_edit_and_delete_page_identity()
    {
        var actor = await fixture.SeedAsync(); await fixture.GrantRoleAsync(actor, "Super Admin");
        using var factory = fixture.CreateFactory(); using var client = factory.Browser(); await Factory.LoginAsync(client);
        var create = await client.PostAsJsonAsync("/api/admin/pages", new { title = "  Crème Brûlée  ", slug = "", extra = "ignored" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var page = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("page");
        var id = page.GetProperty("id").GetString()!;
        Assert.Equal("Crème Brûlée", page.GetProperty("title").GetString());
        Assert.Equal("creme-brulee", page.GetProperty("slug").GetString());
        Assert.Equal("draft", page.GetProperty("status").GetString());
        var read = await client.GetFromJsonAsync<JsonElement>("/api/admin/pages/" + id);
        Assert.Equal("creme-brulee", read.GetProperty("slug").GetString());
        var edit = await client.PatchAsJsonAsync("/api/admin/pages/" + id, new { title = "Different title", status = "published", extra = "ignored" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal("creme-brulee", await fixture.ScalarAsync("SELECT slug FROM pages WHERE id=@id", ("@id", id)));
        Assert.Equal("published", await fixture.ScalarAsync("SELECT status FROM pages WHERE id=@id", ("@id", id)));
        var listed = await client.GetFromJsonAsync<JsonElement>("/api/admin/pages?search=creme&status=published");
        Assert.Equal(1, listed.GetProperty("total").GetInt32());
        Assert.Equal("Different title", listed.GetProperty("items")[0].GetProperty("title").GetString());
        var drafts = await client.GetFromJsonAsync<JsonElement>("/api/admin/pages?status=draft");
        Assert.Equal(0, drafts.GetProperty("total").GetInt32());
        var slugEdit = await client.PatchAsJsonAsync("/api/admin/pages/" + id, new { slug = "new-page" });
        Assert.Equal(HttpStatusCode.OK, slugEdit.StatusCode);
        Assert.Equal("new-page", await fixture.ScalarAsync("SELECT slug FROM pages WHERE id=@id", ("@id", id)));
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync("/api/admin/pages/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin/pages/" + id)).StatusCode);
    }

    [Fact]
    public async Task Slug_title_status_and_uniqueness_rules_are_server_enforced()
    {
        var actor = await fixture.SeedAsync(); await fixture.GrantRoleAsync(actor, "Super Admin");
        using var factory = fixture.CreateFactory(); using var client = factory.Browser(); await Factory.LoginAsync(client);
        async Task<string> ErrorField(object body, string field)
        {
            var response = await client.PostAsJsonAsync("/api/admin/pages", body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty(field).GetString()!;
        }
        Assert.NotEmpty(await ErrorField(new { title = "" }, "title"));
        Assert.NotEmpty(await ErrorField(new { title = "<script>" }, "title"));
        Assert.NotEmpty(await ErrorField(new { title = "Sample", slug = "../admin" }, "slug"));
        Assert.NotEmpty(await ErrorField(new { title = "Sample", slug = "api" }, "slug"));
        Assert.NotEmpty(await ErrorField(new { title = "Sample", status = "scheduled" }, "status"));
        Assert.NotEmpty(await ErrorField(new { title = "हिन्दी" }, "slug"));
        var first = await client.PostAsJsonAsync("/api/admin/pages", new { title = "Sample", slug = "sample" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.NotEmpty(await ErrorField(new { title = "Duplicate", slug = "sample" }, "slug"));
        var id = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("page").GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PatchAsJsonAsync("/api/admin/pages/999999999", new { slug = "sample" })).StatusCode);
        var blank = await client.PatchAsJsonAsync("/api/admin/pages/" + id, new { slug = "" });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal("sample", await fixture.ScalarAsync("SELECT slug FROM pages WHERE id=@id", ("@id", id)));
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM pages")));
    }

    [Fact]
    public async Task Section_references_block_page_deletion_without_removing_content()
    {
        var actor = await fixture.SeedAsync(); await fixture.GrantRoleAsync(actor, "Super Admin");
        await fixture.ExecuteAsync("INSERT INTO pages(title,slug,status,created_at,updated_at) VALUES('Referenced','referenced','draft',UTC_TIMESTAMP(),UTC_TIMESTAMP())");
        var id = Convert.ToUInt64(await fixture.ScalarAsync("SELECT id FROM pages WHERE slug='referenced'"));
        await fixture.ExecuteAsync("INSERT INTO page_sections(page_id) VALUES(@id)", ("@id", id));
        using var factory = fixture.CreateFactory(); using var client = factory.Browser(); await Factory.LoginAsync(client);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync("/api/admin/pages/" + id)).StatusCode);
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM pages WHERE id=@id", ("@id", id))));
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM page_sections WHERE page_id=@id", ("@id", id))));
        await fixture.ExecuteAsync("DELETE FROM page_sections WHERE page_id=@id", ("@id", id));
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync("/api/admin/pages/" + id)).StatusCode);
    }

    [Fact]
    public async Task Explicitly_granted_admin_can_manage_pages_but_writes_require_csrf()
    {
        var actor = await fixture.SeedAsync(); await fixture.GrantRoleAsync(actor, "Admin");
        await fixture.ExecuteAsync("INSERT INTO model_has_permissions(permission_id,model_type,model_id) SELECT id,@model,@id FROM permissions WHERE name='pages.manage' AND guard_name='web'",
            ("@model", @"App\Models\User"), ("@id", actor.Id));
        using var factory = fixture.CreateFactory(); using var client = factory.Browser(); await Factory.LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/pages")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/pages", new { title = "Blocked" })).StatusCode);
        Assert.Equal(0L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM pages")));
    }

    [Fact]
    public async Task Page_listing_is_paginated_and_sorted_by_update_time_then_id()
    {
        var actor = await fixture.SeedAsync(); await fixture.GrantRoleAsync(actor, "Super Admin");
        for (var index = 1; index <= 21; index++)
            await fixture.ExecuteAsync(
                "INSERT INTO pages(title,slug,status,created_at,updated_at) VALUES(@title,@slug,'draft',UTC_TIMESTAMP(),UTC_TIMESTAMP())",
                ("@title", "Page " + index), ("@slug", "page-" + index));
        using var factory = fixture.CreateFactory(); using var client = factory.Browser(); await Factory.LoginAsync(client);
        var first = await client.GetFromJsonAsync<JsonElement>("/api/admin/pages?page=1");
        Assert.Equal(21, first.GetProperty("total").GetInt32());
        Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        Assert.Equal(20, first.GetProperty("items").GetArrayLength());
        Assert.Equal("page-21", first.GetProperty("items")[0].GetProperty("slug").GetString());
        var second = await client.GetFromJsonAsync<JsonElement>("/api/admin/pages?page=2");
        Assert.Equal(1, second.GetProperty("items").GetArrayLength());
        Assert.Equal("page-1", second.GetProperty("items")[0].GetProperty("slug").GetString());
    }

}
