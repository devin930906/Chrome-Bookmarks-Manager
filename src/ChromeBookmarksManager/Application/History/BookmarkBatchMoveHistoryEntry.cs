using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

public sealed class BookmarkBatchMoveHistoryEntry : IBookmarkHistoryEntry
{
    private readonly IReadOnlyList<(
        BookmarkNode Node,
        BookmarkMoveResult Move)> _items;

    public BookmarkBatchMoveHistoryEntry(
        IReadOnlyList<BookmarkUrl> bookmarks,
        BookmarkBatchMoveResult result)
        : this(
            bookmarks is null
                ? throw new ArgumentNullException(nameof(bookmarks))
                : bookmarks.Cast<BookmarkNode>().ToArray(),
            result)
    {
    }

    public BookmarkBatchMoveHistoryEntry(
        IReadOnlyList<BookmarkNode> nodes,
        BookmarkBatchMoveResult result)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Changed)
        {
            throw new ArgumentException(
                "A no-op batch move cannot be recorded in history.",
                nameof(result));
        }

        var seen = new HashSet<BookmarkNode>(
            ReferenceEqualityComparer.Instance);
        var unique = new List<BookmarkNode>(nodes.Count);

        foreach (var node in nodes)
        {
            ArgumentNullException.ThrowIfNull(node);

            if (seen.Add(node))
            {
                unique.Add(node);
            }
        }

        var selected = new HashSet<BookmarkNode>(
            unique,
            ReferenceEqualityComparer.Instance);
        var topLevel = unique
            .Where(node => !HasSelectedAncestor(node, selected))
            .ToArray();

        if (topLevel.Length != result.Moves.Count)
        {
            throw new ArgumentException(
                "The batch move snapshots do not match the moved node set.",
                nameof(result));
        }

        var items = new List<(
            BookmarkNode Node,
            BookmarkMoveResult Move)>(topLevel.Length);

        for (var index = 0; index < topLevel.Length; index++)
        {
            var move = result.Moves[index];

            if (!move.Changed ||
                move.Semantics != BookmarkMoveSemantics.Direct)
            {
                throw new ArgumentException(
                    "Batch move history only accepts changed direct-move snapshots.",
                    nameof(result));
            }

            items.Add((topLevel[index], move));
        }

        _items = items;
    }

    public string Description =>
        _items.All(item => item.Node is BookmarkUrl)
            ? $"Move {_items.Count} bookmarks"
            : $"Move {_items.Count} items";

    public BookmarkHistoryImpact Impact =>
        BookmarkHistoryImpact.StructureOnly;

    public void Undo(BookmarkDocument document) =>
        BookmarkHistoryMutation.ApplyBatchRaw(
            document,
            _items.Select(item =>
                new BookmarkHistoryMutation.RawMove(
                    item.Node,
                    item.Move.TargetParent,
                    item.Move.TargetIndex,
                    item.Move.SourceParent,
                    item.Move.SourceIndex))
            .ToArray());

    public void Redo(BookmarkDocument document) =>
        BookmarkHistoryMutation.ApplyBatchRaw(
            document,
            _items.Select(item =>
                new BookmarkHistoryMutation.RawMove(
                    item.Node,
                    item.Move.SourceParent,
                    item.Move.SourceIndex,
                    item.Move.TargetParent,
                    item.Move.TargetIndex))
            .ToArray());

    private static bool HasSelectedAncestor(
        BookmarkNode node,
        HashSet<BookmarkNode> selected)
    {
        var current = node.Parent;

        while (current is not null)
        {
            if (selected.Contains(current))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }
}
