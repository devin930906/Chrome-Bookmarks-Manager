namespace ChromeBookmarksManager.Application.Clipboard;

public sealed class InMemoryBookmarkClipboardStore
    : IBookmarkClipboardStore
{
    private BookmarkClipboardPayload? _payload;

    public bool HasPayload => _payload is not null;

    public BookmarkClipboardPayload? GetPayload() =>
        _payload;

    public void SetPayload(BookmarkClipboardPayload payload)
    {
        _payload = payload
            ?? throw new ArgumentNullException(nameof(payload));
    }

    public void Clear() =>
        _payload = null;
}
