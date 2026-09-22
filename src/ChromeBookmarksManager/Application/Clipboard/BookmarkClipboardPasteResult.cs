using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Clipboard;

public sealed record BookmarkClipboardPasteResult(
    bool Changed,
    bool MovedOriginalNodes,
    IReadOnlyList<BookmarkNode> Nodes,
    BookmarkFolder TargetParent,
    int TargetIndex,
    int AddedUrlCount,
    int AddedFolderCount,
    BookmarkBatchMoveResult? MoveResult);
