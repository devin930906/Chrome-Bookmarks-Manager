using System.IO;
using System.Globalization;
using System.Text.Json;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Chrome;

public sealed class ChromeBookmarksWriter : IChromeBookmarksWriter
{
    private static readonly HashSet<string> TopLevelReservedProperties =
        new(StringComparer.Ordinal)
        {
            "version",
            "checksum",
            "checksum_sha256",
            "roots"
        };

    private static readonly HashSet<string> RootContainerReservedProperties =
        new(StringComparer.Ordinal)
        {
            "bookmark_bar",
            "other",
            "synced"
        };

    private static readonly HashSet<string> NodeReservedProperties =
        new(StringComparer.Ordinal)
        {
            "id",
            "guid",
            "name",
            "type",
            "url",
            "children",
            "date_added",
            "date_modified",
            "date_last_used",
            "meta_info"
        };

    public async Task<ChromeBookmarksChecksums> WriteAsync(
        BookmarkDocument document,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();

        if (!destination.CanWrite)
        {
            throw new ArgumentException(
                "The destination stream must be writable.",
                nameof(destination));
        }

        ValidateDocument(document, cancellationToken);
        var checksums =
            ChromeBookmarksChecksum.Compute(document, cancellationToken);

        using var jsonWriter = new Utf8JsonWriter(
            destination,
            new JsonWriterOptions
            {
                Indented = true,
                SkipValidation = false
            });

        jsonWriter.WriteStartObject();

        jsonWriter.WriteNumber("version", document.Version);
        jsonWriter.WriteString("checksum", checksums.Md5);
        jsonWriter.WriteString("checksum_sha256", checksums.Sha256);

        jsonWriter.WritePropertyName("roots");
        jsonWriter.WriteStartObject();

        jsonWriter.WritePropertyName("bookmark_bar");
        WriteNode(
            jsonWriter,
            document.Roots.BookmarkBar,
            cancellationToken);

        jsonWriter.WritePropertyName("other");
        WriteNode(
            jsonWriter,
            document.Roots.Other,
            cancellationToken);

        jsonWriter.WritePropertyName("synced");
        WriteNode(
            jsonWriter,
            document.Roots.Synced,
            cancellationToken);

        WriteExtensionData(
            jsonWriter,
            document.Roots.ExtensionData,
            RootContainerReservedProperties,
            cancellationToken);
        jsonWriter.WriteEndObject();

        WriteExtensionData(
            jsonWriter,
            document.ExtensionData,
            TopLevelReservedProperties,
            cancellationToken);

        jsonWriter.WriteEndObject();

        await jsonWriter
            .FlushAsync(cancellationToken)
            .ConfigureAwait(false);

        return checksums;
    }

    private static void WriteNode(
        Utf8JsonWriter writer,
        BookmarkNode node,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.WriteStartObject();
        writer.WriteString("id", node.Id);
        writer.WriteString("name", node.Name);
        writer.WriteString("guid", node.Guid.ToString("D"));

        WriteOptionalString(
            writer,
            "date_added",
            node.DateAddedRaw);
        WriteOptionalString(
            writer,
            "date_modified",
            node.DateModifiedRaw);
        WriteOptionalString(
            writer,
            "date_last_used",
            node.DateLastUsedRaw);

        switch (node)
        {
            case BookmarkUrl bookmark:
                writer.WriteString("type", "url");
                writer.WriteString("url", bookmark.Url);
                break;

            case BookmarkFolder folder:
                writer.WriteString("type", "folder");
                writer.WritePropertyName("children");
                writer.WriteStartArray();
                foreach (var child in folder.Children)
                {
                    WriteNode(writer, child, cancellationToken);
                }

                writer.WriteEndArray();
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported bookmark node type {node.GetType().FullName}.");
        }

        if (node.MetaInfo is JsonElement metaInfo)
        {
            EnsureWritableJsonElement(metaInfo, "meta_info");
            writer.WritePropertyName("meta_info");
            metaInfo.WriteTo(writer);
        }

        WriteExtensionData(
            writer,
            node.ExtensionData,
            NodeReservedProperties,
            cancellationToken);

        writer.WriteEndObject();
    }

    private static void WriteOptionalString(
        Utf8JsonWriter writer,
        string propertyName,
        string? value)
    {
        if (value is not null)
        {
            writer.WriteString(propertyName, value);
        }
    }

