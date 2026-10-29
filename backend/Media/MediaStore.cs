using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace Diwakar.Cms.Api.Media;

public sealed record MediaItem(string Id, string OriginalName, string? Title, string MimeType, long Size,
    uint? Width, uint? Height, string CreatedAt, string Url);
public sealed record MediaPage(IReadOnlyList<MediaItem> Items, int Page, int TotalPages, long Total);
public sealed record MediaDeleteResult(string Status);

public sealed class MediaStore(MySqlDataSource source, IConfiguration configuration)
{
    public const int PageSize = 24;
    private static readonly Regex SafePath = new(@"\Amedia/[0-9]{4}/[0-9]{2}/[a-zA-Z0-9]{40}\.(jpg|jpeg|png|webp|pdf)\z", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Dictionary<string, string> MimeByExtension = new(StringComparer.OrdinalIgnoreCase)
    { ["jpg"] = "image/jpeg", ["jpeg"] = "image/jpeg", ["png"] = "image/png", ["webp"] = "image/webp", ["pdf"] = "application/pdf" };

    private string Root()
    {
        var configured = configuration["Media:PublicRoot"];
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured))
            throw new InvalidOperationException("Media public root is not configured.");
        var root = Path.GetFullPath(configured);
        if (!Directory.Exists(root)) throw new InvalidOperationException("Media public root does not exist.");
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Media public root must be a physical directory.");
        return root;
    }

    private string Resolve(string relative)
    {
        if (!SafePath.IsMatch(relative)) throw new InvalidOperationException("Unsafe media path.");
        var root = Root();
        var current = root;
        foreach (var part in relative.Split('/'))
        {
            current = Path.Combine(current, part);
            if ((Directory.Exists(current) || File.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Media path contains a link.");
        }
        var full = Path.GetFullPath(current);
        if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe media path.");
        return full;
    }

    private static bool ValidMime(string path, string mime) =>
        MimeByExtension.TryGetValue(Path.GetExtension(path).TrimStart('.'), out var expected) && expected == mime;

    private static MediaItem Read(MySqlDataReader reader) => new(
        reader.GetUInt64(0).ToString(), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.GetString(5), reader.GetInt64(6), reader.IsDBNull(7) ? null : reader.GetUInt32(7),
        reader.IsDBNull(8) ? null : reader.GetUInt32(8), reader.IsDBNull(9) ? "" : reader.GetDateTime(9).ToString("O"),
        "/api/media/" + reader.GetUInt64(0) + "/file");
    private const string Columns = "id, original_name, title, disk, path, mime_type, size, width, height, created_at";

    public async Task<MediaPage> ListAsync(int page, string? search, string? type, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 1000000);
        var term = search?.Trim() ?? "";
        if (term.Length > 255) throw new ArgumentException("Search is too long.");
        if (type is not (null or "all" or "images" or "documents")) throw new ArgumentException("Invalid media type.");
        var clause = " FROM media WHERE disk='public'";
        if (term.Length > 0) clause += " AND (original_name LIKE @search OR title LIKE @search)";
        if (type == "images") clause += " AND mime_type IN ('image/jpeg','image/png','image/webp')";
        if (type == "documents") clause += " AND mime_type='application/pdf'";
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*)" + clause;
        if (term.Length > 0) count.Parameters.AddWithValue("@search", "%" + term.Replace("%", "\\%").Replace("_", "\\_") + "%");
        var total = Convert.ToInt64(await count.ExecuteScalarAsync(ct));
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Min(page, pages);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns}{clause} ORDER BY created_at DESC, id DESC LIMIT @limit OFFSET @offset";
        if (term.Length > 0) command.Parameters.AddWithValue("@search", "%" + term.Replace("%", "\\%").Replace("_", "\\_") + "%");
        command.Parameters.AddWithValue("@limit", PageSize);
        command.Parameters.AddWithValue("@offset", (page - 1) * PageSize);
        var items = new List<MediaItem>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(Read(reader));
        return new(items, page, pages, total);
    }

    public async Task<IReadOnlyList<MediaItem>> UploadAsync(IReadOnlyList<ValidatedMediaFile> files, CancellationToken ct)
    {
        _ = Root();
        var written = new List<string>();
        var result = new List<MediaItem>();
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            foreach (var file in files)
            {
                var now = DateTime.UtcNow;
                var relative = $"media/{now.Year:D4}/{now.Month:D2}/{Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant()}.{file.Extension}";
                var full = Resolve(relative);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                written.Add(full);
                await using (var stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await stream.WriteAsync(file.Bytes, ct);
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO media(original_name,title,disk,path,mime_type,size,width,height,created_at,updated_at) VALUES(@name,NULL,'public',@path,@mime,@size,@width,@height,UTC_TIMESTAMP(),UTC_TIMESTAMP())";
                command.Parameters.AddWithValue("@name", file.OriginalName);
                command.Parameters.AddWithValue("@path", relative);
                command.Parameters.AddWithValue("@mime", file.MimeType);
                command.Parameters.AddWithValue("@size", file.Size);
                command.Parameters.AddWithValue("@width", (object?)file.Width ?? DBNull.Value);
                command.Parameters.AddWithValue("@height", (object?)file.Height ?? DBNull.Value);
                await command.ExecuteNonQueryAsync(ct);
                var id = Convert.ToUInt64(command.LastInsertedId);
                result.Add(new(id.ToString(), file.OriginalName, null, file.MimeType, file.Size,
                    file.Width, file.Height, now.ToString("O"), $"/api/media/{id}/file"));
            }
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            foreach (var path in written)
                if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }
    public async Task<MediaItem?> SetTitleAsync(ulong id, string? title, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE media SET title=@title, updated_at=UTC_TIMESTAMP() WHERE id=@id AND disk='public'";
        command.Parameters.AddWithValue("@title", (object?)title ?? DBNull.Value);
        command.Parameters.AddWithValue("@id", id);
        if (await command.ExecuteNonQueryAsync(ct) == 0) return null;
        await using var read = connection.CreateCommand();
        read.CommandText = $"SELECT {Columns} FROM media WHERE id=@id AND disk='public'";
        read.Parameters.AddWithValue("@id", id);
        await using var reader = await read.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }

    public async Task<(string Path, string Mime)?> FileAsync(ulong id, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT path,mime_type FROM media WHERE id=@id AND disk='public' LIMIT 1";
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var path = reader.GetString(0); var mime = reader.GetString(1);
        if (!SafePath.IsMatch(path) || !ValidMime(path, mime)) return null;
        return (Resolve(path), mime);
    }

    public async Task<MediaDeleteResult> DeleteAsync(ulong id, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT path,mime_type FROM media WHERE id=@id AND disk='public' FOR UPDATE";
        select.Parameters.AddWithValue("@id", id);
        string path, mime;
        await using (var reader = await select.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return new("missing");
            path = reader.GetString(0); mime = reader.GetString(1);
        }
        if (!SafePath.IsMatch(path) || !ValidMime(path, mime)) return new("unsafe");
        var full = Resolve(path);
        await using var reference = connection.CreateCommand();
        reference.Transaction = transaction;
        reference.CommandText = "SELECT 1 FROM page_sections WHERE media_id=@id LIMIT 1";
        reference.Parameters.AddWithValue("@id", id);
        if (await reference.ExecuteScalarAsync(ct) is not null) return new("referenced");
        if (File.Exists(full)) File.Delete(full);
        await using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM media WHERE id=@id AND disk='public'";
        delete.Parameters.AddWithValue("@id", id);
        await delete.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return new("deleted");
    }
}







