namespace ChromeBookmarksManager.Application.Saving;

public sealed class ChromeBookmarksSaveException : InvalidOperationException
{
    public ChromeBookmarksSaveException(
        ChromeBookmarksSaveError error,
        string message,
        string? backupPath = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
        BackupPath = backupPath;
    }

    public ChromeBookmarksSaveError Error { get; }

    public string? BackupPath { get; }

    public bool HasVerifiedRecoveryBackup =>
        !string.IsNullOrWhiteSpace(BackupPath) &&
        Error is ChromeBookmarksSaveError.AtomicReplacementFailed or
            ChromeBookmarksSaveError.RecoveryRequired;
}
