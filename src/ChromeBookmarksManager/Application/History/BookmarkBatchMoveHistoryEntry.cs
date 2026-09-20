using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

public sealed class BookmarkBatchMoveHistoryEntry : IBookmarkHistoryEntry
{
    private readonly IReadOnlyList<(
        BookmarkUrl Bookmark,
        BookmarkMoveResult Move)> _items;

    public BookmarkBatchMoveHistoryEntry(
        IReadOnlyList<BookmarkUrl> bookmarks,
        BookmarkBatchMoveResult result)
    {
        ArgumentNullException.ThrowIfNull(bookmarks);
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Changed)
        {
            throw new ArgumentException(
                "A no-op batch move cannot be recorded in history.",
                nameof(result));
        }

        var seen = new HashSet<BookmarkUrl>(
            ReferenceEqualityComparer.Instance);
        var unique = new List<BookmarkUrl>(bookmarks.Count);

        foreach (var bookmark in bookmarks)
        {
            ArgumentNullException.ThrowIfNull(bookmark);
            if (seen.Add(bookmark))
            {
                unique.Add(bookmark);
            }
        }

        if (unique.Count != result.Moves.Count)
        {
            throw new ArgumentException(
                "The batch move snapshots do not match the moved bookmark set.",
                nameof(result));
        }

        var items = new List<(
            BookmarkUrl Bookmark,
            BookmarkMoveResult Move)>(unique.Count);

        for (var index = 0; index < unique.Count; index++)
        {
            var move = result.Moves[index];
            if (!move.Changed ||
                move.Semantics != BookmarkMoveSemantics.Direct)
            {
                throw new ArgumentException(
                    "Batch move history only accepts changed direct-move snapshots.",
                    nameof(result));
            }

            items.Add((unique[index], move));
        }

        _items = items;
    }

    public string Description =>
        $"Move {_items.Count} bookmarks";

    public BookmarkHistoryImpact Impact =>
        BookmarkHistoryImpact.StructureOnly;

    public void Undo(BookmarkDocument document) =>
        BookmarkHistoryMutation.ApplyBatchRaw(
            document,
            _items.Select(item =>
                new BookmarkHistoryMutation.RawMove(
                    item.Bookmark,
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
                    item.Bookmark,
                    item.Move.SourceParent,
                    item.Move.SourceIndex,
                    item.Move.TargetParent,
                    item.Move.TargetIndex))
            .ToArray());
}
