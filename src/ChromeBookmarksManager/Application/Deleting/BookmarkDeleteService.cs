using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Deleting;

public sealed class BookmarkDeleteService : IBookmarkDeleteService
{
    public BookmarkDeleteResult DeleteNode(
        BookmarkDocument document,
        BookmarkNode node)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(node);

        EnsureBelongsToDocument(document, node);

        if (IsPermanentRoot(document, node))
        {
            throw new BookmarkDeleteException(
                BookmarkDeleteError.ProtectedRoot,
                "Permanent Chrome bookmark roots cannot be deleted.");
        }

        var sourceParent = node.Parent
            ?? throw new BookmarkDeleteException(
                BookmarkDeleteError.MissingParent,
                "The bookmark node has no deletable parent.");

        var sourceIndex = sourceParent.IndexOfChild(node);
        if (sourceIndex < 0)
        {
            throw new BookmarkDeleteException(
                BookmarkDeleteError.NodeNotInDocument,
                "The bookmark node is not present in its recorded parent.");
        }

        var (removedUrls, removedFolders) =
            CountSubtree(node);

        if (removedUrls > document.UrlCount ||
            removedFolders > document.FolderCount)
        {
            throw new InvalidOperationException(
                "Deleting the requested subtree would make bookmark document counts invalid.");
        }

        var removed = sourceParent.RemoveChildAt(sourceIndex);

        try
        {
            document.RecordRemovedSubtree(
                removedUrls,
                removedFolders);
        }
        catch
        {
            sourceParent.InsertChild(sourceIndex, removed);
            throw;
        }

        return new BookmarkDeleteResult(
            removed,
            sourceParent,
            sourceIndex,
            removedUrls,
            removedFolders);
    }

    private static (int UrlCount, int FolderCount) CountSubtree(
        BookmarkNode node)
    {
        var urls = 0;
        var folders = 0;
        var stack = new Stack<BookmarkNode>();
        stack.Push(node);

        while (stack.TryPop(out var current))
        {
            if (current is BookmarkFolder folder)
            {
                folders = checked(folders + 1);

                for (var index = folder.Children.Count - 1;
                     index >= 0;
                     index--)
                {
                    stack.Push(folder.Children[index]);
                }
            }
            else if (current is BookmarkUrl)
            {
                urls = checked(urls + 1);
            }
            else
            {
                throw new ArgumentOutOfRangeException(
                    nameof(node),
                    current.Kind,
                    "Unsupported bookmark node kind.");
            }
        }

        return (urls, folders);
    }

    private static void EnsureBelongsToDocument(
        BookmarkDocument document,
        BookmarkNode node)
    {
        BookmarkNode current = node;

        while (current.Parent is not null)
        {
            current = current.Parent;
        }

        if (!IsPermanentRoot(document, current))
        {
            throw new BookmarkDeleteException(
                BookmarkDeleteError.NodeNotInDocument,
                "The bookmark node does not belong to the active document.");
        }
    }

    private static bool IsPermanentRoot(
        BookmarkDocument document,
        BookmarkNode node) =>
        ReferenceEquals(node, document.Roots.BookmarkBar) ||
        ReferenceEquals(node, document.Roots.Other) ||
        ReferenceEquals(node, document.Roots.Synced);
}
