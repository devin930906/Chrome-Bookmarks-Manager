using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Sorting;

public sealed record BookmarkSortResult(
    bool Changed,
    BookmarkFolder Folder,
    IReadOnlyList<BookmarkNode> OriginalOrder,
    IReadOnlyList<BookmarkNode> SortedOrder);
