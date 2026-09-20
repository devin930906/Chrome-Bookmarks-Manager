namespace ChromeBookmarksManager.Application.Moving;

public sealed class BookmarkMoveException : InvalidOperationException
{
    public BookmarkMoveException(
        BookmarkMoveError error,
        string message)
        : base(message)
    {
        Error = error;
    }

    public BookmarkMoveError Error { get; }
}
