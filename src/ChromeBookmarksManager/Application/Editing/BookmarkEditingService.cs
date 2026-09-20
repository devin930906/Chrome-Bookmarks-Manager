using System.Collections.ObjectModel;
using System.Text.Json;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Editing;

public sealed class BookmarkEditingService : IBookmarkEditingService
{
    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyExtensionData =
            new ReadOnlyDictionary<string, JsonElement>(
                new Dictionary<string, JsonElement>(StringComparer.Ordinal));

    private readonly TimeProvider _timeProvider;

    public BookmarkEditingService()
        : this(TimeProvider.System)
    {
    }

    public BookmarkEditingService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public bool RenameNode(BookmarkDocument document, BookmarkNode node, string newName)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(node);

        EnsureBelongsToDocument(document, node);

        if (IsPermanentRoot(document, node))
        {
            throw new BookmarkEditException(
                BookmarkEditError.ProtectedRoot,
                "Permanent Chrome bookmark roots cannot be renamed.");
        }

        if (newName is null)
        {
            throw InvalidValue("Bookmark name cannot be null.");
        }

        return node.SetName(newName);
    }

    public bool EditUrl(BookmarkDocument document, BookmarkUrl bookmark, string newUrl)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(bookmark);

        EnsureBelongsToDocument(document, bookmark);

        if (string.IsNullOrWhiteSpace(newUrl))
        {
            throw InvalidValue("Bookmark URL cannot be empty or whitespace.");
        }

        return bookmark.SetUrl(newUrl);
    }

    public BookmarkUrl AddBookmark(
        BookmarkDocument document,
        BookmarkFolder parent,
        string name,
        string url)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(parent);

        EnsureBelongsToDocument(document, parent);

        if (name is null)
        {
            throw InvalidValue("Bookmark name cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            throw InvalidValue("Bookmark URL cannot be empty or whitespace.");
        }

        var nowRaw = ChromeBookmarkTime.ToRaw(_timeProvider.GetUtcNow());
        var bookmark = new BookmarkUrl(
            document.AllocateNextNodeId(),
            document.AllocateUniqueGuid(),
            BookmarkNode.SanitizeTitleForChromium(name),
            url,
            nowRaw,
            null,
            "0",
            null,
            EmptyExtensionData);

        parent.AddChild(bookmark);
        document.RecordAddedNode(bookmark);
        return bookmark;
    }

    public BookmarkFolder AddFolder(
        BookmarkDocument document,
        BookmarkFolder parent,
        string name)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(parent);

        EnsureBelongsToDocument(document, parent);

        if (name is null)
        {
            throw InvalidValue("Folder name cannot be null.");
        }

        var nowRaw = ChromeBookmarkTime.ToRaw(_timeProvider.GetUtcNow());
        var folder = new BookmarkFolder(
            document.AllocateNextNodeId(),
            document.AllocateUniqueGuid(),
            BookmarkNode.SanitizeTitleForChromium(name),
            nowRaw,
            nowRaw,
            "0",
            null,
            EmptyExtensionData,
            Array.Empty<BookmarkNode>());

        parent.AddChild(folder);
        document.RecordAddedNode(folder);
        return folder;
    }

    private static BookmarkEditException InvalidValue(string message) =>
        new(BookmarkEditError.InvalidValue, message);

    private static bool IsPermanentRoot(BookmarkDocument document, BookmarkNode node) =>
        ReferenceEquals(node, document.Roots.BookmarkBar) ||
        ReferenceEquals(node, document.Roots.Other) ||
        ReferenceEquals(node, document.Roots.Synced);

    private static void EnsureBelongsToDocument(BookmarkDocument document, BookmarkNode node)
    {
        BookmarkNode current = node;
        while (current.Parent is not null)
        {
            current = current.Parent;
        }

        if (!IsPermanentRoot(document, current))
        {
            throw new BookmarkEditException(
                BookmarkEditError.NodeNotInDocument,
                "The bookmark node does not belong to the active document.");
        }
    }
}
