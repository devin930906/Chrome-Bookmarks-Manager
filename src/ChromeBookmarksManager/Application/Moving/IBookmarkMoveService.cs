using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Moving;

public interface IBookmarkMoveService
{
    BookmarkMoveResult MoveNode(
        BookmarkDocument document,
        BookmarkNode node,
        BookmarkFolder targetParent,
        int targetIndex);

    BookmarkMoveResult MoveBookmarkBefore(
        BookmarkDocument document,
        BookmarkUrl bookmark,
        BookmarkUrl target);

    BookmarkMoveResult MoveBookmarkAfter(
        BookmarkDocument document,
        BookmarkUrl bookmark,
        BookmarkUrl target);

    BookmarkMoveResult MoveFolderBefore(
        BookmarkDocument document,
        BookmarkFolder folder,
        BookmarkFolder target);

    BookmarkMoveResult MoveFolderAfter(
        BookmarkDocument document,
        BookmarkFolder folder,
        BookmarkFolder target);

    BookmarkMoveResult MoveToEnd(
        BookmarkDocument document,
        BookmarkNode node,
        BookmarkFolder targetParent);

    BookmarkBatchMoveResult MoveBookmarksToEnd(
        BookmarkDocument document,
        IReadOnlyList<BookmarkUrl> bookmarks,
        BookmarkFolder targetParent);
}
