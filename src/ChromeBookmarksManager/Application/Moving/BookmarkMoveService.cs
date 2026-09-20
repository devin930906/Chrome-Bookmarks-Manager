using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Moving;

public sealed class BookmarkMoveService : IBookmarkMoveService
{
    public BookmarkMoveResult MoveNode(
        BookmarkDocument document,
        BookmarkNode node,
        BookmarkFolder targetParent,
        int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(targetParent);

        EnsureBelongsToDocument(
            document,
            node,
            BookmarkMoveError.NodeNotInDocument,
            "The bookmark node does not belong to the active document.");

        EnsureBelongsToDocument(
            document,
            targetParent,
            BookmarkMoveError.TargetNotInDocument,
            "The target folder does not belong to the active document.");

        if (IsPermanentRoot(document, node))
        {
            throw new BookmarkMoveException(
                BookmarkMoveError.ProtectedRoot,
                "Permanent Chrome bookmark roots cannot be moved.");
        }

        var sourceParent = node.Parent
            ?? throw new BookmarkMoveException(
                BookmarkMoveError.MissingParent,
                "The bookmark node has no movable parent.");

        var sourceIndex = sourceParent.IndexOfChild(node);
        if (sourceIndex < 0)
        {
            throw new BookmarkMoveException(
                BookmarkMoveError.NodeNotInDocument,
                "The bookmark node is not present in its recorded parent.");
        }

        if (targetIndex < 0 || targetIndex > targetParent.Children.Count)
        {
            throw new BookmarkMoveException(
                BookmarkMoveError.InvalidIndex,
                "The requested bookmark insertion position is invalid.");
        }

        if (node is BookmarkFolder movingFolder)
        {
            if (ReferenceEquals(movingFolder, targetParent))
            {
                throw new BookmarkMoveException(
                    BookmarkMoveError.SelfTarget,
                    "A folder cannot be moved into itself.");
            }

            if (IsDescendantOf(targetParent, movingFolder))
            {
                throw new BookmarkMoveException(
                    BookmarkMoveError.DescendantTarget,
                    "A folder cannot be moved into one of its descendants.");
            }
        }

        var normalizedTargetIndex = targetIndex;
        if (ReferenceEquals(sourceParent, targetParent))
        {
            if (targetIndex == sourceIndex ||
                targetIndex == sourceIndex + 1)
            {
                return new BookmarkMoveResult(
                    false,
                    sourceParent,
                    sourceIndex,
                    targetParent,
                    sourceIndex);
            }

            if (sourceIndex < targetIndex)
            {
                normalizedTargetIndex--;
            }
        }

        sourceParent.RemoveChildAt(sourceIndex);

        try
        {
            targetParent.InsertChild(normalizedTargetIndex, node);
        }
        catch
        {
            sourceParent.InsertChild(sourceIndex, node);
            throw;
        }

        return new BookmarkMoveResult(
            true,
            sourceParent,
            sourceIndex,
            targetParent,
            normalizedTargetIndex);
    }

    public BookmarkMoveResult MoveBookmarkBefore(
        BookmarkDocument document,
        BookmarkUrl bookmark,
        BookmarkUrl target) =>
        MoveRelative(document, bookmark, target, insertAfter: false);

    public BookmarkMoveResult MoveBookmarkAfter(
        BookmarkDocument document,
        BookmarkUrl bookmark,
        BookmarkUrl target) =>
        MoveRelative(document, bookmark, target, insertAfter: true);

    public BookmarkMoveResult MoveFolderBefore(
        BookmarkDocument document,
        BookmarkFolder folder,
        BookmarkFolder target) =>
        MoveRelative(document, folder, target, insertAfter: false);

    public BookmarkMoveResult MoveFolderAfter(
        BookmarkDocument document,
        BookmarkFolder folder,
        BookmarkFolder target) =>
        MoveRelative(document, folder, target, insertAfter: true);

    public BookmarkMoveResult MoveToEnd(
        BookmarkDocument document,
        BookmarkNode node,
        BookmarkFolder targetParent)
    {
        ArgumentNullException.ThrowIfNull(targetParent);

        return MoveNode(
            document,
            node,
            targetParent,
            targetParent.Children.Count);
    }

    private BookmarkMoveResult MoveRelative(
        BookmarkDocument document,
        BookmarkNode node,
        BookmarkNode target,
        bool insertAfter)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(target);

        if (node.Kind != target.Kind)
        {
            throw new BookmarkMoveException(
                BookmarkMoveError.InvalidDropTarget,
                "Bookmarks can only be reordered against bookmarks, and folders against folders.");
        }

        EnsureBelongsToDocument(
            document,
            node,
            BookmarkMoveError.NodeNotInDocument,
            "The bookmark node does not belong to the active document.");

