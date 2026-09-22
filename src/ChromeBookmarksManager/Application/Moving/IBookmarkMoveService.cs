using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Moving;

public interface IBookmarkMoveService
{
    BookmarkMoveResult MoveNode(
        BookmarkDocument document,
        BookmarkNode node,
        BookmarkFolder targetParent,
        int targetIndex);

    BookmarkMoveResult MoveNodeBefore(
        BookmarkDocument document,
        BookmarkNode node,
        BookmarkNode target);

    BookmarkMoveResult MoveNodeAfter(
        BookmarkDocument document,
        BookmarkNode node,
        BookmarkNode target);

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

    BookmarkBatchMoveResult MoveNodes(
        BookmarkDocument document,
        IReadOnlyList<BookmarkNode> nodes,
        BookmarkFolder targetParent,
        int targetIndex);

    BookmarkBatchMoveResult MoveBookmarksToEnd(
        BookmarkDocument document,
        IReadOnlyList<BookmarkUrl> bookmarks,
        BookmarkFolder targetParent);
}
