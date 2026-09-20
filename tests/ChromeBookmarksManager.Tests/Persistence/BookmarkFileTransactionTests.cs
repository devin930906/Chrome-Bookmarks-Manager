using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Infrastructure.Persistence;

namespace ChromeBookmarksManager.Tests.Persistence;

public sealed class BookmarkFileTransactionTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"cbm-v09-{Guid.NewGuid():N}");

    public BookmarkFileTransactionTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task ExecuteAsync_ValidDocument_CreatesVerifiedBackupAndReplacesSource()
    {
        var source = await CreateSourceAsync();
        var reader = new ChromeBookmarksReader();
        var original = await reader.ReadFileAsync(source);
        var baselineService = new BookmarkSourceBaselineService();
        var baseline = await baselineService.CaptureAsync(source);
        var document = await reader.ReadFileAsync(source);

        var editing = new ChromeBookmarksManager.Application.Editing.BookmarkEditingService(
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero)));
        editing.AddBookmark(
            document,
            document.Roots.Other,
            "Saved by V0.9",
            "https://example.com/v09");

        var transaction = new BookmarkFileTransaction(
            new ChromeBookmarksWriter(),
            reader,
            baselineService,
            new BookmarkFileSystem(),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 20, 12, 34, 56, 789, TimeSpan.Zero)));

        var result = await transaction.ExecuteAsync(document, baseline);

        Assert.True(File.Exists(result.BackupPath));
        Assert.Equal(baseline.Sha256, result.BackupBaseline.Sha256);
        Assert.Equal(baseline.Length, result.BackupBaseline.Length);
        Assert.NotEqual(baseline.Sha256, result.FinalBaseline.Sha256);

        var saved = await reader.ReadFileAsync(source);
        Assert.Equal(document.UrlCount, saved.UrlCount);
        Assert.Equal(document.FolderCount, saved.FolderCount);
        Assert.Equal(
            ChromeBookmarksChecksum.Compute(document),
            ChromeBookmarksChecksum.Compute(saved));

        var backup = await reader.ReadFileAsync(result.BackupPath);
        Assert.Equal(original.UrlCount, backup.UrlCount);
        Assert.Equal(original.FolderCount, backup.FolderCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenWriterFails_LeavesSourceBytesUnchanged()
    {
        var source = await CreateSourceAsync();
        var before = await File.ReadAllBytesAsync(source);
        var reader = new ChromeBookmarksReader();
        var document = await reader.ReadFileAsync(source);
        var baselineService = new BookmarkSourceBaselineService();
        var baseline = await baselineService.CaptureAsync(source);

        var transaction = new BookmarkFileTransaction(
            new ThrowingWriter(),
            reader,
            baselineService,
            new BookmarkFileSystem(),
            TimeProvider.System);

        var error = await Assert.ThrowsAsync<BookmarkFileTransactionException>(
            () => transaction.ExecuteAsync(document, baseline));

        Assert.Equal(BookmarkFileTransactionError.TempWriteFailed, error.Error);
        Assert.Equal(before, await File.ReadAllBytesAsync(source));
        Assert.Null(error.BackupPath);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTempValidationFails_LeavesSourceBytesUnchanged()
    {
        var source = await CreateSourceAsync();
        var before = await File.ReadAllBytesAsync(source);
        var realReader = new ChromeBookmarksReader();
        var document = await realReader.ReadFileAsync(source);
        var baselineService = new BookmarkSourceBaselineService();
        var baseline = await baselineService.CaptureAsync(source);

        var transaction = new BookmarkFileTransaction(
            new ChromeBookmarksWriter(),
            new ThrowingReader(),
            baselineService,
            new BookmarkFileSystem(),
            TimeProvider.System);

        var error = await Assert.ThrowsAsync<BookmarkFileTransactionException>(
            () => transaction.ExecuteAsync(document, baseline));

        Assert.Equal(BookmarkFileTransactionError.TempValidationFailed, error.Error);
        Assert.Equal(before, await File.ReadAllBytesAsync(source));
        Assert.Null(error.BackupPath);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAtomicReplaceFails_PreservesVerifiedBackupAndOriginalSource()
    {
        var source = await CreateSourceAsync();
        var before = await File.ReadAllBytesAsync(source);
        var reader = new ChromeBookmarksReader();
        var document = await reader.ReadFileAsync(source);
        var baselineService = new BookmarkSourceBaselineService();
        var baseline = await baselineService.CaptureAsync(source);

        var transaction = new BookmarkFileTransaction(
            new ChromeBookmarksWriter(),
            reader,
            baselineService,
            new ReplaceFailingFileSystem(),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 20, 12, 34, 56, 789, TimeSpan.Zero)));

        var error = await Assert.ThrowsAsync<BookmarkFileTransactionException>(
            () => transaction.ExecuteAsync(document, baseline));

        Assert.Equal(BookmarkFileTransactionError.AtomicReplaceFailed, error.Error);
        Assert.NotNull(error.BackupPath);
        Assert.True(File.Exists(error.BackupPath));
        Assert.Equal(before, await File.ReadAllBytesAsync(source));

        var backupBaseline = await baselineService.CaptureAsync(error.BackupPath!);
        Assert.Equal(baseline.Sha256, backupBaseline.Sha256);
        Assert.Equal(baseline.Length, backupBaseline.Length);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSourceChangesBeforeReplace_RefusesOverwrite()
    {
        var source = await CreateSourceAsync();
        var reader = new ChromeBookmarksReader();
        var document = await reader.ReadFileAsync(source);
        var baselineService = new BookmarkSourceBaselineService();
        var baseline = await baselineService.CaptureAsync(source);
        var fileSystem = new MutatingAfterBackupFileSystem(source);

        var transaction = new BookmarkFileTransaction(
            new ChromeBookmarksWriter(),
            reader,
            baselineService,
            fileSystem,
            TimeProvider.System);

        var error = await Assert.ThrowsAsync<BookmarkFileTransactionException>(
            () => transaction.ExecuteAsync(document, baseline));

        Assert.Equal(BookmarkFileTransactionError.SourceChanged, error.Error);
        Assert.NotNull(error.BackupPath);
        Assert.Contains("external-change", await File.ReadAllTextAsync(source));
    }

    private async Task<string> CreateSourceAsync()
    {
        var source = Path.Combine(_directory, "Bookmarks");
        var fixture = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Bookmarks.sample.json");
        await using var input = File.OpenRead(fixture);
        await using var output = File.Create(source);
        await input.CopyToAsync(output);
        return source;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class ThrowingWriter : IChromeBookmarksWriter
    {
        public Task<ChromeBookmarksChecksums> WriteAsync(
            ChromeBookmarksManager.Domain.BookmarkDocument document,
            Stream destination,
            CancellationToken cancellationToken = default) =>
            throw new IOException("Synthetic writer failure.");
    }

    private sealed class ThrowingReader : IChromeBookmarksReader
    {
        public Task<ChromeBookmarksManager.Domain.BookmarkDocument> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            throw new ChromeBookmarksReadException(
                ChromeBookmarksReadError.MalformedJson,
                "Synthetic validation failure.");
    }

    private sealed class ReplaceFailingFileSystem : BookmarkFileSystem
    {
        public override void Replace(string sourceFileName, string destinationFileName)
        {
            throw new IOException("Synthetic atomic replacement failure.");
        }
    }

    private sealed class MutatingAfterBackupFileSystem(string sourcePath) : BookmarkFileSystem
    {
        private int _copyCount;

        public override void Copy(string sourceFileName, string destinationFileName)
        {
            base.Copy(sourceFileName, destinationFileName);
            _copyCount++;

            if (_copyCount == 1)
            {
                File.AppendAllText(sourcePath, "\nexternal-change");
                File.SetLastWriteTimeUtc(
                    sourcePath,
                    File.GetLastWriteTimeUtc(sourcePath).AddSeconds(1));
            }
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
