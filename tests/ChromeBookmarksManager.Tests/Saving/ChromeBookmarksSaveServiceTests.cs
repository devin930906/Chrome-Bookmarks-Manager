using ChromeBookmarksManager.Application.Saving;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.Infrastructure.Persistence;
using ChromeBookmarksManager.Infrastructure.Processes;

namespace ChromeBookmarksManager.Tests.Saving;

public sealed class ChromeBookmarksSaveServiceTests
{
    [Fact]
    public async Task SaveAsync_ChromeRunning_BlocksBeforeBaselineOrTransaction()
    {
        var process = new SequenceProcessDetector(true);
        var baseline = new RecordingBaselineService();
        var transaction = new RecordingTransaction();
        var service = new ChromeBookmarksSaveService(process, baseline, transaction);

        var error = await Assert.ThrowsAsync<ChromeBookmarksSaveException>(
            () => service.SaveAsync(
                CreateDocument(),
                CreateBaseline()));

        Assert.Equal(ChromeBookmarksSaveError.ChromeRunning, error.Error);
        Assert.Equal(1, process.CallCount);
        Assert.Equal(0, baseline.VerifyCount);
        Assert.Equal(0, transaction.ExecuteCount);
    }

    [Fact]
    public async Task SaveAsync_ProcessEnumerationFailure_IsTypedSafetyFailure()
    {
        var process = new ThrowingProcessDetector();
        var service = new ChromeBookmarksSaveService(
            process,
            new RecordingBaselineService(),
            new RecordingTransaction());

        var error = await Assert.ThrowsAsync<ChromeBookmarksSaveException>(
            () => service.SaveAsync(CreateDocument(), CreateBaseline()));

        Assert.Equal(ChromeBookmarksSaveError.ProcessCheckFailed, error.Error);
    }

    [Fact]
    public async Task SaveAsync_SourceChanged_BlocksTransaction()
    {
        var baseline = new RecordingBaselineService
        {
            VerifyException = new BookmarkSourceBaselineException(
                BookmarkSourceBaselineError.SourceChanged,
                "changed")
        };
        var transaction = new RecordingTransaction();
        var service = new ChromeBookmarksSaveService(
            new SequenceProcessDetector(false),
            baseline,
            transaction);

        var error = await Assert.ThrowsAsync<ChromeBookmarksSaveException>(
            () => service.SaveAsync(CreateDocument(), CreateBaseline()));

        Assert.Equal(
            ChromeBookmarksSaveError.SourceChangedExternally,
            error.Error);
        Assert.Equal(0, transaction.ExecuteCount);
    }

    [Fact]
    public async Task SaveAsync_Success_RechecksChromeAtCriticalReplaceBoundary()
    {
        var expected = CreateBaseline();
        var final = expected with
        {
            Sha256 = new string('b', 64),
            Length = expected.Length + 10,
            LastWriteTimeUtc = expected.LastWriteTimeUtc.AddSeconds(1)
        };
        var backup = expected with
        {
            FullPath = expected.FullPath + ".ChromeBookmarksManager.test.bak"
        };

        var process = new SequenceProcessDetector(false, false);
        var transaction = new RecordingTransaction
        {
            Result = new BookmarkFileTransactionResult(
                backup.FullPath,
                backup,
                final)
        };
        var service = new ChromeBookmarksSaveService(
            process,
            new RecordingBaselineService(),
            transaction);

        var result = await service.SaveAsync(
            CreateDocument(),
            expected);

        Assert.Equal(2, process.CallCount);
        Assert.Equal(1, transaction.ExecuteCount);
        Assert.True(transaction.CriticalGuardInvoked);
        Assert.Equal(backup.FullPath, result.BackupPath);
        Assert.Equal(final, result.FinalBaseline);
    }

    [Fact]
    public async Task SaveAsync_ChromeStartsBeforeReplace_AbortsBeforeReplace()
    {
        var process = new SequenceProcessDetector(false, true);
        var transaction = new RecordingTransaction
        {
            InvokeCriticalGuard = true
        };
        var service = new ChromeBookmarksSaveService(
            process,
            new RecordingBaselineService(),
            transaction);

        var error = await Assert.ThrowsAsync<ChromeBookmarksSaveException>(
            () => service.SaveAsync(
                CreateDocument(),
                CreateBaseline()));

        Assert.Equal(ChromeBookmarksSaveError.ChromeRunning, error.Error);
        Assert.Equal(2, process.CallCount);
        Assert.True(transaction.CriticalGuardInvoked);
        Assert.False(transaction.ReplacementSimulated);
    }

    [Fact]
    public async Task SaveAsync_AtomicReplaceFailure_PreservesBackupPathInTypedError()
    {
        var backupPath = @"C:\Synthetic\Bookmarks.ChromeBookmarksManager.test.bak";
        var transaction = new RecordingTransaction
        {
            Exception = new BookmarkFileTransactionException(
                BookmarkFileTransactionError.AtomicReplaceFailed,
                "replace failed",
                backupPath)
        };
        var service = new ChromeBookmarksSaveService(
            new SequenceProcessDetector(false),
            new RecordingBaselineService(),
            transaction);

        var error = await Assert.ThrowsAsync<ChromeBookmarksSaveException>(
            () => service.SaveAsync(
                CreateDocument(),
                CreateBaseline()));

        Assert.Equal(
            ChromeBookmarksSaveError.AtomicReplacementFailed,
            error.Error);
        Assert.Equal(backupPath, error.BackupPath);
    }

