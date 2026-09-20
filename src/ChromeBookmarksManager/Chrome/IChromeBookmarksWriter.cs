using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Chrome;

public interface IChromeBookmarksWriter
{
    Task<ChromeBookmarksChecksums> WriteAsync(
        BookmarkDocument document,
        Stream destination,
        CancellationToken cancellationToken = default);
}
