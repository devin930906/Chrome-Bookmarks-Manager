using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.ViewModels;

public sealed class BookmarkListItemViewModel
{
    public BookmarkListItemViewModel(BookmarkNode node)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public BookmarkNode Node { get; }

    public string Name => Node.Name;

    public string Url => Node is BookmarkUrl bookmark
        ? bookmark.Url
        : string.Empty;

    public BookmarkNodeKind Kind => Node.Kind;

    public bool IsFolder => Node is BookmarkFolder;

    public bool IsBookmark => Node is BookmarkUrl;
}
