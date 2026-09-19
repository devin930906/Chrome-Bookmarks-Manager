using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Search;

public interface IBookmarkSearchService
{
    Task<BookmarkSearchIndex> BuildIndexAsync(
        BookmarkDocument document,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BookmarkUrl>> SearchAsync(
        BookmarkSearchIndex index,
        string query,
        BookmarkSearchScope scope,
        BookmarkFolder? currentFolder,
        CancellationToken cancellationToken);
}
