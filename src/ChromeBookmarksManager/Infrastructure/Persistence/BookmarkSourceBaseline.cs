namespace ChromeBookmarksManager.Infrastructure.Persistence;

public sealed record BookmarkSourceBaseline(
    string FullPath,
    string Sha256,
    long Length,
    DateTime LastWriteTimeUtc);
