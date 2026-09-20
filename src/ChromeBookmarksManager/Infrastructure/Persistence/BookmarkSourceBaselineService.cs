using System.Security.Cryptography;

namespace ChromeBookmarksManager.Infrastructure.Persistence;

public sealed class BookmarkSourceBaselineService :
    IBookmarkSourceBaselineService
{
    public async Task<BookmarkSourceBaseline> CaptureAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);

        try
        {
            var before = new FileInfo(fullPath);
            if (!before.Exists)
            {
                throw new BookmarkSourceBaselineException(
                    BookmarkSourceBaselineError.FileNotFound,
                    "The Chrome Bookmarks source file no longer exists.");
            }

            var expectedLength = before.Length;
            var expectedLastWriteUtc = before.LastWriteTimeUtc;

            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 128 * 1024,
                options: FileOptions.Asynchronous |
                         FileOptions.SequentialScan);

            var hash = await SHA256
                .HashDataAsync(stream, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            var after = new FileInfo(fullPath);
            if (!after.Exists)
            {
                throw new BookmarkSourceBaselineException(
                    BookmarkSourceBaselineError.SourceChanged,
                    "The Chrome Bookmarks source changed while its baseline was being captured.");
            }

            if (after.Length != expectedLength ||
                after.LastWriteTimeUtc != expectedLastWriteUtc)
            {
                throw new BookmarkSourceBaselineException(
                    BookmarkSourceBaselineError.SourceChanged,
                    "The Chrome Bookmarks source changed while its baseline was being captured.");
            }

            return new BookmarkSourceBaseline(
                fullPath,
                Convert.ToHexString(hash).ToLowerInvariant(),
                after.Length,
                after.LastWriteTimeUtc);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BookmarkSourceBaselineException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new BookmarkSourceBaselineException(
                BookmarkSourceBaselineError.AccessDenied,
                "Access to the Chrome Bookmarks source file was denied.",
                exception);
        }
        catch (FileNotFoundException exception)
        {
            throw new BookmarkSourceBaselineException(
                BookmarkSourceBaselineError.FileNotFound,
                "The Chrome Bookmarks source file no longer exists.",
                exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new BookmarkSourceBaselineException(
                BookmarkSourceBaselineError.FileNotFound,
                "The Chrome Bookmarks source directory no longer exists.",
                exception);
        }
        catch (IOException exception)
        {
            throw new BookmarkSourceBaselineException(
                BookmarkSourceBaselineError.IoFailure,
                "The Chrome Bookmarks source file could not be read safely.",
                exception);
        }
    }

    public async Task<BookmarkSourceBaseline> VerifyUnchangedAsync(
        BookmarkSourceBaseline expected,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        cancellationToken.ThrowIfCancellationRequested();

        BookmarkSourceBaseline current;
        try
        {
            current = await CaptureAsync(
                    expected.FullPath,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (BookmarkSourceBaselineException exception)
            when (exception.Error == BookmarkSourceBaselineError.SourceChanged)
        {
            throw;
        }

        if (!string.Equals(
                Path.GetFullPath(expected.FullPath),
                current.FullPath,
                StringComparison.OrdinalIgnoreCase) ||
            expected.Length != current.Length ||
            expected.LastWriteTimeUtc != current.LastWriteTimeUtc ||
            !string.Equals(
                expected.Sha256,
                current.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BookmarkSourceBaselineException(
                BookmarkSourceBaselineError.SourceChanged,
                "The Chrome Bookmarks source changed outside the application. Reload it before saving.");
        }

        return current;
    }
}
