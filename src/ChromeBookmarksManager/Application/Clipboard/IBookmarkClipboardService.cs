using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Clipboard;

public interface IBookmarkClipboardService
{
    BookmarkClipboardPayload Capture(
        BookmarkDocument document,
        IReadOnlyList<BookmarkNode> nodes,
        BookmarkClipboardMode mode);

    BookmarkClipboardPasteResult Paste(
        BookmarkDocument document,
        BookmarkClipboardPayload payload,
        BookmarkFolder targetParent,
        int targetIndex);
}
