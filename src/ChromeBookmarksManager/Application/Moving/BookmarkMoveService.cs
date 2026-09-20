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
