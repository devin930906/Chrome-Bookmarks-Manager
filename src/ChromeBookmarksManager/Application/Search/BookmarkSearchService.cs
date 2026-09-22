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

    public Task<IReadOnlyList<BookmarkNode>> SearchAsync(
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
            return Task.FromResult<IReadOnlyList<BookmarkNode>>(
                Array.Empty<BookmarkNode>());
        }

        return scope switch
        {
            BookmarkSearchScope.AllBookmarks => Task.Run(
                () => SearchAll(index, trimmedQuery, cancellationToken),
                cancellationToken),
            BookmarkSearchScope.CurrentFolder when currentFolder is null =>
                Task.FromResult<IReadOnlyList<BookmarkNode>>(
                    Array.Empty<BookmarkNode>()),
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

    private static IReadOnlyList<BookmarkNode> SearchAll(
        BookmarkSearchIndex index,
        string query,
        CancellationToken cancellationToken)
    {
        var matches = new List<BookmarkNode>();

        foreach (var node in index.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Matches(node, query))
            {
                matches.Add(node);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return matches.AsReadOnly();
    }

    private static IReadOnlyList<BookmarkNode> SearchCurrentFolder(
        BookmarkFolder currentFolder,
        string query,
        CancellationToken cancellationToken)
    {
        var matches = new List<BookmarkNode>();

        foreach (var child in currentFolder.Children)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Matches(child, query))
            {
                matches.Add(child);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return matches.AsReadOnly();
    }

    private static bool Matches(
        BookmarkNode node,
        string query) =>
        node switch
        {
            BookmarkFolder folder =>
                folder.Name.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase),
            BookmarkUrl bookmark =>
                bookmark.Name.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase) ||
                bookmark.Url.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase),
            _ => false
        };
}
