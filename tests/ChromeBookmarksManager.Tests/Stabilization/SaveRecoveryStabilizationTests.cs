using System.Text.Json;
using ChromeBookmarksManager.Application.Saving;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.Infrastructure.Persistence;
using ChromeBookmarksManager.Infrastructure.Processes;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Stabilization;

public sealed class SaveRecoveryStabilizationTests
{
    [Fact]
    public async Task RecoveryRequired_BlocksDirectRetryUntilDocumentIsReloadedOrRecovered()
    {
        var document = CreateDocument();
        var baseline = CreateBaseline();
        var backupPath = baseline.FullPath + ".verified-recovery.bak";
        var saveService = new ControlledSaveService
        {
            Exception = new ChromeBookmarksSaveException(
                ChromeBookmarksSaveError.RecoveryRequired,
                "Synthetic post-replacement validation failure.",
                backupPath)
        };
        var viewModel = new MainViewModel(
            new StubReader(document),
            new BookmarkSearchService(),
            new FixedBaselineService(baseline),
            saveService,
            TimeSpan.Zero);

        await viewModel.LoadBookmarksAsync(baseline.FullPath);
        await viewModel.AddFolderAsync("Unsaved");

        var error = await Assert.ThrowsAsync<ChromeBookmarksSaveException>(
            () => viewModel.SaveAsync());

        Assert.Equal(ChromeBookmarksSaveError.RecoveryRequired, error.Error);
        Assert.Equal("RecoveryRequired", viewModel.State.ToString());
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.CanBrowseDocument);
        Assert.False(viewModel.CanSave);
        Assert.Equal(1, saveService.CallCount);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.SaveAsync());

        Assert.Equal(
            1,
            saveService.CallCount);
    }

    [Fact]
    public async Task SaveService_FinalBaselineChangesAfterTransaction_IsRecoveryRequired()
    {
        var expected = CreateBaseline();
        var final = expected with
        {
            Sha256 = new string('b', 64),
            Length = expected.Length + 12,
            LastWriteTimeUtc = expected.LastWriteTimeUtc.AddSeconds(1)
        };
        var backupPath = expected.FullPath + ".verified-recovery.bak";
        var baselineService = new FinalConfirmationBaselineService();
        var transaction = new SuccessfulTransaction(
            new BookmarkFileTransactionResult(
                backupPath,
                expected with { FullPath = backupPath },
                final));
        var service = new ChromeBookmarksSaveService(
            new AlwaysClosedProcessDetector(),
            baselineService,
            transaction);

        var error = await Assert.ThrowsAsync<ChromeBookmarksSaveException>(
            () => service.SaveAsync(CreateDocument(), expected));

        Assert.Equal(2, baselineService.VerifyCount);
        Assert.Equal(ChromeBookmarksSaveError.RecoveryRequired, error.Error);
        Assert.Equal(backupPath, error.BackupPath);
        Assert.True(error.HasVerifiedRecoveryBackup);
    }

    private static BookmarkSourceBaseline CreateBaseline() =>
        new(
            Path.GetFullPath(
                Path.Combine(
                    Path.GetTempPath(),
                    "synthetic",
                    "Bookmarks")),
            new string('a', 64),
            100,
            new DateTime(
                2026,
                9,
                22,
                4,
                0,
                0,
                DateTimeKind.Utc));

    private static BookmarkDocument CreateDocument()
    {
        var bookmarkBar = Folder("1", "Bookmarks bar");
        var other = Folder("2", "Other bookmarks");
        var synced = Folder("3", "Mobile bookmarks");

        return new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                bookmarkBar,
                other,
                synced,
                EmptyProperties),
            EmptyProperties);
    }

    private static BookmarkFolder Folder(string id, string name) =>
        new(
            id,
            GuidFor(int.Parse(id)),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties,
            Array.Empty<BookmarkNode>());

    private static Guid GuidFor(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        bytes[7] = 0x40;
        bytes[8] = 0x80;
        return new Guid(bytes);
    }

    private sealed class StubReader(BookmarkDocument document)
        : IChromeBookmarksReader
    {
        public Task<BookmarkDocument> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(document);
    }

    private sealed class FixedBaselineService(
        BookmarkSourceBaseline baseline)
        : IBookmarkSourceBaselineService
    {
        public Task<BookmarkSourceBaseline> CaptureAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(baseline);

        public Task<BookmarkSourceBaseline> VerifyUnchangedAsync(
            BookmarkSourceBaseline expected,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(expected);
    }

    private sealed class ControlledSaveService : IChromeBookmarksSaveService
    {
        public int CallCount { get; private set; }

        public Exception? Exception { get; init; }

        public Task<ChromeBookmarksSaveResult> SaveAsync(
            BookmarkDocument? document,
            BookmarkSourceBaseline? sourceBaseline,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Exception is null
                ? Task.FromResult(
                    new ChromeBookmarksSaveResult(
                        sourceBaseline!.FullPath + ".bak",
                        sourceBaseline))
                : Task.FromException<ChromeBookmarksSaveResult>(
                    Exception);
        }
    }

    private sealed class FinalConfirmationBaselineService
        : IBookmarkSourceBaselineService
    {
        public int VerifyCount { get; private set; }

        public Task<BookmarkSourceBaseline> CaptureAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BookmarkSourceBaseline> VerifyUnchangedAsync(
            BookmarkSourceBaseline expected,
            CancellationToken cancellationToken = default)
        {
            VerifyCount++;

            if (VerifyCount == 1)
            {
                return Task.FromResult(expected);
            }

            return Task.FromException<BookmarkSourceBaseline>(
                new BookmarkSourceBaselineException(
                    BookmarkSourceBaselineError.SourceChanged,
                    "Synthetic change after replacement."));
        }
    }

    private sealed class SuccessfulTransaction(
        BookmarkFileTransactionResult result)
        : IBookmarkFileTransaction
    {
        public Task<BookmarkFileTransactionResult> ExecuteAsync(
            BookmarkDocument document,
            BookmarkSourceBaseline expectedBaseline,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "The guarded overload is required.");

        public async Task<BookmarkFileTransactionResult> ExecuteAsync(
            BookmarkDocument document,
            BookmarkSourceBaseline expectedBaseline,
            Func<CancellationToken, Task> beforeReplaceGuard,
            CancellationToken cancellationToken = default)
        {
            await beforeReplaceGuard(CancellationToken.None);
            return result;
        }
    }

    private sealed class AlwaysClosedProcessDetector : IChromeProcessDetector
    {
        public bool IsChromeRunning() => false;
    }

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
