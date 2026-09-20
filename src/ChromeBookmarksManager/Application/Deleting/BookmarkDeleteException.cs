namespace ChromeBookmarksManager.Application.Deleting;

public sealed class BookmarkDeleteException : InvalidOperationException
{
    public BookmarkDeleteException(
        BookmarkDeleteError error,
        string message)
        : base(message)
    {
        Error = error;
    }

    public BookmarkDeleteError Error { get; }
}
