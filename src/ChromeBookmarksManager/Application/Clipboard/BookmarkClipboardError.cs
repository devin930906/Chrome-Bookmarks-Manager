namespace ChromeBookmarksManager.Application.Clipboard;

public enum BookmarkClipboardError
{
    InvalidPayload,
    NodeNotInDocument,
    ProtectedRoot,
    SourceDocumentMismatch,
    SourceNodeMissing,
    InvalidTargetIndex
}
