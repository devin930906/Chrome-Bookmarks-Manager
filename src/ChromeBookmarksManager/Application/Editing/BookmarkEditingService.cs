using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Editing;

public sealed class BookmarkEditingService : IBookmarkEditingService
{
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
