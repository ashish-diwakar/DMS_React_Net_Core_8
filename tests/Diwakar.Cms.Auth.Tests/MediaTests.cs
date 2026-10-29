using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Diwakar.Cms.Api.Media;
using static Diwakar.Cms.Auth.Tests.AuthFixture;

namespace Diwakar.Cms.Auth.Tests;

[Collection("Authentication")]
public sealed class MediaTests(AuthFixture fixture) : IAsyncLifetime
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private static MultipartFormDataContent Upload(string name, byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "files", name);
        return form;
    }

    [Fact]
    public async Task Guest_and_default_admin_cannot_manage_media()
    {
        var user = await fixture.SeedAsync(); await fixture.GrantRoleAsync(user, "Admin");
        using var factory = fixture.CreateFactory(); using var client = factory.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/media")).StatusCode);
        await Factory.LoginAsync(client);
        var authorization = await client.GetFromJsonAsync<JsonElement>("/api/auth/authorization");
        Assert.False(authorization.GetProperty("canManageMedia").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/media")).StatusCode);
        using var form = Upload("sample.png", Png);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/admin/media", form)).StatusCode);
        Assert.Equal(0L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM media")));
    }

    [Fact]
    public async Task Super_admin_uploads_lists_edits_serves_and_deletes_media_with_reference_guard()
    {
        var user = await fixture.SeedAsync(); await fixture.GrantRoleAsync(user, "Super Admin");
        using var factory = fixture.CreateFactory(); using var client = factory.Browser(); await Factory.LoginAsync(client);
        using var form = Upload("sample.png", Png);
        var upload = await client.PostAsync("/api/admin/media", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var item = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items")[0];
        var id = item.GetProperty("id").GetString()!;
        Assert.Equal("image/png", item.GetProperty("mimeType").GetString());
        Assert.Equal(1u, item.GetProperty("width").GetUInt32());
        var relative = (string) (await fixture.ScalarAsync("SELECT path FROM media WHERE id=@id", ("@id", id)))!;
        Assert.Matches(@"^media/\d{4}/\d{2}/[a-f0-9]{40}\.png$", relative);
        Assert.True(File.Exists(Path.Combine(fixture.MediaDirectory, relative.Replace('/', Path.DirectorySeparatorChar))));
        var listed = await client.GetFromJsonAsync<JsonElement>("/api/admin/media?type=images&search=sample");
        Assert.Equal(1, listed.GetProperty("total").GetInt32());
        Assert.Equal(Png, await client.GetByteArrayAsync($"/api/media/{id}/file"));
        var edit = await client.PatchAsJsonAsync($"/api/admin/media/{id}", new { title = "Edited image", path = "ignored" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal("Edited image", await fixture.ScalarAsync("SELECT title FROM media WHERE id=@id", ("@id", id)));
        var byDisplayName = await client.GetFromJsonAsync<JsonElement>("/api/admin/media?search=Edited&type=images");
        Assert.Equal(1, byDisplayName.GetProperty("total").GetInt32());
        var byFilename = await client.GetFromJsonAsync<JsonElement>("/api/admin/media?search=sample&type=images");
        Assert.Equal(1, byFilename.GetProperty("total").GetInt32());
        var documents = await client.GetFromJsonAsync<JsonElement>("/api/admin/media?search=sample&type=documents");
        Assert.Equal(0, documents.GetProperty("total").GetInt32());
        await fixture.ExecuteAsync("INSERT INTO page_sections(media_id) VALUES(@id)", ("@id", id));
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/admin/media/{id}")).StatusCode);
        Assert.True(File.Exists(Path.Combine(fixture.MediaDirectory, relative.Replace('/', Path.DirectorySeparatorChar))));
        await fixture.ExecuteAsync("DELETE FROM page_sections WHERE media_id=@id", ("@id", id));
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/admin/media/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/media/{id}/file")).StatusCode);
    }

    [Fact]
    public async Task Invalid_batch_writes_nothing_and_admin_with_explicit_permission_can_browse()
    {
        var user = await fixture.SeedAsync(); await fixture.GrantRoleAsync(user, "Super Admin");
        using var factory = fixture.CreateFactory(); using var client = factory.Browser(); await Factory.LoginAsync(client);
        using var invalid = new MultipartFormDataContent();
        invalid.Add(new ByteArrayContent(Png), "files", "okay.png");
        invalid.Add(new ByteArrayContent("<script>"u8.ToArray()), "files", "bad.png");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/admin/media", invalid)).StatusCode);
        Assert.Equal(0L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM media")));
        var corrupt = (byte[])Png.Clone(); corrupt[45] ^= 1;
        using var brokenImage = Upload("broken.png", corrupt);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/admin/media", brokenImage)).StatusCode);
        Assert.Equal(0L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM media")));
        var admin = await fixture.SeedAsync("admin@example.test"); await fixture.GrantRoleAsync(admin, "Admin");
        await fixture.ExecuteAsync("INSERT INTO model_has_permissions(permission_id,model_type,model_id) SELECT id,@model,@id FROM permissions WHERE name='media.manage' AND guard_name='web'", ("@model", @"App\Models\User"), ("@id", admin.Id));
        using var other = factory.Browser(); await Factory.LoginAsync(other, "admin@example.test");
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync("/api/admin/media")).StatusCode);
    }

    [Fact]
    public async Task Multi_upload_filters_documents_and_missing_file_can_be_cleaned_up()
    {
        var actor = await fixture.SeedAsync(); await fixture.GrantRoleAsync(actor, "Super Admin");
        using var factory = fixture.CreateFactory(); using var client = factory.Browser(); await Factory.LoginAsync(client);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Png), "files", "first.png");
        form.Add(new ByteArrayContent("%PDF-1.4\n1 0 obj\n<<>>\nendobj\n%%EOF"u8.ToArray()), "files", "guide.pdf");
        var response = await client.PostAsync("/api/admin/media", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var document = await client.GetFromJsonAsync<JsonElement>("/api/admin/media?type=documents");
        Assert.Equal(1, document.GetProperty("total").GetInt32());
        var id = document.GetProperty("items")[0].GetProperty("id").GetString()!;
        var relative = (string)(await fixture.ScalarAsync("SELECT path FROM media WHERE id=@id", ("@id", id)))!;
        File.Delete(Path.Combine(fixture.MediaDirectory, relative.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/media/{id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/admin/media/{id}")).StatusCode);
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM media")));
    }

    [Fact]
    public async Task Unsafe_stored_path_is_never_served_or_deleted()
    {
        var actor = await fixture.SeedAsync(); await fixture.GrantRoleAsync(actor, "Super Admin");
        await fixture.ExecuteAsync("INSERT INTO media(original_name,disk,path,mime_type,size,created_at,updated_at) VALUES('bad.png','public','../outside.png','image/png',1,UTC_TIMESTAMP(),UTC_TIMESTAMP())");
        var id = Convert.ToUInt64(await fixture.ScalarAsync("SELECT id FROM media WHERE original_name='bad.png'"));
        using var factory = fixture.CreateFactory(); using var client = factory.Browser(); await Factory.LoginAsync(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/media/{id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/admin/media/{id}")).StatusCode);
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM media WHERE id=@id", ("@id", id))));
    }

    [Fact]
    public async Task Existing_laravel_public_record_is_served_and_upload_requires_csrf()
    {
        var actor = await fixture.SeedAsync(); await fixture.GrantRoleAsync(actor, "Super Admin");
        var relative = "media/2026/10/" + new string('a', 40) + ".png";
        var physical = Path.Combine(fixture.MediaDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(physical)!); await File.WriteAllBytesAsync(physical, Png);
        await fixture.ExecuteAsync("INSERT INTO media(original_name,disk,path,mime_type,size,width,height,created_at,updated_at) VALUES('laravel.png','public',@path,'image/png',@size,1,1,UTC_TIMESTAMP(),UTC_TIMESTAMP())", ("@path", relative), ("@size", Png.Length));
        var id = Convert.ToUInt64(await fixture.ScalarAsync("SELECT id FROM media WHERE original_name='laravel.png'"));
        using var factory = fixture.CreateFactory(); using var client = factory.Browser(); await Factory.LoginAsync(client);
        Assert.Equal(Png, await client.GetByteArrayAsync($"/api/media/{id}/file"));
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var form = Upload("new.png", Png);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/admin/media", form)).StatusCode);
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM media")));
    }

    [Fact]
    public async Task Valid_jpeg_and_webp_images_save_dimensions_and_mime_types()
    {
        var actor = await fixture.SeedAsync(); await fixture.GrantRoleAsync(actor, "Super Admin");
        using var factory = fixture.CreateFactory(); using var client = factory.Browser(); await Factory.LoginAsync(client);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Convert.FromBase64String("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAIDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDj6KKK4D78/9k=")), "files", "photo.jpeg");
        form.Add(new ByteArrayContent(Convert.FromBase64String("UklGRjgAAABXRUJQVlA4ICwAAACwAQCdASoCAAEAAUAmJaACdLoABDAAAP7x3I9fAFPVH/20P/1of/rQ/6UAAA==")), "files", "photo.webp");
        var response = await client.PostAsync("/api/admin/media", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal("image/jpeg", items[0].GetProperty("mimeType").GetString());
        Assert.Equal("image/webp", items[1].GetProperty("mimeType").GetString());
        Assert.All(items.EnumerateArray(), item => { Assert.Equal(2u, item.GetProperty("width").GetUInt32()); Assert.Equal(1u, item.GetProperty("height").GetUInt32()); });
    }


    [Fact]
    public async Task Count_and_image_size_limits_reject_before_storage()
    {
        IFormFile NewFile(long length) => new FormFile(new MemoryStream(Png), 0, length, "files", "photo.png");
        var tooMany = Enumerable.Range(0, 11).Select(_ => NewFile(Png.Length)).ToArray();
        var count = await MediaUploadValidator.ValidateAsync(tooMany, CancellationToken.None);
        Assert.False(count.IsValid);
        var oversize = await MediaUploadValidator.ValidateAsync([NewFile(MediaUploadValidator.MaximumImageBytes + 1)], CancellationToken.None);
        Assert.False(oversize.IsValid);
        Assert.Equal(0L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM media")));
    }

}

