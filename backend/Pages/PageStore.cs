using System.Globalization;
using MySqlConnector;

namespace Diwakar.Cms.Api.Pages;

public sealed record PageItem(string Id, string Title, string Slug, string Status, string? CreatedAt, string? UpdatedAt);
public sealed record PageList(IReadOnlyList<PageItem> Items, int Page, int TotalPages, long Total);

public sealed class PageStore(MySqlDataSource source)
{
    public const int PageSize = 20;
    private const string Columns = "id,title,slug,status,created_at,updated_at";
    private static PageItem Read(MySqlDataReader reader) => new(
        reader.GetUInt64(0).ToString(CultureInfo.InvariantCulture), reader.GetString(1), reader.GetString(2),
        reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetDateTime(4).ToString("O", CultureInfo.InvariantCulture),
        reader.IsDBNull(5) ? null : reader.GetDateTime(5).ToString("O", CultureInfo.InvariantCulture));

    public async Task<PageList> ListAsync(int page, string? search, string? status, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 1000000);
        var term = search?.Trim() ?? "";
        if (term.Length > 200 || status is not (null or "" or "all" or "draft" or "published"))
            throw new ArgumentException("Invalid Page filters.");
        var clause = " FROM pages WHERE 1=1";
        if (term.Length > 0) clause += " AND (title LIKE @search OR slug LIKE @search)";
        if (status is "draft" or "published") clause += " AND status=@status";
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*)" + clause;
        BindFilters(count, term, status);
        var total = Convert.ToInt64(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Min(page, totalPages);
        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT " + Columns + clause + " ORDER BY updated_at DESC,id DESC LIMIT @limit OFFSET @offset";
        BindFilters(query, term, status);
        query.Parameters.AddWithValue("@limit", PageSize);
        query.Parameters.AddWithValue("@offset", (page - 1) * PageSize);
        var items = new List<PageItem>();
        await using var reader = await query.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(Read(reader));
        return new(items, page, totalPages, total);
    }

    private static void BindFilters(MySqlCommand command, string term, string? status)
    {
        if (term.Length > 0) command.Parameters.AddWithValue("@search", "%" + term.Replace("%", "\\%").Replace("_", "\\_") + "%");
        if (status is "draft" or "published") command.Parameters.AddWithValue("@status", status);
    }

    public async Task<PageItem?> GetAsync(ulong id, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        return await GetAsync(connection, id, ct);
    }

    private static async Task<PageItem?> GetAsync(MySqlConnection connection, ulong id, CancellationToken ct)
    {
        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT " + Columns + " FROM pages WHERE id=@id";
        query.Parameters.AddWithValue("@id", id);
        await using var reader = await query.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }

    public async Task<bool> SlugExistsAsync(string slug, ulong? except, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT 1 FROM pages WHERE slug=@slug" + (except is null ? "" : " AND id<>@id") + " LIMIT 1";
        query.Parameters.AddWithValue("@slug", slug);
        if (except is not null) query.Parameters.AddWithValue("@id", except.Value);
        return await query.ExecuteScalarAsync(ct) is not null;
    }

    public async Task<PageItem> CreateAsync(PageChange change, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var query = connection.CreateCommand();
        query.CommandText = "INSERT INTO pages(title,slug,status,created_at,updated_at) VALUES(@title,@slug,@status,UTC_TIMESTAMP(),UTC_TIMESTAMP())";
        query.Parameters.AddWithValue("@title", change.Title!);
        query.Parameters.AddWithValue("@slug", change.Slug!);
        query.Parameters.AddWithValue("@status", change.Status!);
        await query.ExecuteNonQueryAsync(ct);
        return (await GetAsync(connection, (ulong)query.LastInsertedId, ct))!;
    }

    public async Task<PageItem?> UpdateAsync(ulong id, PageChange change, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        var assignments = new List<string>();
        await using var query = connection.CreateCommand();
        if (change.Title is not null) { assignments.Add("title=@title"); query.Parameters.AddWithValue("@title", change.Title); }
        if (change.Slug is not null) { assignments.Add("slug=@slug"); query.Parameters.AddWithValue("@slug", change.Slug); }
        if (change.Status is not null) { assignments.Add("status=@status"); query.Parameters.AddWithValue("@status", change.Status); }
        if (assignments.Count > 0)
        {
            assignments.Add("updated_at=UTC_TIMESTAMP()");
            query.CommandText = "UPDATE pages SET " + string.Join(",", assignments) + " WHERE id=@id";
            query.Parameters.AddWithValue("@id", id);
            await query.ExecuteNonQueryAsync(ct);
        }
        return await GetAsync(connection, id, ct);
    }

    public async Task<string> DeleteAsync(ulong id, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var find = connection.CreateCommand();
        find.Transaction = transaction;
        find.CommandText = "SELECT id FROM pages WHERE id=@id FOR UPDATE";
        find.Parameters.AddWithValue("@id", id);
        if (await find.ExecuteScalarAsync(ct) is null) return "missing";
        await using var referenced = connection.CreateCommand();
        referenced.Transaction = transaction;
        referenced.CommandText = "SELECT 1 FROM page_sections WHERE page_id=@id LIMIT 1";
        referenced.Parameters.AddWithValue("@id", id);
        if (await referenced.ExecuteScalarAsync(ct) is not null) return "referenced";
        await using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM pages WHERE id=@id";
        delete.Parameters.AddWithValue("@id", id);
        try { await delete.ExecuteNonQueryAsync(ct); }
        catch (MySqlException error) when (error.Number == 1451) { return "referenced"; }
        await transaction.CommitAsync(ct);
        return "deleted";
    }
}
