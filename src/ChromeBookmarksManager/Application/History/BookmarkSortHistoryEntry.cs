using ChromeBookmarksManager.Application.Sorting;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

public sealed class BookmarkSortHistoryEntry : IBookmarkHistoryEntry
{
    private readonly BookmarkFolder _folder;
    private readonly IReadOnlyList<BookmarkNode> _originalOrder;
    private readonly IReadOnlyList<BookmarkNode> _sortedOrder;

    public BookmarkSortHistoryEntry(
        BookmarkSortResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Changed)
        {
            throw new ArgumentException(
                "A no-op sort cannot be recorded in history.",
                nameof(result));
        }

        _folder = result.Folder;
        _originalOrder = result.OriginalOrder.ToArray();
        _sortedOrder = result.SortedOrder.ToArray();
    }

    public string Description => "Sort by name";

    public BookmarkHistoryImpact Impact =>
        BookmarkHistoryImpact.StructureOnly;

    public void Undo(BookmarkDocument document)
    {
        BookmarkHistoryMutation.EnsureBelongsToDocument(
            document,
            _folder);
        BookmarkSortService.ApplyOrder(
            _folder,
            _originalOrder);
    }

    public void Redo(BookmarkDocument document)
    {
        BookmarkHistoryMutation.EnsureBelongsToDocument(
            document,
            _folder);
        BookmarkSortService.ApplyOrder(
            _folder,
            _sortedOrder);
    }
}
