using System.Buffers.Binary;

namespace Diwakar.Cms.Api.Media;

public sealed record ValidatedMediaFile(byte[] Bytes, string OriginalName, string Extension,
    string MimeType, long Size, uint? Width, uint? Height);

public sealed record MediaUploadValidation(IReadOnlyList<ValidatedMediaFile> Files, string? Error)
{
    public bool IsValid => Error is null;
}

public static class MediaUploadValidator
{
    public const int MaximumFiles = 10;
    public const long MaximumImageBytes = 5 * 1024 * 1024;
    public const long MaximumPdfBytes = 10 * 1024 * 1024;

    public static async Task<MediaUploadValidation> ValidateAsync(IReadOnlyList<IFormFile> files, CancellationToken ct)
    {
        if (files.Count is < 1 or > MaximumFiles)
            return Failed("Choose between 1 and 10 files.");
        var accepted = new List<ValidatedMediaFile>(files.Count);
        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var name = file.FileName.Replace('\\', '/').Split('/').Last().Trim();
            if (string.IsNullOrWhiteSpace(name) || name.EnumerateRunes().Count() > 255 || name.Any(char.IsControl))
                return Failed($"File {index + 1}: enter a filename of no more than 255 characters.");
            var extension = Path.GetExtension(name).ToLowerInvariant();
            var mime = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                _ => null
            };
            if (mime is null) return Failed($"File {index + 1}: only JPEG, PNG, WebP, and PDF files are allowed.");
            var maximum = mime == "application/pdf" ? MaximumPdfBytes : MaximumImageBytes;
            if (file.Length < 1 || file.Length > maximum)
                return Failed($"File {index + 1}: size must be within the {maximum / 1024 / 1024} MiB limit.");

            byte[] bytes;
            await using (var stream = file.OpenReadStream())
            await using (var buffer = new MemoryStream((int)file.Length))
            {
                await stream.CopyToAsync(buffer, ct);
                bytes = buffer.ToArray();
            }
            if (bytes.LongLength != file.Length)
                return Failed($"File {index + 1}: the upload is incomplete.");

            (uint Width, uint Height)? dimensions = mime switch
            {
                "image/jpeg" => JpegDimensions(bytes),
                "image/png" => PngDimensions(bytes),
                "image/webp" => WebpDimensions(bytes),
                _ => null
            };
            if (mime == "application/pdf" ? !LooksLikePdf(bytes) : dimensions is null)
                return Failed($"File {index + 1}: the file content does not match its extension or is incomplete.");
            accepted.Add(new ValidatedMediaFile(bytes, name, extension.TrimStart('.'), mime,
                file.Length, dimensions?.Width, dimensions?.Height));
        }
        return new MediaUploadValidation(accepted, null);
    }

    private static MediaUploadValidation Failed(string message) => new([], message);

    private static bool LooksLikePdf(ReadOnlySpan<byte> data)
    {
        if (data.Length < 10 || !data[..5].SequenceEqual("%PDF-"u8)) return false;
        return data[Math.Max(0, data.Length - 1024)..].IndexOf("%%EOF"u8) >= 0;
    }

    private static (uint Width, uint Height)? PngDimensions(ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (data.Length < 57 || !data[..8].SequenceEqual(signature)) return null;
        var offset = 8;
        var first = true;
        var hasImageData = false;
        uint width = 0, height = 0;
        while (offset <= data.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            if (length > data.Length - offset - 12) return null;
            var type = data.Slice(offset + 4, 4);
            var actualCrc = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 8 + (int)length, 4));
            if (actualCrc != PngCrc(data.Slice(offset + 4, 4 + (int)length))) return null;
            if (first)
            {
                if (!type.SequenceEqual("IHDR"u8) || length != 13) return null;
                width = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 8, 4));
                height = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 12, 4));
                if (width == 0 || height == 0) return null;
                first = false;
            }
            if (type.SequenceEqual("IDAT"u8) && length > 0) hasImageData = true;
            var next = offset + 12 + (int)length;
            if (type.SequenceEqual("IEND"u8))
                return length == 0 && hasImageData && next == data.Length ? (width, height) : null;
            offset = next;
        }
        return null;
    }

    private static uint PngCrc(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xffffffff;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0);
        }
        return ~crc;
    }

    private static (uint Width, uint Height)? JpegDimensions(ReadOnlySpan<byte> data)
    {
        if (data.Length < 20 || data[0] != 0xff || data[1] != 0xd8
            || data[^2] != 0xff || data[^1] != 0xd9) return null;
        var offset = 2;
        (uint Width, uint Height)? dimensions = null;
        while (offset < data.Length - 2)
        {
            if (data[offset++] != 0xff) return null;
            while (offset < data.Length && data[offset] == 0xff) offset++;
            if (offset >= data.Length) return null;
            var marker = data[offset++];
            if (marker == 0xda) return data.Length - offset >= 8 ? dimensions : null;
            if (marker is 0xd8 or 0xd9 or 0x01 or >= 0xd0 and <= 0xd7) continue;
            if (offset + 2 > data.Length) return null;
            var length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
            if (length < 2 || offset + length > data.Length) return null;
            if (marker is >= 0xc0 and <= 0xc3 or >= 0xc5 and <= 0xc7 or >= 0xc9 and <= 0xcb or >= 0xcd and <= 0xcf)
            {
                if (length < 7) return null;
                var height = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 3, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 5, 2));
                if (width == 0 || height == 0) return null;
                dimensions = (width, height);
            }
            offset += length;
        }
        return null;
    }

    private static (uint Width, uint Height)? WebpDimensions(ReadOnlySpan<byte> data)
    {
        if (data.Length < 30 || !data[..4].SequenceEqual("RIFF"u8)
            || !data.Slice(8, 4).SequenceEqual("WEBP"u8)
            || BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4)) != data.Length - 8) return null;
        var offset = 12;
        (uint Width, uint Height)? dimensions = null;
        var hasBitstream = false;
        while (offset <= data.Length - 8)
        {
            var chunk = data.Slice(offset, 4);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4, 4));
            if (length > data.Length - offset - 8) return null;
            var body = data.Slice(offset + 8, (int)length);
            if ((chunk.SequenceEqual("VP8 "u8) || chunk.SequenceEqual("VP8L"u8)) && body.Length > 0) hasBitstream = true;
            if (dimensions is null && chunk.SequenceEqual("VP8X"u8) && body.Length >= 10)
            {
                dimensions = (1u + Read24(body.Slice(4, 3)), 1u + Read24(body.Slice(7, 3)));
            }
            else if (dimensions is null && chunk.SequenceEqual("VP8L"u8) && body.Length >= 5 && body[0] == 0x2f)
            {
                dimensions = (1u + (uint)(body[1] | ((body[2] & 0x3f) << 8)),
                    1u + (uint)((body[2] >> 6) | (body[3] << 2) | ((body[4] & 0x0f) << 10)));
            }
            else if (dimensions is null && chunk.SequenceEqual("VP8 "u8) && body.Length >= 10
                     && body.Slice(3, 3).SequenceEqual(new byte[] { 0x9d, 0x01, 0x2a }))
            {
                dimensions = ((uint)(BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(6, 2)) & 0x3fff),
                    (uint)(BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(8, 2)) & 0x3fff));
            }
            offset += 8 + (int)length + ((int)length & 1);
            if (offset > data.Length) return null;
        }
        if (offset != data.Length || !hasBitstream || dimensions is not { Width: > 0, Height: > 0 }) return null;
        return dimensions;
    }

    private static uint Read24(ReadOnlySpan<byte> bytes) =>
        (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16));
}





