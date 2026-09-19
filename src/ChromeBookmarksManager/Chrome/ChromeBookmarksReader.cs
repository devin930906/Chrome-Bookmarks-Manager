using System.IO;
using System.Text.Json;
using ChromeBookmarksManager.Chrome.Serialization;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Chrome;

public sealed partial class ChromeBookmarksReader : IChromeBookmarksReader
{
    public async Task<BookmarkDocument> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException exception)
        {
            throw new ChromeBookmarksReadException(ChromeBookmarksReadError.FileNotFound, "The selected Bookmarks file no longer exists.", innerException: exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new ChromeBookmarksReadException(ChromeBookmarksReadError.FileNotFound, "The selected Bookmarks file no longer exists.", innerException: exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ChromeBookmarksReadException(ChromeBookmarksReadError.AccessDenied, "Windows denied access to the selected Bookmarks file.", innerException: exception);
        }
        catch (IOException exception)
        {
            throw new ChromeBookmarksReadException(ChromeBookmarksReadError.IoFailure, "The selected Bookmarks file could not be read.", innerException: exception);
        }
    }

    internal Task<BookmarkDocument> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        return ReadCoreAsync(stream, cancellationToken);
    }

    private static async Task<BookmarkDocument> ReadCoreAsync(Stream stream, CancellationToken cancellationToken)
    {
        ChromeBookmarkFileDto? dto;

        try
        {
            dto = await JsonSerializer.DeserializeAsync<ChromeBookmarkFileDto>(
                stream,
                ChromeBookmarksJson.ReaderOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            var depth = exception.Message.Contains("maximum depth", StringComparison.OrdinalIgnoreCase);
            throw new ChromeBookmarksReadException(
                depth ? ChromeBookmarksReadError.DepthLimitExceeded : ChromeBookmarksReadError.MalformedJson,
                "The selected file is not a valid supported Chrome Bookmarks JSON document.",
                exception.Path,
                exception);
        }

        if (dto?.Version is not 1)
        {
            throw new ChromeBookmarksReadException(
                ChromeBookmarksReadError.UnsupportedVersion,
                "Only Chrome bookmark format version 1 is supported.",
                "$.version");
        }

        var roots = dto.Roots ?? throw Missing("$.roots");
        var context = new MappingContext(cancellationToken);
        var bookmarkBar = MapRequiredRoot(roots.BookmarkBar, "$.roots.bookmark_bar", context);
        var other = MapRequiredRoot(roots.Other, "$.roots.other", context);
        var synced = MapRequiredRoot(roots.Synced, "$.roots.synced", context);

        return new BookmarkDocument(
            dto.Version.Value,
            dto.Checksum,
            dto.ChecksumSha256,
            new BookmarkRoots(bookmarkBar, other, synced, Freeze(roots.ExtensionData)),
            Freeze(dto.ExtensionData));
    }

    private static IReadOnlyDictionary<string, JsonElement> Freeze(Dictionary<string, JsonElement>? values)
    {
        if (values is null || values.Count == 0)
        {
            return new Dictionary<string, JsonElement>();
        }

        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, JsonElement>(
            values.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal));
    }

    private static ChromeBookmarksReadException Missing(string path) =>
        new(
            ChromeBookmarksReadError.MissingProperty,
            $"Required Chrome bookmark property is missing at {path}.",
            path);

    private sealed class MappingContext(CancellationToken cancellationToken)
    {
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public HashSet<long> Ids { get; } = new();
        public HashSet<Guid> Guids { get; } = new();
    }

    private static BookmarkFolder MapRequiredRoot(ChromeBookmarkNodeDto? dto, string path, MappingContext context) =>
        MapNode(dto ?? throw Missing(path), path, context, 0) as BookmarkFolder
        ?? throw new ChromeBookmarksReadException(
            ChromeBookmarksReadError.InvalidNodeType,
            $"Chrome bookmark root must be a folder at {path}.",
            path);

    private static BookmarkNode MapNode(ChromeBookmarkNodeDto dto, string path, MappingContext context, int depth)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (dto.Type != "folder")
        {
            throw new ChromeBookmarksReadException(
                ChromeBookmarksReadError.InvalidNodeType,
                $"Expected folder at {path}.",
                $"{path}.type");
        }

        var id = ValidateId(dto.Id, path, context);
        var guid = ValidateGuid(dto.Guid, path, context);
        var name = dto.Name ?? throw Missing($"{path}.name");
        var children = dto.Children ?? throw Missing($"{path}.children");

        if (children.Count != 0)
        {
            throw new ChromeBookmarksReadException(
                ChromeBookmarksReadError.InvalidPropertyType,
                $"Nested nodes are added in the next reader task at {path}.",
                $"{path}.children");
        }

        return new BookmarkFolder(
            id,
            guid,
            name,
            dto.DateAdded,
            dto.DateModified,
            dto.DateLastUsed,
            dto.MetaInfo?.Clone(),
            Freeze(dto.ExtensionData),
            Array.Empty<BookmarkNode>());
    }

    private static string ValidateId(string? raw, string path, MappingContext context)
    {
        if (raw is null ||
            !long.TryParse(
                raw,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var numeric) ||
            numeric <= 0)
        {
            throw new ChromeBookmarksReadException(
                ChromeBookmarksReadError.InvalidId,
                $"Bookmark ID must be a positive decimal string at {path}.",
                $"{path}.id");
        }

        if (!context.Ids.Add(numeric))
        {
            throw new ChromeBookmarksReadException(
                ChromeBookmarksReadError.DuplicateId,
                $"Duplicate bookmark ID {raw} at {path}.",
                $"{path}.id");
        }

        return raw;
    }

    private static Guid ValidateGuid(string? raw, string path, MappingContext context)
    {
        if (raw is null || !Guid.TryParseExact(raw, "D", out var guid) || guid == Guid.Empty)
        {
            throw new ChromeBookmarksReadException(
                ChromeBookmarksReadError.InvalidGuid,
                $"Bookmark GUID is invalid at {path}.",
                $"{path}.guid");
        }

        if (!context.Guids.Add(guid))
        {
            throw new ChromeBookmarksReadException(
                ChromeBookmarksReadError.DuplicateGuid,
                $"Duplicate bookmark GUID {raw} at {path}.",
                $"{path}.guid");
        }

        return guid;
    }
}
