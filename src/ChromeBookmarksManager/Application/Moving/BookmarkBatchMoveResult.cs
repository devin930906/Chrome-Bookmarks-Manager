namespace ChromeBookmarksManager.Application.Moving;

public sealed record BookmarkBatchMoveResult(
    bool Changed,
    IReadOnlyList<BookmarkMoveResult> Moves);
