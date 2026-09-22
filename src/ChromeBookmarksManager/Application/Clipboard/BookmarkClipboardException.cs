namespace ChromeBookmarksManager.Application.Clipboard;

public sealed class BookmarkClipboardException : Exception
{
    public BookmarkClipboardException(
        BookmarkClipboardError error,
        string message)
        : base(message)
    {
        Error = error;
    }

    public BookmarkClipboardError Error { get; }
}