    private static void WriteExtensionData(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, JsonElement> extensionData,
        HashSet<string> reservedProperties,
        CancellationToken cancellationToken)
    {
        foreach (var pair in extensionData.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reservedProperties.Contains(pair.Key))
            {
                continue;
            }

            EnsureWritableJsonElement(pair.Value, pair.Key);
            writer.WritePropertyName(pair.Key);
            pair.Value.WriteTo(writer);
        }
    }

    private static void EnsureWritableJsonElement(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException(
                $"Chrome bookmark property '{propertyName}' has an undefined JSON value.");
        }
    }

    private static void ValidateDocument(
        BookmarkDocument document,
        CancellationToken cancellationToken)
    {
        if (document.Version != 1)
        {
            throw new NotSupportedException(
                $"Chrome bookmark format version {document.Version} is not supported for writing.");
        }

        if (ReferenceEquals(
                document.Roots.BookmarkBar,
                document.Roots.Other) ||
            ReferenceEquals(
                document.Roots.BookmarkBar,
                document.Roots.Synced) ||
            ReferenceEquals(
                document.Roots.Other,
                document.Roots.Synced))
        {
            throw new InvalidOperationException(
                "The three permanent Chrome bookmark roots must be distinct.");
        }

        var ids = new HashSet<long>();
        var guids = new HashSet<Guid>();
        var folderCount = 0;
        var urlCount = 0;

        ValidateRoot(
            document.Roots.BookmarkBar,
            ids,
            guids,
            ref folderCount,
            ref urlCount,
            cancellationToken);
        ValidateRoot(
            document.Roots.Other,
            ids,
            guids,
            ref folderCount,
            ref urlCount,
            cancellationToken);
        ValidateRoot(
            document.Roots.Synced,
            ids,
            guids,
            ref folderCount,
            ref urlCount,
            cancellationToken);

        if (folderCount != document.FolderCount ||
            urlCount != document.UrlCount)
        {
            throw new InvalidOperationException(
                "Bookmark document node counts do not match the current graph.");
        }
    }

    private static void ValidateRoot(
        BookmarkFolder root,
        HashSet<long> ids,
        HashSet<Guid> guids,
        ref int folderCount,
        ref int urlCount,
        CancellationToken cancellationToken)
    {
        if (root.Parent is not null)
        {
            throw new InvalidOperationException(
                "A permanent Chrome bookmark root cannot have a parent.");
        }

        ValidateNode(
            root,
            expectedParent: null,
            depth: 0,
            ids,
            guids,
            ref folderCount,
            ref urlCount,
            cancellationToken);
    }

    private static void ValidateNode(
        BookmarkNode node,
        BookmarkFolder? expectedParent,
        int depth,
        HashSet<long> ids,
        HashSet<Guid> guids,
        ref int folderCount,
        ref int urlCount,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (depth > 63)
        {
            throw new InvalidOperationException(
                "Bookmark nesting exceeds the supported 64-level reader boundary.");
        }

        if (!ReferenceEquals(node.Parent, expectedParent))
        {
            throw new InvalidOperationException(
                $"Bookmark node {node.Id} has an inconsistent parent relationship.");
        }

        if (!long.TryParse(
                node.Id,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var numericId) ||
            numericId <= 0 ||
            !ids.Add(numericId))
        {
            throw new InvalidOperationException(
                $"Bookmark node ID '{node.Id}' is invalid or duplicated.");
        }

        if (node.Guid == Guid.Empty ||
            !guids.Add(node.Guid))
        {
            throw new InvalidOperationException(
                $"Bookmark node GUID '{node.Guid:D}' is invalid or duplicated.");
        }

        switch (node)
        {
            case BookmarkUrl bookmark:
                if (string.IsNullOrEmpty(bookmark.Url))
                {
                    throw new InvalidOperationException(
                        $"Bookmark URL node {node.Id} has no URL.");
                }

                urlCount = checked(urlCount + 1);
                break;

            case BookmarkFolder folder:
                folderCount = checked(folderCount + 1);
                foreach (var child in folder.Children)
                {
                    ValidateNode(
                        child,
                        folder,
                        depth + 1,
                        ids,
                        guids,
                        ref folderCount,
                        ref urlCount,
                        cancellationToken);
                }

                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported bookmark node type {node.GetType().FullName}.");
        }
    }
}
