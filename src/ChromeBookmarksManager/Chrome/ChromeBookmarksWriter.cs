using System.Globalization;
using System.IO;
using System.Text;
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

    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

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
            ChromeBookmarksChecksum.Compute(
                document,
                cancellationToken);

        using var writer = new StreamWriter(
            destination,
            StrictUtf8,
            bufferSize: 128 * 1024,
            leaveOpen: true);

        WriteDocument(
            writer,
            document,
            checksums,
            cancellationToken);

        await writer
            .FlushAsync(cancellationToken)
            .ConfigureAwait(false);

        return checksums;
    }

    private static void WriteDocument(
        TextWriter writer,
        BookmarkDocument document,
        ChromeBookmarksChecksums checksums,
        CancellationToken cancellationToken)
    {
        writer.Write('{');
        var first = true;

        var propertyNames = new SortedSet<string>(
            StringComparer.Ordinal)
        {
            "checksum",
            "roots",
            "version"
        };

        if (document.ChecksumSha256 is not null)
        {
            propertyNames.Add("checksum_sha256");
        }

        AddExtensionPropertyNames(
            propertyNames,
            document.ExtensionData,
            TopLevelReservedProperties);

        foreach (var propertyName in propertyNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ChromeNativeJsonWriter.WritePropertyPrefix(
                writer,
                propertyName,
                depth: 1,
                ref first);

            switch (propertyName)
            {
                case "checksum":
                    ChromeNativeJsonWriter.WriteString(
                        writer,
                        checksums.Md5);
                    break;

                case "checksum_sha256":
                    ChromeNativeJsonWriter.WriteString(
                        writer,
                        checksums.Sha256);
                    break;

                case "roots":
                    WriteRoots(
                        writer,
                        document.Roots,
                        depth: 1,
                        cancellationToken);
                    break;

                case "version":
                    writer.Write(
                        document.Version.ToString(
                            CultureInfo.InvariantCulture));
                    break;

                default:
                    ChromeNativeJsonWriter.WriteJsonElement(
                        writer,
                        document.ExtensionData[propertyName],
                        depth: 1,
                        cancellationToken);
                    break;
            }
        }

        ChromeNativeJsonWriter.CloseObject(
            writer,
            depth: 0,
            hasProperties: !first);

        // Chromium's native Bookmarks file ends with CRLF.
        writer.Write("\r\n");
    }

    private static void WriteRoots(
        TextWriter writer,
        BookmarkRoots roots,
        int depth,
        CancellationToken cancellationToken)
    {
        writer.Write('{');
        var first = true;

        var propertyNames = new SortedSet<string>(
            StringComparer.Ordinal)
        {
            "bookmark_bar",
            "other",
            "synced"
        };

        AddExtensionPropertyNames(
            propertyNames,
            roots.ExtensionData,
            RootContainerReservedProperties);

        foreach (var propertyName in propertyNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ChromeNativeJsonWriter.WritePropertyPrefix(
                writer,
                propertyName,
                depth + 1,
                ref first);

            switch (propertyName)
            {
                case "bookmark_bar":
                    WriteNode(
                        writer,
                        roots.BookmarkBar,
                        depth + 1,
                        cancellationToken);
                    break;

                case "other":
                    WriteNode(
                        writer,
                        roots.Other,
                        depth + 1,
                        cancellationToken);
                    break;

                case "synced":
                    WriteNode(
                        writer,
                        roots.Synced,
                        depth + 1,
                        cancellationToken);
                    break;

                default:
                    ChromeNativeJsonWriter.WriteJsonElement(
                        writer,
                        roots.ExtensionData[propertyName],
                        depth + 1,
                        cancellationToken);
                    break;
            }
        }

        ChromeNativeJsonWriter.CloseObject(
            writer,
            depth,
            hasProperties: !first);
    }

    private static void WriteNode(
        TextWriter writer,
        BookmarkNode node,
        int depth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        writer.Write('{');
        var first = true;

        if (node.ExtensionData.Count == 0)
        {
            WriteKnownNodeProperties(
                writer,
                node,
                depth,
                ref first,
                cancellationToken);
        }
        else
        {
            WriteMergedNodeProperties(
                writer,
                node,
                depth,
                ref first,
                cancellationToken);
        }

        ChromeNativeJsonWriter.CloseObject(
            writer,
            depth,
            hasProperties: !first);
    }

    private static void WriteKnownNodeProperties(
        TextWriter writer,
        BookmarkNode node,
        int depth,
        ref bool first,
        CancellationToken cancellationToken)
    {
        if (node is BookmarkFolder folder)
        {
            WriteChildrenProperty(
                writer,
                folder,
                depth,
                ref first,
                cancellationToken);
        }

        WriteOptionalStringProperty(
            writer,
            "date_added",
            node.DateAddedRaw,
            depth,
            ref first);

        WriteOptionalStringProperty(
            writer,
            "date_last_used",
            node.DateLastUsedRaw,
            depth,
            ref first);

        WriteOptionalStringProperty(
            writer,
            "date_modified",
            node.DateModifiedRaw,
            depth,
            ref first);

        WriteStringProperty(
            writer,
            "guid",
            node.Guid.ToString("D"),
            depth,
            ref first);

        WriteStringProperty(
            writer,
            "id",
            node.Id,
            depth,
            ref first);

        if (node.MetaInfo is JsonElement metaInfo)
        {
            WriteJsonElementProperty(
                writer,
                "meta_info",
                metaInfo,
                depth,
                ref first,
                cancellationToken);
        }

        WriteStringProperty(
            writer,
            "name",
            node.Name,
            depth,
            ref first);

        WriteStringProperty(
            writer,
            "type",
            node is BookmarkFolder
                ? "folder"
                : "url",
            depth,
            ref first);

        if (node is BookmarkUrl bookmark)
        {
            WriteStringProperty(
                writer,
                "url",
                bookmark.Url,
                depth,
                ref first);
        }
    }

    private static void WriteMergedNodeProperties(
        TextWriter writer,
        BookmarkNode node,
        int depth,
        ref bool first,
        CancellationToken cancellationToken)
    {
        var propertyNames =
            new SortedSet<string>(StringComparer.Ordinal)
            {
                "guid",
                "id",
                "name",
                "type"
            };

        if (node is BookmarkFolder)
        {
            propertyNames.Add("children");
        }

        if (node is BookmarkUrl)
        {
            propertyNames.Add("url");
        }

        if (node.DateAddedRaw is not null)
        {
            propertyNames.Add("date_added");
        }

        if (node.DateLastUsedRaw is not null)
        {
            propertyNames.Add("date_last_used");
        }

        if (node.DateModifiedRaw is not null)
        {
            propertyNames.Add("date_modified");
        }

        if (node.MetaInfo is not null)
        {
            propertyNames.Add("meta_info");
        }

        AddExtensionPropertyNames(
            propertyNames,
            node.ExtensionData,
            NodeReservedProperties);

        foreach (var propertyName in propertyNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (propertyName)
            {
                case "children":
                    WriteChildrenProperty(
                        writer,
                        (BookmarkFolder)node,
                        depth,
                        ref first,
                        cancellationToken);
                    break;

                case "date_added":
                    WriteStringProperty(
                        writer,
                        propertyName,
                        node.DateAddedRaw!,
                        depth,
                        ref first);
                    break;

                case "date_last_used":
                    WriteStringProperty(
                        writer,
                        propertyName,
                        node.DateLastUsedRaw!,
                        depth,
                        ref first);
                    break;

                case "date_modified":
                    WriteStringProperty(
                        writer,
                        propertyName,
                        node.DateModifiedRaw!,
                        depth,
                        ref first);
                    break;

                case "guid":
                    WriteStringProperty(
                        writer,
                        propertyName,
                        node.Guid.ToString("D"),
                        depth,
                        ref first);
                    break;

                case "id":
                    WriteStringProperty(
                        writer,
                        propertyName,
                        node.Id,
                        depth,
                        ref first);
                    break;

                case "meta_info":
                    WriteJsonElementProperty(
                        writer,
                        propertyName,
                        node.MetaInfo!.Value,
                        depth,
                        ref first,
                        cancellationToken);
                    break;

                case "name":
                    WriteStringProperty(
                        writer,
                        propertyName,
                        node.Name,
                        depth,
                        ref first);
                    break;

                case "type":
                    WriteStringProperty(
                        writer,
                        propertyName,
                        node is BookmarkFolder
                            ? "folder"
                            : "url",
                        depth,
                        ref first);
                    break;

                case "url":
                    WriteStringProperty(
                        writer,
                        propertyName,
                        ((BookmarkUrl)node).Url,
                        depth,
                        ref first);
                    break;

                default:
                    WriteJsonElementProperty(
                        writer,
                        propertyName,
                        node.ExtensionData[propertyName],
                        depth,
                        ref first,
                        cancellationToken);
                    break;
            }
        }
    }

    private static void WriteChildrenProperty(
        TextWriter writer,
        BookmarkFolder folder,
        int depth,
        ref bool first,
        CancellationToken cancellationToken)
    {
        ChromeNativeJsonWriter.WritePropertyPrefix(
            writer,
            "children",
            depth + 1,
            ref first);

        writer.Write("[ ");

        var firstChild = true;
        foreach (var child in folder.Children)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!firstChild)
            {
                writer.Write(", ");
            }

            firstChild = false;

            // Chromium does not add an extra indentation level for the
            // array itself. The child object starts directly after "[ ".
            WriteNode(
                writer,
                child,
                depth + 1,
                cancellationToken);
        }

        writer.Write(" ]");
    }

    private static void WriteOptionalStringProperty(
        TextWriter writer,
        string propertyName,
        string? value,
        int depth,
        ref bool first)
    {
        if (value is null)
        {
            return;
        }

        WriteStringProperty(
            writer,
            propertyName,
            value,
            depth,
            ref first);
    }

    private static void WriteStringProperty(
        TextWriter writer,
        string propertyName,
        string value,
        int depth,
        ref bool first)
    {
        ChromeNativeJsonWriter.WritePropertyPrefix(
            writer,
            propertyName,
            depth + 1,
            ref first);

        ChromeNativeJsonWriter.WriteString(
            writer,
            value);
    }

    private static void WriteJsonElementProperty(
        TextWriter writer,
        string propertyName,
        JsonElement value,
        int depth,
        ref bool first,
        CancellationToken cancellationToken)
    {
        EnsureWritableJsonElement(
            value,
            propertyName);

        ChromeNativeJsonWriter.WritePropertyPrefix(
            writer,
            propertyName,
            depth + 1,
            ref first);

        ChromeNativeJsonWriter.WriteJsonElement(
            writer,
            value,
            depth + 1,
            cancellationToken);
    }

    private static void AddExtensionPropertyNames(
        SortedSet<string> destination,
        IReadOnlyDictionary<string, JsonElement> extensionData,
        HashSet<string> reservedProperties)
    {
        foreach (var pair in extensionData)
        {
            if (!reservedProperties.Contains(pair.Key))
            {
                destination.Add(pair.Key);
            }
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
