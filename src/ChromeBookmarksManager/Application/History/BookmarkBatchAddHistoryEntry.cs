using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

public sealed class BookmarkBatchAddHistoryEntry : IBookmarkHistoryEntry
{
    private readonly BookmarkUrl[] _bookmarks;
    private readonly BookmarkFolder _parent;
    private readonly int _startIndex;

    public BookmarkBatchAddHistoryEntry(
        IReadOnlyList<BookmarkUrl> bookmarks,
        BookmarkFolder parent,
        int startIndex)
    {
        ArgumentNullException.ThrowIfNull(bookmarks);
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));

        if (bookmarks.Count == 0)
        {
            throw new ArgumentException(
                "A batch add history entry requires at least one bookmark.",
                nameof(bookmarks));
        }

        if (startIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }

        _bookmarks = bookmarks.ToArray();
        _startIndex = startIndex;
        Description = _bookmarks.Length == 1
            ? "Add bookmark"
            : $"Add {_bookmarks.Length} bookmarks";
    }

    public string Description { get; }

    public BookmarkHistoryImpact Impact =>
        BookmarkHistoryImpact.SearchRelevant;

    public void Undo(BookmarkDocument document)
    {
        for (var index = 0; index < _bookmarks.Length; index++)
        {
            BookmarkHistoryMutation.EnsureNodeAt(
                _bookmarks[index],
                _parent,
                _startIndex + index);
        }

        for (var index = _bookmarks.Length - 1; index >= 0; index--)
        {
            BookmarkHistoryMutation.DetachWithCounts(
                document,
                _bookmarks[index],
                _parent,
                _startIndex + index,
                urlCount: 1,
                folderCount: 0);
        }
    }

    public void Redo(BookmarkDocument document)
    {
        foreach (var bookmark in _bookmarks)
        {
            if (bookmark.Parent is not null)
            {
                throw new InvalidOperationException(
                    "A batch-added bookmark is already attached and cannot be restored.");
            }
        }

        for (var index = 0; index < _bookmarks.Length; index++)
        {
            BookmarkHistoryMutation.RestoreDetachedWithCounts(
                document,
                _bookmarks[index],
                _parent,
                _startIndex + index,
                urlCount: 1,
                folderCount: 0);
        }
    }
}
