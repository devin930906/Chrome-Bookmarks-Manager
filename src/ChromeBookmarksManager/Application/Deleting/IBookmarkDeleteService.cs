using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Deleting;

public interface IBookmarkDeleteService
{
    BookmarkDeleteResult DeleteNode(
        BookmarkDocument document,
        BookmarkNode node);

    BookmarkBatchDeleteResult DeleteBookmarks(
        BookmarkDocument document,
        IReadOnlyList<BookmarkUrl> bookmarks);
}
