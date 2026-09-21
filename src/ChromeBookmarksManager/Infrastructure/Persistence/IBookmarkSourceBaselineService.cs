namespace ChromeBookmarksManager.Infrastructure.Persistence;

public interface IBookmarkSourceBaselineService
{
    Task<BookmarkSourceBaseline> CaptureAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task<BookmarkSourceBaseline> VerifyUnchangedAsync(
        BookmarkSourceBaseline expected,
        CancellationToken cancellationToken = default);
}
