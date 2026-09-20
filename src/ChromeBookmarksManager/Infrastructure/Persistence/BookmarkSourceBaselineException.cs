namespace ChromeBookmarksManager.Infrastructure.Persistence;

public sealed class BookmarkSourceBaselineException : IOException
{
    public BookmarkSourceBaselineException(
        BookmarkSourceBaselineError error,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    public BookmarkSourceBaselineError Error { get; }
}
