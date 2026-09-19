using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Chrome;

public sealed partial class ChromeBookmarksReader : IChromeBookmarksReader
{
    public async Task<BookmarkDocument> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException exception)
        {
            throw new ChromeBookmarksReadException(ChromeBookmarksReadError.FileNotFound, "The selected Bookmarks file no longer exists.", innerException: exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new ChromeBookmarksReadException(ChromeBookmarksReadError.FileNotFound, "The selected Bookmarks file no longer exists.", innerException: exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ChromeBookmarksReadException(ChromeBookmarksReadError.AccessDenied, "Windows denied access to the selected Bookmarks file.", innerException: exception);
        }
        catch (IOException exception)
        {
            throw new ChromeBookmarksReadException(ChromeBookmarksReadError.IoFailure, "The selected Bookmarks file could not be read.", innerException: exception);
        }
    }

    internal Task<BookmarkDocument> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        return ReadCoreAsync(stream, cancellationToken);
    }

    private static Task<BookmarkDocument> ReadCoreAsync(Stream stream, CancellationToken cancellationToken) =>
        Task.FromException<BookmarkDocument>(new ChromeBookmarksReadException(ChromeBookmarksReadError.MalformedJson, "Reader mapping is not available."));
}
