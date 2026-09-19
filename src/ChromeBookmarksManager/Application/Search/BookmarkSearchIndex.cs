using System.Collections.ObjectModel;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Search;

public sealed class BookmarkSearchIndex
{
    public BookmarkSearchIndex(
        BookmarkDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var bookmarks = new List<BookmarkUrl>(document.UrlCount);
        var roots = document.Roots;
        var stack = new Stack<BookmarkNode>(
            new BookmarkNode[]
            {
                roots.Synced,
                roots.Other,
                roots.BookmarkBar
            });

        while (stack.TryPop(out var node))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (node is BookmarkUrl bookmark)
            {
                bookmarks.Add(bookmark);
                continue;
            }

            if (node is not BookmarkFolder folder)
            {
                continue;
            }

            for (var index = folder.Children.Count - 1; index >= 0; index--)
            {
                stack.Push(folder.Children[index]);
            }
        }

        Bookmarks = new ReadOnlyCollection<BookmarkUrl>(bookmarks);
    }

    public IReadOnlyList<BookmarkUrl> Bookmarks { get; }

    public int Count => Bookmarks.Count;
}
