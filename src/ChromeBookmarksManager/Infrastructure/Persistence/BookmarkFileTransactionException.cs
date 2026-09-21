using System.IO;

namespace ChromeBookmarksManager.Infrastructure.Persistence;

public sealed class BookmarkFileTransactionException : IOException
{
    public BookmarkFileTransactionException(
        BookmarkFileTransactionError error,
        string message,
        string? backupPath = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
        BackupPath = backupPath;
    }

    public BookmarkFileTransactionError Error { get; }

    public string? BackupPath { get; }
}
