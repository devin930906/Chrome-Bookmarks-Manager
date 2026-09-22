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

        var nodes = new List<BookmarkNode>(
            document.UrlCount + Math.Max(0, document.FolderCount - 3));
        var bookmarks = new List<BookmarkUrl>(document.UrlCount);
        var roots = document.Roots;
        var stack = new Stack<BookmarkNode>();

        PushChildrenReverse(stack, roots.Synced);
        PushChildrenReverse(stack, roots.Other);
        PushChildrenReverse(stack, roots.BookmarkBar);

        while (stack.TryPop(out var node))
        {
            cancellationToken.ThrowIfCancellationRequested();
            nodes.Add(node);

            if (node is BookmarkUrl bookmark)
            {
                bookmarks.Add(bookmark);
                continue;
            }

            if (node is BookmarkFolder folder)
            {
                PushChildrenReverse(stack, folder);
            }
        }

        Nodes = new ReadOnlyCollection<BookmarkNode>(nodes);
        Bookmarks = new ReadOnlyCollection<BookmarkUrl>(bookmarks);
    }

    public IReadOnlyList<BookmarkNode> Nodes { get; }

    public IReadOnlyList<BookmarkUrl> Bookmarks { get; }

    public int NodeCount => Nodes.Count;

    // Compatibility projection retained for V0.4 scale contracts.
    public int Count => Bookmarks.Count;

    private static void PushChildrenReverse(
        Stack<BookmarkNode> stack,
        BookmarkFolder folder)
    {
        for (var index = folder.Children.Count - 1; index >= 0; index--)
        {
            stack.Push(folder.Children[index]);
        }
    }
}
