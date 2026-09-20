using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

public interface IBookmarkHistoryEntry
{
    string Description { get; }

    BookmarkHistoryImpact Impact { get; }

    void Undo(BookmarkDocument document);

    void Redo(BookmarkDocument document);
}
