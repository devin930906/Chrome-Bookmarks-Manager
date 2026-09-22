namespace ChromeBookmarksManager.Application.Clipboard;

public interface IBookmarkClipboardStore
{
    bool HasPayload { get; }

    BookmarkClipboardPayload? GetPayload();

    void SetPayload(BookmarkClipboardPayload payload);

    void Clear();
}
