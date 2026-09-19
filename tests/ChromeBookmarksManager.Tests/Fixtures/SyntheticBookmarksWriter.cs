using System.Buffers.Binary;
using System.Text.Json;

namespace ChromeBookmarksManager.Tests.Fixtures;

internal static class SyntheticBookmarksWriter
{
    public static async Task WriteAsync(
        string path,
        int urlCount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(urlCount);

        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteNumber("version", 1);
        writer.WriteString("checksum", "synthetic-not-a-chrome-checksum");
        writer.WriteString(
            "checksum_sha256",
            "synthetic-not-a-chrome-sha256-checksum");
        writer.WriteStartObject("roots");

        WriteFolderStart(
            writer,
            "1",
            "11111111-1111-4111-8111-111111111111",
            "Bookmarks bar");
        for (var index = 0; index < urlCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writer.WriteStartObject();
            writer.WriteString(
                "id",
                (index + 4L).ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteString(
                "guid",
                CreateGuid(index + 1000).ToString("D"));
            writer.WriteString("name", $"Synthetic Bookmark {index}");
            writer.WriteString("type", "url");
            writer.WriteString(
                "url",
                $"https://example.com/bookmark/{index}");
            writer.WriteString("date_added", "13370000000000000");
            writer.WriteEndObject();

            if ((index & 4095) == 4095)
            {
                await writer.FlushAsync(cancellationToken);
            }
        }

        WriteFolderEnd(writer);

        WriteFolderStart(
            writer,
            "2",
            "22222222-2222-4222-8222-222222222222",
            "Other bookmarks");
        WriteFolderEnd(writer);

        WriteFolderStart(
            writer,
            "3",
            "33333333-3333-4333-8333-333333333333",
            "Mobile bookmarks");
        WriteFolderEnd(writer);

        writer.WriteEndObject();
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);
    }

    private static void WriteFolderStart(
        Utf8JsonWriter writer,
        string id,
        string guid,
        string name)
    {
        var propertyName = id switch
        {
            "1" => "bookmark_bar",
            "2" => "other",
            "3" => "synced",
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };

        writer.WriteStartObject(propertyName);
        writer.WriteString("id", id);
        writer.WriteString("guid", guid);
        writer.WriteString("name", name);
        writer.WriteString("type", "folder");
        writer.WriteStartArray("children");
    }

    private static void WriteFolderEnd(Utf8JsonWriter writer)
    {
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static Guid CreateGuid(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        bytes[7] = 0x40;
        bytes[8] = 0x80;
        return new Guid(bytes);
    }
}
