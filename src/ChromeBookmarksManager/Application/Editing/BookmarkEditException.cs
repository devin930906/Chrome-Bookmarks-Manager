namespace ChromeBookmarksManager.Application.Editing;

public sealed class BookmarkEditException : InvalidOperationException
{
    public BookmarkEditException(BookmarkEditError error, string message)
        : base(message)
    {
        Error = error;
    }

    public BookmarkEditError Error { get; }
}
