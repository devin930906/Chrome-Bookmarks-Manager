using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Editing;

public interface IBookmarkEditingService
{
    bool RenameNode(BookmarkDocument document, BookmarkNode node, string newName);

    bool EditUrl(BookmarkDocument document, BookmarkUrl bookmark, string newUrl);
}
