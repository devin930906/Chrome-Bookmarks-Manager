namespace ChromeBookmarksManager.DragDrop;

internal static class DragDropRules
{
    internal static bool HasExceededDragThreshold(
        double deltaX,
        double deltaY,
        double horizontalThreshold,
        double verticalThreshold) =>
        Math.Abs(deltaX) > horizontalThreshold ||
        Math.Abs(deltaY) > verticalThreshold;

    internal static DropPlacement GetBookmarkRowPlacement(
        double pointerY,
        double rowHeight)
    {
        if (rowHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rowHeight),
                rowHeight,
                "Bookmark row height must be positive.");
        }

        return pointerY < rowHeight / 2
            ? DropPlacement.Before
            : DropPlacement.After;
    }

    internal static DropPlacement GetContentRowPlacement(
        double pointerY,
        double rowHeight,
        bool targetIsFolder)
    {
        if (targetIsFolder)
        {
            if (rowHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rowHeight),
                    rowHeight,
                    "Content row height must be positive.");
            }

            var third = rowHeight / 3;
            if (pointerY < third)
            {
                return DropPlacement.Before;
            }

            return pointerY <= third * 2
                ? DropPlacement.Into
                : DropPlacement.After;
        }

        return GetBookmarkRowPlacement(pointerY, rowHeight);
    }

    internal static bool CanMoveContentNodeInto(
        Domain.BookmarkNode movingNode,
        Domain.BookmarkFolder targetFolder)
    {
        ArgumentNullException.ThrowIfNull(movingNode);
        ArgumentNullException.ThrowIfNull(targetFolder);

        if (movingNode.Parent is null)
        {
            return false;
        }

        return movingNode switch
        {
            Domain.BookmarkFolder movingFolder =>
                CanMoveFolderInto(movingFolder, targetFolder),
            Domain.BookmarkUrl => true,
            _ => false
        };
    }

    internal static bool CanMoveContentNodeRelativeTo(
        Domain.BookmarkNode movingNode,
        Domain.BookmarkNode targetNode)
    {
        ArgumentNullException.ThrowIfNull(movingNode);
        ArgumentNullException.ThrowIfNull(targetNode);

        if (ReferenceEquals(movingNode, targetNode) ||
            movingNode.Parent is null ||
            targetNode.Parent is not { } targetParent)
        {
            return false;
        }

        return movingNode switch
        {
            Domain.BookmarkFolder movingFolder =>
                CanMoveFolderInto(movingFolder, targetParent),
            Domain.BookmarkUrl => true,
            _ => false
        };
    }

    internal static bool CanMoveContentNodesInto(
        IReadOnlyList<Domain.BookmarkNode> movingNodes,
        Domain.BookmarkFolder targetFolder)
    {
        ArgumentNullException.ThrowIfNull(movingNodes);
        ArgumentNullException.ThrowIfNull(targetFolder);

        return movingNodes.Count > 0 &&
               movingNodes.All(
                   node => CanMoveContentNodeInto(
                       node,
                       targetFolder));
    }

    internal static bool CanMoveContentNodesRelativeTo(
        IReadOnlyList<Domain.BookmarkNode> movingNodes,
        Domain.BookmarkNode targetNode)
    {
        ArgumentNullException.ThrowIfNull(movingNodes);
        ArgumentNullException.ThrowIfNull(targetNode);

        if (movingNodes.Count == 0 ||
            movingNodes.Any(
                node => ReferenceEquals(
                    node,
                    targetNode)))
        {
            return false;
        }

        return movingNodes.All(
            node => CanMoveContentNodeRelativeTo(
                node,
                targetNode));
    }

    internal static bool CanPositionallyReorderBookmarks(
        bool isSearchActive) =>
        !isSearchActive;

    internal static bool CanStartFolderDrag(bool isPermanentRoot) =>
        !isPermanentRoot;

    internal static DropPlacement GetFolderRowPlacement(
        double pointerY,
        double rowHeight,
        bool isPermanentRootTarget)
    {
        if (rowHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rowHeight),
                rowHeight,
                "Folder row height must be positive.");
        }

        if (isPermanentRootTarget)
        {
            return DropPlacement.Into;
        }

        var third = rowHeight / 3;
        if (pointerY < third)
        {
            return DropPlacement.Before;
        }

        return pointerY <= third * 2
            ? DropPlacement.Into
            : DropPlacement.After;
    }

    internal static bool CanMoveFolderInto(
        Domain.BookmarkFolder movingFolder,
        Domain.BookmarkFolder targetFolder)
    {
        ArgumentNullException.ThrowIfNull(movingFolder);
        ArgumentNullException.ThrowIfNull(targetFolder);

        return !ReferenceEquals(movingFolder, targetFolder) &&
               !IsFolderWithin(targetFolder, movingFolder);
    }

    internal static bool CanMoveFolderRelativeTo(
        Domain.BookmarkFolder movingFolder,
        Domain.BookmarkFolder targetFolder)
    {
        ArgumentNullException.ThrowIfNull(movingFolder);
        ArgumentNullException.ThrowIfNull(targetFolder);

        return targetFolder.Parent is not null &&
               CanMoveFolderInto(movingFolder, targetFolder);
    }

    private static bool IsFolderWithin(
        Domain.BookmarkFolder candidate,
        Domain.BookmarkFolder ancestor)
    {
        Domain.BookmarkFolder? current = candidate;
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
}
