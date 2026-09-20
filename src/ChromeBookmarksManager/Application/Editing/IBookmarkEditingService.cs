using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Editing;

public interface IBookmarkEditingService
{
    bool RenameNode(BookmarkDocument document, BookmarkNode node, string newName);

    bool EditUrl(BookmarkDocument document, BookmarkUrl bookmark, string newUrl);

    BookmarkUrl AddBookmark(
        BookmarkDocument document,
        BookmarkFolder parent,
        string name,
        string url);

    BookmarkFolder AddFolder(
        BookmarkDocument document,
        BookmarkFolder parent,
        string name);
}
