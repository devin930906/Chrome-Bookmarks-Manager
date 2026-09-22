using ChromeBookmarksManager.Application.Clipboard;
using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

public sealed class BookmarkClipboardPasteHistoryEntry
    : IBookmarkHistoryEntry
{
    private readonly BookmarkBatchMoveHistoryEntry? _moveHistory;
    private readonly IReadOnlyList<BookmarkDeleteResult> _copyItems;
    private readonly int _urlCount;
    private readonly int _folderCount;

    public BookmarkClipboardPasteHistoryEntry(
        BookmarkClipboardPasteResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Changed)
        {
            throw new ArgumentException(
                "A no-op clipboard paste cannot be recorded in history.",
                nameof(result));
        }

        Description =
            $"Paste {result.Nodes.Count} " +
            $"item{(result.Nodes.Count == 1 ? string.Empty : "s")}";

        if (result.MovedOriginalNodes)
        {
            if (result.MoveResult is null)
            {
                throw new ArgumentException(
                    "A cut paste requires batch move history snapshots.",
                    nameof(result));
            }

            _moveHistory = new BookmarkBatchMoveHistoryEntry(
                result.Nodes,
                result.MoveResult);
            _copyItems =
                Array.Empty<BookmarkDeleteResult>();
            Impact = BookmarkHistoryImpact.StructureOnly;
            return;
        }

        if (result.MoveResult is not null)
        {
            throw new ArgumentException(
                "A copy paste cannot contain move history snapshots.",
                nameof(result));
        }

        var copyItems =
            new List<BookmarkDeleteResult>(
                result.Nodes.Count);
        var urlCount = 0;
        var folderCount = 0;

        for (var index = 0;
             index < result.Nodes.Count;
             index++)
        {
            var node = result.Nodes[index];
            var (urls, folders) = CountSubtree(node);

            urlCount = checked(urlCount + urls);
            folderCount = checked(
                folderCount + folders);

            copyItems.Add(
                new BookmarkDeleteResult(
                    node,
                    result.TargetParent,
                    result.TargetIndex + index,
                    urls,
                    folders));
        }

        if (urlCount != result.AddedUrlCount ||
            folderCount != result.AddedFolderCount)
        {
            throw new ArgumentException(
                "The clipboard paste counts do not match the pasted subtrees.",
                nameof(result));
        }

        _copyItems = copyItems;
        _urlCount = urlCount;
        _folderCount = folderCount;
        Impact = BookmarkHistoryImpact.SearchRelevant;
    }

    public string Description { get; }

    public BookmarkHistoryImpact Impact { get; }

    public void Undo(BookmarkDocument document)
    {
        if (_moveHistory is not null)
        {
            _moveHistory.Undo(document);
            return;
        }

        BookmarkHistoryMutation.DetachDeletedBatch(
            document,
            _copyItems,
            _urlCount,
            _folderCount);
    }

    public void Redo(BookmarkDocument document)
    {
        if (_moveHistory is not null)
        {
            _moveHistory.Redo(document);
            return;
        }

        BookmarkHistoryMutation.RestoreDeletedBatch(
            document,
            _copyItems,
            _urlCount,
            _folderCount);
    }

    private static (int UrlCount, int FolderCount)
        CountSubtree(BookmarkNode node)
    {
        var urls = 0;
        var folders = 0;
        var stack = new Stack<BookmarkNode>();
        stack.Push(node);

        while (stack.TryPop(out var current))
        {
            if (current is BookmarkUrl)
            {
                urls = checked(urls + 1);
                continue;
            }

            if (current is not BookmarkFolder folder)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(node),
                    current.Kind,
                    "Unsupported bookmark node kind.");
            }

            folders = checked(folders + 1);

            for (var index = folder.Children.Count - 1;
                 index >= 0;
                 index--)
            {
                stack.Push(folder.Children[index]);
            }
        }

        return (urls, folders);
    }
}
