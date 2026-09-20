using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Moving;

public interface IBookmarkMoveService
{
    BookmarkMoveResult MoveNode(
        BookmarkDocument document,
        BookmarkNode node,
        BookmarkFolder targetParent,
        int targetIndex);
}
