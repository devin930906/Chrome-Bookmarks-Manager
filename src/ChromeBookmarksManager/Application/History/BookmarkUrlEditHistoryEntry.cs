using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

public sealed class BookmarkUrlEditHistoryEntry : IBookmarkHistoryEntry
{
    private readonly BookmarkUrl _bookmark;
    private readonly string _oldUrl;
    private readonly string _newUrl;

    public BookmarkUrlEditHistoryEntry(
        BookmarkUrl bookmark,
        string oldUrl,
        string newUrl)
    {
        _bookmark = bookmark
            ?? throw new ArgumentNullException(nameof(bookmark));
        _oldUrl = oldUrl
            ?? throw new ArgumentNullException(nameof(oldUrl));
        _newUrl = newUrl
            ?? throw new ArgumentNullException(nameof(newUrl));

        if (string.Equals(_oldUrl, _newUrl, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A URL history entry requires two different values.",
                nameof(newUrl));
        }
    }

    public string Description => "Edit bookmark URL";

    public BookmarkHistoryImpact Impact =>
        BookmarkHistoryImpact.SearchRelevant;

    public void Undo(BookmarkDocument document)
    {
        BookmarkHistoryMutation.EnsureBelongsToDocument(
            document,
            _bookmark);
        EnsureCurrentUrl(_newUrl);
        _bookmark.SetUrl(_oldUrl);
    }

    public void Redo(BookmarkDocument document)
    {
        BookmarkHistoryMutation.EnsureBelongsToDocument(
            document,
            _bookmark);
        EnsureCurrentUrl(_oldUrl);
        _bookmark.SetUrl(_newUrl);
    }

    private void EnsureCurrentUrl(string expected)
    {
        if (!string.Equals(
                _bookmark.Url,
                expected,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The bookmark URL no longer matches the recorded history state.");
        }
    }
}
