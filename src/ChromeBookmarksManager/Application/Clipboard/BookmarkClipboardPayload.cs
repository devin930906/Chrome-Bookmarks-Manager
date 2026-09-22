namespace ChromeBookmarksManager.Application.Clipboard;

public sealed record BookmarkClipboardPayload(
    BookmarkClipboardMode Mode,
    BookmarkClipboardDocumentIdentity SourceDocument,
    IReadOnlyList<BookmarkClipboardNodeSnapshot> Items,
    IReadOnlyList<Guid> SourceNodeGuids);
