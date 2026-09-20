using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Infrastructure.Persistence;

public interface IBookmarkFileTransaction
{
    Task<BookmarkFileTransactionResult> ExecuteAsync(
        BookmarkDocument document,
        BookmarkSourceBaseline expectedBaseline,
        CancellationToken cancellationToken = default);
}
