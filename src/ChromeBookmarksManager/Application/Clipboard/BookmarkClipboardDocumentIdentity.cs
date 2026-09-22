namespace ChromeBookmarksManager.Application.Clipboard;

public sealed record BookmarkClipboardDocumentIdentity(
    Guid BookmarkBarGuid,
    Guid OtherGuid,
    Guid SyncedGuid);
