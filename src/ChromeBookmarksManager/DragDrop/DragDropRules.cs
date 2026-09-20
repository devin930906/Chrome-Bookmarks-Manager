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
