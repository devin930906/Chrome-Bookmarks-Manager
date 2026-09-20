namespace ChromeBookmarksManager.Infrastructure.Persistence;

public sealed record BookmarkFileTransactionResult(
    string BackupPath,
    BookmarkSourceBaseline BackupBaseline,
    BookmarkSourceBaseline FinalBaseline);
