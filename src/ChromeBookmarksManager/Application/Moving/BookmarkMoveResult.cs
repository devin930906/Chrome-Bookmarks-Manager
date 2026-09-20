using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Moving;

public sealed record BookmarkMoveResult(
    bool Changed,
    BookmarkFolder SourceParent,
    int SourceIndex,
    BookmarkFolder TargetParent,
    int TargetIndex,
    BookmarkMoveSemantics Semantics = BookmarkMoveSemantics.Direct,
    int? SourceVisibleIndex = null,
    int? TargetVisibleIndex = null);
