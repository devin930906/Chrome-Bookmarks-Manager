using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Infrastructure.Persistence;

public interface IBookmarkFileTransaction
{
    Task<BookmarkFileTransactionResult> ExecuteAsync(
        BookmarkDocument document,
        BookmarkSourceBaseline expectedBaseline,
        CancellationToken cancellationToken = default);

    Task<BookmarkFileTransactionResult> ExecuteAsync(
        BookmarkDocument document,
        BookmarkSourceBaseline expectedBaseline,
        Func<CancellationToken, Task> beforeReplaceGuard,
        CancellationToken cancellationToken = default);
}
