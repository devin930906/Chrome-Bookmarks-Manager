using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

public sealed class BookmarkBatchDeleteHistoryEntry : IBookmarkHistoryEntry
{
    private readonly IReadOnlyList<BookmarkDeleteResult> _items;
    private readonly int _urlCount;
    private readonly int _folderCount;

    public BookmarkBatchDeleteHistoryEntry(
        BookmarkBatchDeleteResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Changed)
        {
            throw new ArgumentException(
                "A no-op batch delete cannot be recorded in history.",
                nameof(result));
        }

        _items = result.RemovedItems.ToArray();
        _urlCount = result.RemovedItems.Sum(
            item => item.RemovedUrlCount);
        _folderCount = result.RemovedItems.Sum(
            item => item.RemovedFolderCount);

        if (_urlCount != result.RemovedUrlCount)
        {
            throw new ArgumentException(
                "The batch delete URL count does not match its item snapshots.",
                nameof(result));
        }
    }

    public string Description =>
        $"Delete {_items.Count} bookmarks";

    public BookmarkHistoryImpact Impact =>
        BookmarkHistoryImpact.SearchRelevant;

    public void Undo(BookmarkDocument document) =>
        BookmarkHistoryMutation.RestoreDeletedBatch(
            document,
            _items,
            _urlCount,
            _folderCount);

    public void Redo(BookmarkDocument document) =>
        BookmarkHistoryMutation.DetachDeletedBatch(
            document,
            _items,
            _urlCount,
            _folderCount);
}
