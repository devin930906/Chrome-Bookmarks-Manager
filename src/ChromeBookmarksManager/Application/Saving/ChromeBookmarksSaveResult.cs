using ChromeBookmarksManager.Infrastructure.Persistence;

namespace ChromeBookmarksManager.Application.Saving;

public sealed record ChromeBookmarksSaveResult(
    string BackupPath,
    BookmarkSourceBaseline FinalBaseline);
