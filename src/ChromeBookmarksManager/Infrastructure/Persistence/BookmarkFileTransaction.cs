using System.Globalization;
using System.IO;
using System.Text.Json;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Infrastructure.Persistence;

public sealed class BookmarkFileTransaction : IBookmarkFileTransaction
{
    private readonly IChromeBookmarksWriter _writer;
    private readonly IChromeBookmarksReader _reader;
    private readonly IBookmarkSourceBaselineService _baselineService;
    private readonly BookmarkFileSystem _fileSystem;
    private readonly TimeProvider _timeProvider;

    public BookmarkFileTransaction(
        IChromeBookmarksWriter writer,
        IChromeBookmarksReader reader,
        IBookmarkSourceBaselineService baselineService,
        BookmarkFileSystem fileSystem,
        TimeProvider timeProvider)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _baselineService = baselineService
            ?? throw new ArgumentNullException(nameof(baselineService));
        _fileSystem = fileSystem
            ?? throw new ArgumentNullException(nameof(fileSystem));
        _timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<BookmarkFileTransactionResult> ExecuteAsync(
        BookmarkDocument document,
        BookmarkSourceBaseline expectedBaseline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(expectedBaseline);
        cancellationToken.ThrowIfCancellationRequested();

        var sourcePath = Path.GetFullPath(expectedBaseline.FullPath);
        var directory = Path.GetDirectoryName(sourcePath)
            ?? throw new BookmarkFileTransactionException(
                BookmarkFileTransactionError.TempWriteFailed,
                "The Chrome Bookmarks source directory could not be resolved.");

        var tempPath = CreateUniqueTempPath(directory, Path.GetFileName(sourcePath));
        string? backupPath = null;
        var replacementCompleted = false;

        try
        {
            await VerifyExpectedSourceAsync(
                    expectedBaseline,
                    backupPath,
                    cancellationToken)
                .ConfigureAwait(false);

            await WriteTempAsync(
                    document,
                    tempPath,
                    cancellationToken)
                .ConfigureAwait(false);

            var tempDocument = await ReadValidatedDocumentAsync(
                    tempPath,
                    BookmarkFileTransactionError.TempValidationFailed,
                    backupPath,
                    cancellationToken)
                .ConfigureAwait(false);

            EnsureEquivalent(
                document,
                tempDocument,
                BookmarkFileTransactionError.TempValidationFailed,
                backupPath);

            backupPath = CreateUniqueBackupPath(
                directory,
                Path.GetFileName(sourcePath));

            try
            {
                _fileSystem.Copy(sourcePath, backupPath);
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                throw new BookmarkFileTransactionException(
                    BookmarkFileTransactionError.BackupCreationFailed,
                    "A verified safety backup could not be created before replacing the Chrome Bookmarks file.",
                    backupPath,
                    exception);
            }

            BookmarkSourceBaseline backupBaseline;
            try
            {
                backupBaseline = await _baselineService
                    .CaptureAsync(backupPath, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
                when (exception is BookmarkSourceBaselineException)
            {
                throw new BookmarkFileTransactionException(
                    BookmarkFileTransactionError.BackupVerificationFailed,
                    "The safety backup could not be verified.",
                    backupPath,
                    exception);
            }

            if (backupBaseline.Length != expectedBaseline.Length ||
                !string.Equals(
                    backupBaseline.Sha256,
                    expectedBaseline.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new BookmarkFileTransactionException(
                    BookmarkFileTransactionError.BackupVerificationFailed,
                    "The safety backup does not match the source file that was loaded.",
                    backupPath);
            }

            // Critical stale-source check immediately before replacement.
            await VerifyExpectedSourceAsync(
                    expectedBaseline,
                    backupPath,
                    cancellationToken)
                .ConfigureAwait(false);

            try
            {
                _fileSystem.Replace(tempPath, sourcePath);
                replacementCompleted = true;
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                throw new BookmarkFileTransactionException(
                    BookmarkFileTransactionError.AtomicReplaceFailed,
                    "The Chrome Bookmarks file could not be replaced atomically. The verified backup was retained.",
                    backupPath,
                    exception);
            }

            var finalDocument = await ReadValidatedDocumentAsync(
                    sourcePath,
                    BookmarkFileTransactionError.PostWriteValidationFailed,
                    backupPath,
                    CancellationToken.None)
                .ConfigureAwait(false);

            EnsureEquivalent(
                document,
                finalDocument,
                BookmarkFileTransactionError.PostWriteValidationFailed,
                backupPath);

            BookmarkSourceBaseline finalBaseline;
            try
            {
                finalBaseline = await _baselineService
                    .CaptureAsync(sourcePath, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (BookmarkSourceBaselineException exception)
            {
                throw new BookmarkFileTransactionException(
                    BookmarkFileTransactionError.PostWriteValidationFailed,
                    "The replaced Chrome Bookmarks file could not be re-baselined after validation. Recovery from the verified backup may be required.",
                    backupPath,
                    exception);
            }

            return new BookmarkFileTransactionResult(
                backupPath,
                backupBaseline,
                finalBaseline);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        finally
        {
            if (!replacementCompleted &&
                _fileSystem.Exists(tempPath))
            {
                try
                {
                    _fileSystem.Delete(tempPath);
                }
                catch
                {
                    // A failed temp cleanup must never trigger a second,
                    // more dangerous filesystem operation or hide the
                    // original transaction failure. Task 10 safety gates
                    // additionally verify no direct-source bypass exists.
                }
            }
        }
    }

    private async Task WriteTempAsync(
        BookmarkDocument document,
        string tempPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = _fileSystem.CreateWriteStream(tempPath);

            await _writer
                .WriteAsync(document, stream, cancellationToken)
                .ConfigureAwait(false);

            await stream
                .FlushAsync(cancellationToken)
                .ConfigureAwait(false);

            if (stream is FileStream fileStream)
            {
                fileStream.Flush(flushToDisk: true);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  InvalidOperationException or
                  NotSupportedException)
        {
            throw new BookmarkFileTransactionException(
                BookmarkFileTransactionError.TempWriteFailed,
                "The new Chrome Bookmarks content could not be written safely to a temporary file.",
                innerException: exception);
        }
    }

    private async Task<BookmarkDocument> ReadValidatedDocumentAsync(
        string path,
        BookmarkFileTransactionError error,
        string? backupPath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _reader
                .ReadFileAsync(path, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is ChromeBookmarksReadException or
                  IOException or
                  UnauthorizedAccessException)
        {
            throw new BookmarkFileTransactionException(
                error,
                error == BookmarkFileTransactionError.TempValidationFailed
                    ? "The temporary Chrome Bookmarks file failed validation; the source was not replaced."
                    : "The replaced Chrome Bookmarks file failed final validation. Recovery from the verified backup may be required.",
                backupPath,
                exception);
        }
    }

    private async Task VerifyExpectedSourceAsync(
        BookmarkSourceBaseline expectedBaseline,
        string? backupPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await _baselineService
                .VerifyUnchangedAsync(expectedBaseline, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BookmarkSourceBaselineException exception)
        {
            throw new BookmarkFileTransactionException(
                exception.Error == BookmarkSourceBaselineError.SourceChanged
                    ? BookmarkFileTransactionError.SourceChanged
                    : BookmarkFileTransactionError.BackupVerificationFailed,
                exception.Error == BookmarkSourceBaselineError.SourceChanged
                    ? "The Chrome Bookmarks source changed outside the application. It was not overwritten."
                    : "The Chrome Bookmarks source could not be reverified safely before replacement.",
                backupPath,
                exception);
        }
    }

    private string CreateUniqueBackupPath(
        string directory,
        string sourceFileName)
    {
        var timestamp = _timeProvider
            .GetUtcNow()
            .UtcDateTime
            .ToString(
                "yyyyMMdd-HHmmss.fff",
                CultureInfo.InvariantCulture);

        var stem = Path.Combine(
            directory,
            $"{sourceFileName}.ChromeBookmarksManager.{timestamp}");

        for (var suffix = 0; ; suffix++)
        {
            var path = suffix == 0
                ? $"{stem}.bak"
                : $"{stem}.{suffix}.bak";

            if (!_fileSystem.Exists(path))
            {
                return path;
            }
        }
    }

    private string CreateUniqueTempPath(
        string directory,
        string sourceFileName)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var path = Path.Combine(
                directory,
                $".{sourceFileName}.ChromeBookmarksManager.{Guid.NewGuid():N}.tmp");

            if (!_fileSystem.Exists(path))
            {
                return path;
            }
        }

        throw new BookmarkFileTransactionException(
            BookmarkFileTransactionError.TempWriteFailed,
            "A unique temporary Chrome Bookmarks path could not be allocated.");
    }

    private static void EnsureEquivalent(
        BookmarkDocument expected,
        BookmarkDocument actual,
        BookmarkFileTransactionError error,
        string? backupPath)
    {
        if (expected.Version != actual.Version ||
            expected.FolderCount != actual.FolderCount ||
            expected.UrlCount != actual.UrlCount ||
            ChromeBookmarksChecksum.Compute(expected) !=
            ChromeBookmarksChecksum.Compute(actual) ||
            !RootsEquivalent(expected.Roots, actual.Roots) ||
            !JsonDictionaryEquivalent(
                expected.ExtensionData,
                actual.ExtensionData) ||
            !JsonDictionaryEquivalent(
                expected.Roots.ExtensionData,
                actual.Roots.ExtensionData))
        {
            throw new BookmarkFileTransactionException(
                error,
                error == BookmarkFileTransactionError.TempValidationFailed
                    ? "The temporary Chrome Bookmarks file is not logically equivalent to the in-memory document."
                    : "The final Chrome Bookmarks file is not logically equivalent to the in-memory document. Recovery from the verified backup may be required.",
                backupPath);
        }
    }

    private static bool RootsEquivalent(
        BookmarkRoots expected,
        BookmarkRoots actual) =>
        NodeEquivalent(expected.BookmarkBar, actual.BookmarkBar) &&
        NodeEquivalent(expected.Other, actual.Other) &&
        NodeEquivalent(expected.Synced, actual.Synced);

    private static bool NodeEquivalent(
        BookmarkNode expected,
        BookmarkNode actual)
    {
        if (expected.GetType() != actual.GetType() ||
            !string.Equals(expected.Id, actual.Id, StringComparison.Ordinal) ||
            expected.Guid != actual.Guid ||
            !string.Equals(expected.Name, actual.Name, StringComparison.Ordinal) ||
            !string.Equals(
                expected.DateAddedRaw,
                actual.DateAddedRaw,
                StringComparison.Ordinal) ||
            !string.Equals(
                expected.DateModifiedRaw,
                actual.DateModifiedRaw,
                StringComparison.Ordinal) ||
            !string.Equals(
                expected.DateLastUsedRaw,
                actual.DateLastUsedRaw,
                StringComparison.Ordinal) ||
            !JsonEquivalent(expected.MetaInfo, actual.MetaInfo) ||
            !JsonDictionaryEquivalent(
                expected.ExtensionData,
                actual.ExtensionData))
        {
            return false;
        }

        if (expected is BookmarkUrl expectedUrl)
        {
            return actual is BookmarkUrl actualUrl &&
                   string.Equals(
                       expectedUrl.Url,
                       actualUrl.Url,
                       StringComparison.Ordinal);
        }

        if (expected is not BookmarkFolder expectedFolder ||
            actual is not BookmarkFolder actualFolder ||
            expectedFolder.Children.Count != actualFolder.Children.Count)
        {
            return false;
        }

        for (var index = 0;
             index < expectedFolder.Children.Count;
             index++)
        {
            if (!NodeEquivalent(
                    expectedFolder.Children[index],
                    actualFolder.Children[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool JsonDictionaryEquivalent(
        IReadOnlyDictionary<string, JsonElement> expected,
        IReadOnlyDictionary<string, JsonElement> actual)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var value) ||
                !JsonElement.DeepEquals(pair.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool JsonEquivalent(
        JsonElement? expected,
        JsonElement? actual)
    {
        if (expected.HasValue != actual.HasValue)
        {
            return false;
        }

        return !expected.HasValue ||
               JsonElement.DeepEquals(
                   expected.Value,
                   actual!.Value);
    }
}
