using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

public sealed class BookmarkRenameHistoryEntry : IBookmarkHistoryEntry
{
    private readonly BookmarkNode _node;
    private readonly string _oldName;
    private readonly string _newName;

    public BookmarkRenameHistoryEntry(
        BookmarkNode node,
        string oldName,
        string newName)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
        _oldName = oldName ?? throw new ArgumentNullException(nameof(oldName));
        _newName = newName ?? throw new ArgumentNullException(nameof(newName));

        if (string.Equals(_oldName, _newName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A rename history entry requires two different values.",
                nameof(newName));
        }

        Description = node is BookmarkFolder
            ? "Rename folder"
            : "Rename bookmark";
    }

    public string Description { get; }

    public BookmarkHistoryImpact Impact =>
        BookmarkHistoryImpact.SearchRelevant;

    public void Undo(BookmarkDocument document)
    {
        BookmarkHistoryMutation.EnsureBelongsToDocument(document, _node);
        EnsureCurrentName(_newName);
        _node.SetName(_oldName);
    }

    public void Redo(BookmarkDocument document)
    {
        BookmarkHistoryMutation.EnsureBelongsToDocument(document, _node);
        EnsureCurrentName(_oldName);
        _node.SetName(_newName);
    }

    private void EnsureCurrentName(string expected)
    {
        if (!string.Equals(_node.Name, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The bookmark name no longer matches the recorded history state.");
        }
    }
}
