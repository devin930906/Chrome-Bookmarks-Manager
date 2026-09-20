using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

public sealed class BookmarkDeleteHistoryEntry : IBookmarkHistoryEntry
{
    private readonly BookmarkDeleteResult _delete;

    public BookmarkDeleteHistoryEntry(BookmarkDeleteResult delete)
    {
        _delete = delete ?? throw new ArgumentNullException(nameof(delete));

        Description = delete.Node is BookmarkFolder
            ? "Delete folder"
            : "Delete bookmark";
    }

    public string Description { get; }

    public BookmarkHistoryImpact Impact =>
        BookmarkHistoryImpact.SearchRelevant;

    public void Undo(BookmarkDocument document) =>
        BookmarkHistoryMutation.RestoreDetachedWithCounts(
            document,
            _delete.Node,
            _delete.SourceParent,
            _delete.SourceIndex,
            _delete.RemovedUrlCount,
            _delete.RemovedFolderCount);

    public void Redo(BookmarkDocument document) =>
        BookmarkHistoryMutation.DetachWithCounts(
            document,
            _delete.Node,
            _delete.SourceParent,
            _delete.SourceIndex,
            _delete.RemovedUrlCount,
            _delete.RemovedFolderCount);
}
