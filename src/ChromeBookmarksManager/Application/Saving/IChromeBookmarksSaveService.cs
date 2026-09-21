using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.Infrastructure.Persistence;

namespace ChromeBookmarksManager.Application.Saving;

public interface IChromeBookmarksSaveService
{
    Task<ChromeBookmarksSaveResult> SaveAsync(
        BookmarkDocument? document,
        BookmarkSourceBaseline? sourceBaseline,
        CancellationToken cancellationToken = default);
}
