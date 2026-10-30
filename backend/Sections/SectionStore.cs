using System.Globalization;
using Diwakar.Cms.Api.Pages;
using MySqlConnector;

namespace Diwakar.Cms.Api.Sections;

public sealed record SectionItem(string Id, string PageId, string Type, uint SortOrder, bool IsEnabled,
    string Heading, string? Body, string? MediaId, string? ImageAlt, string? ButtonLabel, string? ButtonUrl,
    string? MediaName, string? MediaUrl);
public sealed record SectionList(PageItem Page, IReadOnlyList<SectionItem> Sections);

public sealed class SectionStore(MySqlDataSource source, PageStore pages)
{
    private const string Columns = "s.id,s.page_id,s.type,s.sort_order,s.is_enabled,s.heading,s.body,s.media_id,s.image_alt,s.button_label,s.button_url,COALESCE(m.title,m.original_name)";
    private const string From = " FROM page_sections s LEFT JOIN media m ON m.id=s.media_id";
    private static SectionItem Read(MySqlDataReader r) => new(
        r.GetUInt64(0).ToString(CultureInfo.InvariantCulture), r.GetUInt64(1).ToString(CultureInfo.InvariantCulture),
        r.GetString(2), r.GetUInt32(3), r.GetBoolean(4), r.GetString(5),
        r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetUInt64(7).ToString(CultureInfo.InvariantCulture),
        r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9),
        r.IsDBNull(10) ? null : r.GetString(10), r.IsDBNull(11) ? null : r.GetString(11),
        r.IsDBNull(7) ? null : "/api/media/" + r.GetUInt64(7).ToString(CultureInfo.InvariantCulture) + "/file");

    public async Task<SectionList?> ListAsync(ulong pageId, CancellationToken ct)
    {
        var page = await pages.GetAsync(pageId, ct);
        if (page is null) return null;
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT " + Columns + From + " WHERE s.page_id=@page ORDER BY s.sort_order,s.id";
        command.Parameters.AddWithValue("@page", pageId);
        var items = new List<SectionItem>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(Read(reader));
        return new(page, items);
    }

    public async Task<SectionItem?> GetAsync(ulong pageId, ulong id, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        return await GetAsync(connection, null, pageId, id, ct);
    }

    private static async Task<SectionItem?> GetAsync(MySqlConnection connection, MySqlTransaction? tx,
        ulong pageId, ulong id, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT " + Columns + From + " WHERE s.page_id=@page AND s.id=@id";
        command.Parameters.AddWithValue("@page", pageId);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }

    private static async Task<bool> LockPage(MySqlConnection connection, MySqlTransaction tx, ulong pageId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT id FROM pages WHERE id=@page FOR UPDATE";
        command.Parameters.AddWithValue("@page", pageId);
        return await command.ExecuteScalarAsync(ct) is not null;
    }

    private static async Task<bool> ValidImage(MySqlConnection connection, MySqlTransaction tx, ulong? mediaId, CancellationToken ct)
    {
        if (mediaId is null) return true;
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT id FROM media WHERE id=@media AND disk='public' AND mime_type IN ('image/jpeg','image/png','image/webp') FOR UPDATE";
        command.Parameters.AddWithValue("@media", mediaId.Value);
        return await command.ExecuteScalarAsync(ct) is not null;
    }

    private static async Task<List<ulong>> OrderedIds(MySqlConnection connection, MySqlTransaction tx, ulong pageId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT id FROM page_sections WHERE page_id=@page ORDER BY sort_order,id FOR UPDATE";
        command.Parameters.AddWithValue("@page", pageId);
        var ids = new List<ulong>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) ids.Add(reader.GetUInt64(0));
        return ids;
    }

    private static async Task Reorder(MySqlConnection connection, MySqlTransaction tx, IReadOnlyList<ulong> ids, CancellationToken ct)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = tx;
            command.CommandText = "UPDATE page_sections SET sort_order=@position WHERE id=@id";
            command.Parameters.AddWithValue("@position", i + 1);
            command.Parameters.AddWithValue("@id", ids[i]);
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<(string Status, SectionItem? Item)> CreateAsync(ulong pageId, SectionChange change, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        if (!await LockPage(connection, tx, pageId, ct)) return ("missing", null);
        if (!await ValidImage(connection, tx, change.MediaId, ct)) return ("invalid-media", null);
        var ids = await OrderedIds(connection, tx, pageId, ct);
        await Reorder(connection, tx, ids, ct);
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = @"INSERT INTO page_sections(page_id,type,sort_order,is_enabled,heading,body,media_id,image_alt,button_label,button_url,created_at,updated_at)
VALUES(@page,@type,@position,@enabled,@heading,@body,@media,@alt,@label,@url,UTC_TIMESTAMP(),UTC_TIMESTAMP())";
        Bind(command, pageId, change);
        command.Parameters.AddWithValue("@position", ids.Count + 1);
        await command.ExecuteNonQueryAsync(ct);
        var item = await GetAsync(connection, tx, pageId, (ulong)command.LastInsertedId, ct);
        await tx.CommitAsync(ct);
        return ("created", item);
    }

    private static void Bind(MySqlCommand command, ulong pageId, SectionChange change)
    {
        command.Parameters.AddWithValue("@page", pageId);
        command.Parameters.AddWithValue("@type", change.Type);
        command.Parameters.AddWithValue("@enabled", change.IsEnabled);
        command.Parameters.AddWithValue("@heading", change.Heading);
        command.Parameters.AddWithValue("@body", (object?)change.Body ?? DBNull.Value);
        command.Parameters.AddWithValue("@media", (object?)change.MediaId ?? DBNull.Value);
        command.Parameters.AddWithValue("@alt", (object?)change.ImageAlt ?? DBNull.Value);
        command.Parameters.AddWithValue("@label", (object?)change.ButtonLabel ?? DBNull.Value);
        command.Parameters.AddWithValue("@url", (object?)change.ButtonUrl ?? DBNull.Value);
    }

    public async Task<(string Status, SectionItem? Item)> UpdateAsync(ulong pageId, ulong id, SectionChange change, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        if (!await LockPage(connection, tx, pageId, ct)) return ("missing", null);
        if (await GetAsync(connection, tx, pageId, id, ct) is null) return ("missing", null);
        if (!await ValidImage(connection, tx, change.MediaId, ct)) return ("invalid-media", null);
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = @"UPDATE page_sections SET is_enabled=@enabled,heading=@heading,body=@body,media_id=@media,
image_alt=@alt,button_label=@label,button_url=@url,updated_at=UTC_TIMESTAMP() WHERE page_id=@page AND id=@id";
        Bind(command, pageId, change);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(ct);
        var item = await GetAsync(connection, tx, pageId, id, ct);
        await tx.CommitAsync(ct);
        return ("updated", item);
    }

    public async Task<string> MoveAsync(ulong pageId, ulong id, string direction, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        if (!await LockPage(connection, tx, pageId, ct)) return "missing";
        var ids = await OrderedIds(connection, tx, pageId, ct);
        var index = ids.IndexOf(id);
        if (index < 0) return "missing";
        var other = index + (direction == "up" ? -1 : 1);
        if (other < 0 || other >= ids.Count) return "boundary";
        (ids[index], ids[other]) = (ids[other], ids[index]);
        await Reorder(connection, tx, ids, ct);
        await tx.CommitAsync(ct);
        return "moved";
    }

    public async Task<string> ToggleAsync(ulong pageId, ulong id, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        if (!await LockPage(connection, tx, pageId, ct)) return "missing";
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "UPDATE page_sections SET is_enabled=NOT is_enabled,updated_at=UTC_TIMESTAMP() WHERE page_id=@page AND id=@id";
        command.Parameters.AddWithValue("@page", pageId);
        command.Parameters.AddWithValue("@id", id);
        if (await command.ExecuteNonQueryAsync(ct) == 0) return "missing";
        await tx.CommitAsync(ct);
        return "updated";
    }

    public async Task<string> DeleteAsync(ulong pageId, ulong id, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        if (!await LockPage(connection, tx, pageId, ct)) return "missing";
        var ids = await OrderedIds(connection, tx, pageId, ct);
        if (!ids.Remove(id)) return "missing";
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "DELETE FROM page_sections WHERE page_id=@page AND id=@id";
        command.Parameters.AddWithValue("@page", pageId);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(ct);
        await Reorder(connection, tx, ids, ct);
        await tx.CommitAsync(ct);
        return "deleted";
    }
}