        EnsureBelongsToDocument(
            document,
            target,
            BookmarkMoveError.TargetNotInDocument,
            "The drop target does not belong to the active document.");

        if (IsPermanentRoot(document, node))
        {
            throw new BookmarkMoveException(
                BookmarkMoveError.ProtectedRoot,
                "Permanent Chrome bookmark roots cannot be moved.");
        }

        var sourceParent = node.Parent
            ?? throw new BookmarkMoveException(
                BookmarkMoveError.MissingParent,
                "The bookmark node has no movable parent.");

        var targetParent = target.Parent
            ?? throw new BookmarkMoveException(
                BookmarkMoveError.InvalidDropTarget,
                "Permanent Chrome roots cannot be used as before/after sibling targets.");

        if (ReferenceEquals(sourceParent, targetParent))
        {
            return ReorderSameKindSlots(
                sourceParent,
                node,
                target,
                insertAfter);
        }

        var targetIndex = targetParent.IndexOfChild(target);
        if (targetIndex < 0)
        {
            throw new BookmarkMoveException(
                BookmarkMoveError.InvalidDropTarget,
                "The drop target is not present in its recorded parent.");
        }

        if (insertAfter)
        {
            targetIndex++;
        }

        return MoveNode(
            document,
            node,
            targetParent,
            targetIndex);
    }

    private static BookmarkMoveResult ReorderSameKindSlots(
        BookmarkFolder parent,
        BookmarkNode node,
        BookmarkNode target,
        bool insertAfter)
    {
        var sourceIndex = parent.IndexOfChild(node);
        if (sourceIndex < 0)
        {
            throw new BookmarkMoveException(
                BookmarkMoveError.NodeNotInDocument,
                "The bookmark node is not present in its recorded parent.");
        }

        if (ReferenceEquals(node, target))
        {
            return new BookmarkMoveResult(
                false,
                parent,
                sourceIndex,
                parent,
                sourceIndex);
        }

        var slotIndexes = new List<int>();
        var visibleNodes = new List<BookmarkNode>();

        for (var index = 0; index < parent.Children.Count; index++)
        {
            var child = parent.Children[index];
            if (child.Kind != node.Kind)
            {
                continue;
            }

            slotIndexes.Add(index);
            visibleNodes.Add(child);
        }

        var sourceVisibleIndex = IndexOfReference(visibleNodes, node);
        var targetVisibleIndex = IndexOfReference(visibleNodes, target);

        if (sourceVisibleIndex < 0 || targetVisibleIndex < 0)
        {
            throw new BookmarkMoveException(
                BookmarkMoveError.InvalidDropTarget,
                "The requested reorder target is not available in the current folder.");
        }

        var desiredNodes = visibleNodes.ToList();
        desiredNodes.RemoveAt(sourceVisibleIndex);

        var adjustedTargetIndex = IndexOfReference(desiredNodes, target);
        var insertVisibleIndex = adjustedTargetIndex + (insertAfter ? 1 : 0);
        desiredNodes.Insert(insertVisibleIndex, node);

        if (ReferenceSequenceEqual(visibleNodes, desiredNodes))
        {
            return new BookmarkMoveResult(
                false,
                parent,
                sourceIndex,
                parent,
                sourceIndex);
        }

        for (var index = slotIndexes.Count - 1; index >= 0; index--)
        {
            parent.RemoveChildAt(slotIndexes[index]);
        }

        for (var index = 0; index < slotIndexes.Count; index++)
        {
            parent.InsertChild(slotIndexes[index], desiredNodes[index]);
        }

        return new BookmarkMoveResult(
            true,
            parent,
            sourceIndex,
            parent,
            slotIndexes[insertVisibleIndex]);
    }

    private static int IndexOfReference(
        IReadOnlyList<BookmarkNode> nodes,
        BookmarkNode target)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            if (ReferenceEquals(nodes[index], target))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool ReferenceSequenceEqual(
        IReadOnlyList<BookmarkNode> left,
        IReadOnlyList<BookmarkNode> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!ReferenceEquals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDescendantOf(
        BookmarkFolder candidate,
        BookmarkFolder ancestor)
    {
        BookmarkFolder? current = candidate;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static bool IsPermanentRoot(
        BookmarkDocument document,
        BookmarkNode node) =>
        ReferenceEquals(node, document.Roots.BookmarkBar) ||
        ReferenceEquals(node, document.Roots.Other) ||
        ReferenceEquals(node, document.Roots.Synced);

    private static void EnsureBelongsToDocument(
        BookmarkDocument document,
        BookmarkNode node,
        BookmarkMoveError error,
        string message)
    {
        BookmarkNode current = node;
        while (current.Parent is not null)
        {
            current = current.Parent;
        }

        if (!IsPermanentRoot(document, current))
        {
            throw new BookmarkMoveException(error, message);
        }
    }
}
