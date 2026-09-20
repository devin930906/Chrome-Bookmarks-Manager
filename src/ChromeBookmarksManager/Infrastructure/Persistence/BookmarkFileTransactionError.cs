namespace ChromeBookmarksManager.Infrastructure.Persistence;

public enum BookmarkFileTransactionError
{
    TempWriteFailed,
    TempValidationFailed,
    BackupCreationFailed,
    BackupVerificationFailed,
    SourceChanged,
    AtomicReplaceFailed,
    PostWriteValidationFailed,
    CleanupFailed
}
