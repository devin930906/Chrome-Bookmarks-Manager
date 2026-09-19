using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Search;

public sealed class BookmarkSearchService : IBookmarkSearchService
{
    public Task<BookmarkSearchIndex> BuildIndexAsync(
        BookmarkDocument document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(
            () => new BookmarkSearchIndex(document, cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlyList<BookmarkUrl>> SearchAsync(
        BookmarkSearchIndex index,
        string query,
        BookmarkSearchScope scope,
        BookmarkFolder? currentFolder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var trimmedQuery = query.Trim();
        if (trimmedQuery.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<BookmarkUrl>>(
                Array.Empty<BookmarkUrl>());
        }

        return scope switch
        {
            BookmarkSearchScope.AllBookmarks => Task.Run(
                () => SearchAll(index, trimmedQuery, cancellationToken),
                cancellationToken),
            BookmarkSearchScope.CurrentFolder when currentFolder is null =>
                Task.FromResult<IReadOnlyList<BookmarkUrl>>(
                    Array.Empty<BookmarkUrl>()),
            BookmarkSearchScope.CurrentFolder => Task.Run(
                () => SearchCurrentFolder(
                    currentFolder!,
                    trimmedQuery,
                    cancellationToken),
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(
                nameof(scope),
                scope,
                "Unsupported bookmark search scope.")
        };
    }

    private static IReadOnlyList<BookmarkUrl> SearchAll(
        BookmarkSearchIndex index,
        string query,
        CancellationToken cancellationToken)
    {
        var matches = new List<BookmarkUrl>();

        foreach (var bookmark in index.Bookmarks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Matches(bookmark, query))
            {
                matches.Add(bookmark);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return matches.AsReadOnly();
    }

    private static IReadOnlyList<BookmarkUrl> SearchCurrentFolder(
        BookmarkFolder currentFolder,
        string query,
        CancellationToken cancellationToken)
    {
        var matches = new List<BookmarkUrl>();

        foreach (var child in currentFolder.Children)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (child is BookmarkUrl bookmark && Matches(bookmark, query))
            {
                matches.Add(bookmark);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return matches.AsReadOnly();
    }

    private static bool Matches(BookmarkUrl bookmark, string query) =>
        bookmark.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        bookmark.Url.Contains(query, StringComparison.OrdinalIgnoreCase);
}
