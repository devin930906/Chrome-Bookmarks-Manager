using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Chrome;

public interface IChromeBookmarksReader
{
    Task<BookmarkDocument> ReadFileAsync(string path, CancellationToken cancellationToken = default);
}