    [Fact]
    public async Task SaveAsync_PostWriteValidationFailure_IsRecoveryRequired()
    {
        var backupPath = @"C:\Synthetic\Bookmarks.ChromeBookmarksManager.test.bak";
        var transaction = new RecordingTransaction
        {
            Exception = new BookmarkFileTransactionException(
                BookmarkFileTransactionError.PostWriteValidationFailed,
                "final validation failed",
                backupPath)
        };
        var service = new ChromeBookmarksSaveService(
            new SequenceProcessDetector(false),
            new RecordingBaselineService(),
            transaction);

        var error = await Assert.ThrowsAsync<ChromeBookmarksSaveException>(
            () => service.SaveAsync(
                CreateDocument(),
                CreateBaseline()));

        Assert.Equal(
            ChromeBookmarksSaveError.RecoveryRequired,
            error.Error);
        Assert.Equal(backupPath, error.BackupPath);
    }

    [Fact]
    public async Task SaveAsync_UnsupportedVersion_BlocksBeforeSafetyTransaction()
    {
        var process = new SequenceProcessDetector(false);
        var transaction = new RecordingTransaction();
        var document = CreateDocument(version: 2);
        var service = new ChromeBookmarksSaveService(
            process,
            new RecordingBaselineService(),
            transaction);

        var error = await Assert.ThrowsAsync<ChromeBookmarksSaveException>(
            () => service.SaveAsync(document, CreateBaseline()));

        Assert.Equal(
            ChromeBookmarksSaveError.UnsupportedSource,
            error.Error);
        Assert.Equal(0, process.CallCount);
        Assert.Equal(0, transaction.ExecuteCount);
    }

    private static BookmarkSourceBaseline CreateBaseline() =>
        new(
            Path.GetFullPath(
                Path.Combine(Path.GetTempPath(), "synthetic", "Bookmarks")),
            new string('a', 64),
            100,
            new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc));

    private static BookmarkDocument CreateDocument(int version = 1)
    {
        var empty =
            new Dictionary<string, System.Text.Json.JsonElement>(
                StringComparer.Ordinal);

        BookmarkFolder Folder(
            string id,
            string guid,
            string name) =>
            new(
                id,
                Guid.Parse(guid),
                name,
                null,
                null,
                null,
                null,
                empty,
                Array.Empty<BookmarkNode>());

        return new BookmarkDocument(
            version,
            null,
            null,
            new BookmarkRoots(
                Folder(
                    "1",
                    "11111111-1111-4111-8111-111111111111",
                    "Bookmarks bar"),
                Folder(
                    "2",
                    "22222222-2222-4222-8222-222222222222",
                    "Other bookmarks"),
                Folder(
                    "3",
                    "33333333-3333-4333-8333-333333333333",
                    "Mobile bookmarks"),
                empty),
            empty);
    }

    private sealed class SequenceProcessDetector(params bool[] states) :
        IChromeProcessDetector
    {
        private readonly Queue<bool> _states = new(states);

        public int CallCount { get; private set; }

        public bool IsChromeRunning()
        {
            CallCount++;
            return _states.Count > 0 && _states.Dequeue();
        }
    }

    private sealed class ThrowingProcessDetector : IChromeProcessDetector
    {
        public bool IsChromeRunning() =>
            throw new ChromeProcessDetectionException(
                ChromeProcessDetectionError.EnumerationFailed,
                "Synthetic process enumeration failure.");
    }

    private sealed class RecordingBaselineService :
        IBookmarkSourceBaselineService
    {
        public int VerifyCount { get; private set; }

        public Exception? VerifyException { get; init; }

        public Task<BookmarkSourceBaseline> CaptureAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BookmarkSourceBaseline> VerifyUnchangedAsync(
            BookmarkSourceBaseline expected,
            CancellationToken cancellationToken = default)
        {
            VerifyCount++;
            if (VerifyException is not null)
            {
                return Task.FromException<BookmarkSourceBaseline>(
                    VerifyException);
            }

            return Task.FromResult(expected);
        }
    }

    private sealed class RecordingTransaction : IBookmarkFileTransaction
    {
        public int ExecuteCount { get; private set; }

        public bool CriticalGuardInvoked { get; private set; }

        public bool ReplacementSimulated { get; private set; }

        public bool InvokeCriticalGuard { get; init; } = true;

        public BookmarkFileTransactionResult? Result { get; init; }

        public BookmarkFileTransactionException? Exception { get; init; }

        public Task<BookmarkFileTransactionResult> ExecuteAsync(
            BookmarkDocument document,
            BookmarkSourceBaseline expectedBaseline,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "SaveService must use the critical-guard transaction overload.");

        public async Task<BookmarkFileTransactionResult> ExecuteAsync(
            BookmarkDocument document,
            BookmarkSourceBaseline expectedBaseline,
            Func<CancellationToken, Task> beforeReplaceGuard,
            CancellationToken cancellationToken = default)
        {
            ExecuteCount++;

            if (Exception is not null)
            {
                throw Exception;
            }

            if (InvokeCriticalGuard)
            {
                CriticalGuardInvoked = true;
                await beforeReplaceGuard(CancellationToken.None);
            }

            ReplacementSimulated = true;

            return Result ?? new BookmarkFileTransactionResult(
                expectedBaseline.FullPath + ".bak",
                expectedBaseline with
                {
                    FullPath = expectedBaseline.FullPath + ".bak"
                },
                expectedBaseline);
        }
    }
}
