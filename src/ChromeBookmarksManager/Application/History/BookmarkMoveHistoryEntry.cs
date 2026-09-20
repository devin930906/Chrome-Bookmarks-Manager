using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

public sealed class BookmarkMoveHistoryEntry : IBookmarkHistoryEntry
{
    private readonly BookmarkNode _node;
    private readonly BookmarkMoveResult _move;

    public BookmarkMoveHistoryEntry(
        BookmarkNode node,
        BookmarkMoveResult move)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
        _move = move ?? throw new ArgumentNullException(nameof(move));

        if (!move.Changed)
        {
            throw new ArgumentException(
                "A no-op move cannot be recorded in history.",
                nameof(move));
        }

        if (move.Semantics == BookmarkMoveSemantics.SameKindSlotReorder &&
            (!move.SourceVisibleIndex.HasValue ||
             !move.TargetVisibleIndex.HasValue ||
             !ReferenceEquals(move.SourceParent, move.TargetParent)))
        {
            throw new ArgumentException(
                "Same-kind reorder history requires visible source/target indexes in one parent.",
                nameof(move));
        }

        Description = node is BookmarkFolder
            ? "Move folder"
            : "Move bookmark";
    }

    public string Description { get; }

    public BookmarkHistoryImpact Impact =>
        BookmarkHistoryImpact.StructureOnly;

    public void Undo(BookmarkDocument document)
    {
        if (_move.Semantics == BookmarkMoveSemantics.SameKindSlotReorder)
        {
            BookmarkHistoryMutation.ReorderSameKind(
                document,
                _node,
                _move.TargetParent,
                _move.TargetVisibleIndex!.Value,
                _move.SourceVisibleIndex!.Value);
            return;
        }

        BookmarkHistoryMutation.ApplyRawMove(
            document,
            new BookmarkHistoryMutation.RawMove(
                _node,
                _move.TargetParent,
                _move.TargetIndex,
                _move.SourceParent,
                _move.SourceIndex));
    }

    public void Redo(BookmarkDocument document)
    {
        if (_move.Semantics == BookmarkMoveSemantics.SameKindSlotReorder)
        {
            BookmarkHistoryMutation.ReorderSameKind(
                document,
                _node,
                _move.SourceParent,
                _move.SourceVisibleIndex!.Value,
                _move.TargetVisibleIndex!.Value);
            return;
        }

        BookmarkHistoryMutation.ApplyRawMove(
            document,
            new BookmarkHistoryMutation.RawMove(
                _node,
                _move.SourceParent,
                _move.SourceIndex,
                _move.TargetParent,
                _move.TargetIndex));
    }
}
