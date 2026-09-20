using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

public sealed class BookmarkAddHistoryEntry : IBookmarkHistoryEntry
{
    private readonly BookmarkNode _node;
    private readonly BookmarkFolder _parent;
    private readonly int _index;
    private readonly int _urlCount;
    private readonly int _folderCount;

    public BookmarkAddHistoryEntry(
        BookmarkNode node,
        BookmarkFolder parent,
        int index)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _index = index;

        (_urlCount, _folderCount, Description) = node switch
        {
            BookmarkUrl => (1, 0, "Add bookmark"),
            BookmarkFolder => (0, 1, "Add folder"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(node),
                node.Kind,
                "Unsupported bookmark node kind.")
        };
    }

    public string Description { get; }

    public BookmarkHistoryImpact Impact =>
        BookmarkHistoryImpact.SearchRelevant;

    public void Undo(BookmarkDocument document) =>
        BookmarkHistoryMutation.DetachWithCounts(
            document,
            _node,
            _parent,
            _index,
            _urlCount,
            _folderCount);

    public void Redo(BookmarkDocument document) =>
        BookmarkHistoryMutation.RestoreDetachedWithCounts(
            document,
            _node,
            _parent,
            _index,
            _urlCount,
            _folderCount);
}
