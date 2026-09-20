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
}
