namespace ChromeBookmarksManager.Infrastructure.Persistence;

public enum BookmarkSourceBaselineError
{
    FileNotFound,
    AccessDenied,
    SourceChanged,
    IoFailure
}
