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

    public BookmarkBatchDeleteResult DeleteBookmarks(
        BookmarkDocument document,
        IReadOnlyList<BookmarkUrl> bookmarks)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(bookmarks);

        if (bookmarks.Count == 0)
        {
            return new BookmarkBatchDeleteResult(
                false,
                Array.Empty<BookmarkDeleteResult>(),
                0);
        }

        var seen = new HashSet<BookmarkUrl>(
            ReferenceEqualityComparer.Instance);
        var unique = new List<BookmarkUrl>(bookmarks.Count);

        foreach (var bookmark in bookmarks)
        {
            ArgumentNullException.ThrowIfNull(bookmark);

            if (seen.Add(bookmark))
            {
                unique.Add(bookmark);
            }
        }

        var snapshots = new List<BookmarkDeleteResult>(
            unique.Count);

        foreach (var bookmark in unique)
        {
            EnsureBelongsToDocument(document, bookmark);

            var parent = bookmark.Parent
                ?? throw new BookmarkDeleteException(
                    BookmarkDeleteError.MissingParent,
                    "The bookmark has no deletable parent.");

            var index = parent.IndexOfChild(bookmark);
            if (index < 0)
            {
                throw new BookmarkDeleteException(
                    BookmarkDeleteError.NodeNotInDocument,
                    "The bookmark is not present in its recorded parent.");
            }

            snapshots.Add(
                new BookmarkDeleteResult(
                    bookmark,
                    parent,
                    index,
                    1,
                    0));
        }

        if (unique.Count > document.UrlCount)
        {
            throw new InvalidOperationException(
                "Deleting the requested bookmarks would make bookmark document counts invalid.");
        }

        var groups = snapshots
            .GroupBy(
                snapshot => snapshot.SourceParent)
            .ToArray();

        try
        {
            foreach (var group in groups)
            {
                foreach (var snapshot in group
                    .OrderByDescending(item => item.SourceIndex))
                {
                    group.Key.RemoveChildAt(snapshot.SourceIndex);
                }
            }

            document.RecordRemovedSubtree(
                unique.Count,
                0);
        }
        catch
        {
            foreach (var group in groups)
            {
                foreach (var snapshot in group
                    .OrderBy(item => item.SourceIndex))
                {
                    if (snapshot.Node.Parent is null)
                    {
                        snapshot.SourceParent.InsertChild(
                            snapshot.SourceIndex,
                            snapshot.Node);
                    }
                }
            }

            throw;
        }

        return new BookmarkBatchDeleteResult(
            true,
            snapshots,
            unique.Count);
    }

    public BookmarkBatchDeleteResult DeleteNodes(
        BookmarkDocument document,
        IReadOnlyList<BookmarkNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(nodes);

        if (nodes.Count == 0)
        {
            return new BookmarkBatchDeleteResult(
                false,
                Array.Empty<BookmarkDeleteResult>(),
                0);
        }

        var seen = new HashSet<BookmarkNode>(
            ReferenceEqualityComparer.Instance);
        var unique = new List<BookmarkNode>(nodes.Count);

        foreach (var node in nodes)
        {
            ArgumentNullException.ThrowIfNull(node);

            if (seen.Add(node))
            {
                unique.Add(node);
            }
        }

        foreach (var node in unique)
        {
            EnsureBelongsToDocument(document, node);

            if (IsPermanentRoot(document, node))
            {
                throw new BookmarkDeleteException(
                    BookmarkDeleteError.ProtectedRoot,
                    "Permanent Chrome bookmark roots cannot be deleted.");
            }

            var parent = node.Parent
                ?? throw new BookmarkDeleteException(
                    BookmarkDeleteError.MissingParent,
                    "The bookmark node has no deletable parent.");

            if (parent.IndexOfChild(node) < 0)
            {
                throw new BookmarkDeleteException(
                    BookmarkDeleteError.NodeNotInDocument,
                    "The bookmark node is not present in its recorded parent.");
            }
        }

        var selected = new HashSet<BookmarkNode>(
            unique,
            ReferenceEqualityComparer.Instance);
        var topLevel = unique
            .Where(node => !HasSelectedAncestor(node, selected))
            .ToArray();

        var snapshots = new List<BookmarkDeleteResult>(
            topLevel.Length);
        var removedUrlCount = 0;
        var removedFolderCount = 0;

        foreach (var node in topLevel)
        {
            var parent = node.Parent!;
            var index = parent.IndexOfChild(node);
            var (urls, folders) = CountSubtree(node);

            removedUrlCount = checked(removedUrlCount + urls);
            removedFolderCount = checked(removedFolderCount + folders);

            snapshots.Add(
                new BookmarkDeleteResult(
                    node,
                    parent,
                    index,
                    urls,
                    folders));
        }

        if (removedUrlCount > document.UrlCount ||
            removedFolderCount > document.FolderCount)
        {
            throw new InvalidOperationException(
                "Deleting the requested nodes would make bookmark document counts invalid.");
        }

        var groups = snapshots
            .GroupBy(snapshot => snapshot.SourceParent)
            .ToArray();

        try
        {
            foreach (var group in groups)
            {
                foreach (var snapshot in group
                    .OrderByDescending(item => item.SourceIndex))
                {
                    group.Key.RemoveChildAt(snapshot.SourceIndex);
                }
            }

            document.RecordRemovedSubtree(
                removedUrlCount,
                removedFolderCount);
        }
        catch
        {
            foreach (var group in groups)
            {
                foreach (var snapshot in group
                    .OrderBy(item => item.SourceIndex))
                {
                    if (snapshot.Node.Parent is null)
                    {
                        snapshot.SourceParent.InsertChild(
                            snapshot.SourceIndex,
                            snapshot.Node);
                    }
                }
            }

            throw;
        }

        return new BookmarkBatchDeleteResult(
            true,
            snapshots,
            removedUrlCount);
    }

    private static bool HasSelectedAncestor(
        BookmarkNode node,
        IReadOnlySet<BookmarkNode> selected)
    {
        var current = node.Parent;

        while (current is not null)
        {
            if (selected.Contains(current))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
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
