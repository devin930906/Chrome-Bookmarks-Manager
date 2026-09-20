using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Deleting;

public sealed record BookmarkDeleteResult(
    BookmarkNode Node,
    BookmarkFolder SourceParent,
    int SourceIndex,
    int RemovedUrlCount,
    int RemovedFolderCount);
