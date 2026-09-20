namespace ChromeBookmarksManager.Application.Deleting;

public sealed record BookmarkBatchDeleteResult(
    bool Changed,
    IReadOnlyList<BookmarkDeleteResult> RemovedItems,
    int RemovedUrlCount);
