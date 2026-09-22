using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.Infrastructure.Persistence;
using ChromeBookmarksManager.Infrastructure.Processes;

namespace ChromeBookmarksManager.Application.Saving;

public sealed class ChromeBookmarksSaveService : IChromeBookmarksSaveService
{
    private readonly IChromeProcessDetector _processDetector;
    private readonly IBookmarkSourceBaselineService _baselineService;
    private readonly IBookmarkFileTransaction _transaction;

    public ChromeBookmarksSaveService(
        IChromeProcessDetector processDetector,
        IBookmarkSourceBaselineService baselineService,
        IBookmarkFileTransaction transaction)
    {
        _processDetector = processDetector
            ?? throw new ArgumentNullException(nameof(processDetector));
        _baselineService = baselineService
            ?? throw new ArgumentNullException(nameof(baselineService));
        _transaction = transaction
            ?? throw new ArgumentNullException(nameof(transaction));
    }

    public async Task<ChromeBookmarksSaveResult> SaveAsync(
        BookmarkDocument? document,
        BookmarkSourceBaseline? sourceBaseline,
        CancellationToken cancellationToken = default)
    {
        if (document is null || sourceBaseline is null)
        {
            throw new ChromeBookmarksSaveException(
                ChromeBookmarksSaveError.NoLoadedSource,
                "No loaded Chrome Bookmarks source is available to save.");
        }

        if (document.Version != 1)
        {
            throw new ChromeBookmarksSaveException(
                ChromeBookmarksSaveError.UnsupportedSource,
                $"Chrome bookmark format version {document.Version} is not supported for safe writing.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            throw Canceled(new OperationCanceledException(cancellationToken));
        }

        EnsureChromeClosed();

        try
        {
            await _baselineService
                .VerifyUnchangedAsync(sourceBaseline, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            throw Canceled(exception);
        }
        catch (BookmarkSourceBaselineException exception)
        {
            throw MapBaselineException(exception);
        }

        try
        {
            var result = await _transaction
                .ExecuteAsync(
                    document,
                    sourceBaseline,
                    CriticalChromeRecheckAsync,
                    cancellationToken)
                .ConfigureAwait(false);

            BookmarkSourceBaseline confirmedFinalBaseline;
            try
            {
                confirmedFinalBaseline = await _baselineService
                    .VerifyUnchangedAsync(
                        result.FinalBaseline,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (BookmarkSourceBaselineException exception)
            {
                throw new ChromeBookmarksSaveException(
                    ChromeBookmarksSaveError.RecoveryRequired,
                    "The source was replaced but changed before final save confirmation. Recovery from the verified backup may be required.",
                    result.BackupPath,
                    exception);
            }

            return new ChromeBookmarksSaveResult(
                result.BackupPath,
                confirmedFinalBaseline);
        }
        catch (ChromeBookmarksSaveException)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw Canceled(exception);
        }
        catch (BookmarkFileTransactionException exception)
        {
            throw MapTransactionException(exception);
        }
    }

    private Task CriticalChromeRecheckAsync(
        CancellationToken cancellationToken)
    {
        // The file transaction deliberately invokes this with
        // CancellationToken.None after all cancellable pre-replace work.
        EnsureChromeClosed();
        return Task.CompletedTask;
    }

    private void EnsureChromeClosed()
    {
        try
        {
            if (_processDetector.IsChromeRunning())
            {
                throw new ChromeBookmarksSaveException(
                    ChromeBookmarksSaveError.ChromeRunning,
                    "Google Chrome is running. Close all Chrome windows and background processes before saving.");
            }
        }
        catch (ChromeBookmarksSaveException)
        {
            throw;
        }
        catch (ChromeProcessDetectionException exception)
        {
            throw new ChromeBookmarksSaveException(
                ChromeBookmarksSaveError.ProcessCheckFailed,
                "Chrome process state could not be checked safely, so the Bookmarks file was not written.",
                innerException: exception);
        }
    }

    private static ChromeBookmarksSaveException MapBaselineException(
        BookmarkSourceBaselineException exception) =>
        exception.Error switch
        {
            BookmarkSourceBaselineError.SourceChanged =>
                new ChromeBookmarksSaveException(
                    ChromeBookmarksSaveError.SourceChangedExternally,
                    "The Chrome Bookmarks source changed outside the application. Reload it before saving.",
                    innerException: exception),

            BookmarkSourceBaselineError.FileNotFound =>
                new ChromeBookmarksSaveException(
                    ChromeBookmarksSaveError.SourceMissing,
                    "The Chrome Bookmarks source file no longer exists.",
                    innerException: exception),

            BookmarkSourceBaselineError.AccessDenied =>
                new ChromeBookmarksSaveException(
                    ChromeBookmarksSaveError.AccessDenied,
                    "Windows denied access to the Chrome Bookmarks source file.",
                    innerException: exception),

            _ =>
                new ChromeBookmarksSaveException(
                    ChromeBookmarksSaveError.SourceReadFailed,
                    "The Chrome Bookmarks source could not be verified safely before saving.",
                    innerException: exception)
        };

    private static ChromeBookmarksSaveException MapTransactionException(
        BookmarkFileTransactionException exception) =>
        exception.Error switch
        {
            BookmarkFileTransactionError.SourceChanged =>
                new ChromeBookmarksSaveException(
                    ChromeBookmarksSaveError.SourceChangedExternally,
                    "The Chrome Bookmarks source changed before replacement and was not overwritten.",
                    exception.BackupPath,
                    exception),

            BookmarkFileTransactionError.TempWriteFailed =>
                new ChromeBookmarksSaveException(
                    ChromeBookmarksSaveError.TempWriteFailed,
                    "The new Chrome Bookmarks content could not be written safely to a temporary file.",
                    exception.BackupPath,
                    exception),

            BookmarkFileTransactionError.TempValidationFailed =>
                new ChromeBookmarksSaveException(
                    ChromeBookmarksSaveError.SerializationValidationFailed,
                    "The generated Chrome Bookmarks file failed validation and the source was not replaced.",
                    exception.BackupPath,
                    exception),

            BookmarkFileTransactionError.BackupCreationFailed =>
                new ChromeBookmarksSaveException(
                    ChromeBookmarksSaveError.BackupCreationFailed,
                    "A safety backup could not be created, so the source was not replaced.",
                    exception.BackupPath,
                    exception),

            BookmarkFileTransactionError.BackupVerificationFailed =>
                new ChromeBookmarksSaveException(
                    ChromeBookmarksSaveError.BackupVerificationFailed,
                    "The safety backup could not be verified, so the source was not replaced.",
                    exception.BackupPath,
                    exception),

            BookmarkFileTransactionError.AtomicReplaceFailed =>
                new ChromeBookmarksSaveException(
                    ChromeBookmarksSaveError.AtomicReplacementFailed,
                    "Atomic replacement failed. The verified backup was retained.",
                    exception.BackupPath,
                    exception),

            BookmarkFileTransactionError.PostWriteValidationFailed =>
                new ChromeBookmarksSaveException(
                    ChromeBookmarksSaveError.RecoveryRequired,
                    "The source was replaced but final validation failed. Recovery from the verified backup may be required.",
                    exception.BackupPath,
                    exception),

            _ =>
                new ChromeBookmarksSaveException(
                    ChromeBookmarksSaveError.SerializationValidationFailed,
                    "The safe-save transaction failed before it could be completed.",
                    exception.BackupPath,
                    exception)
        };

    private static ChromeBookmarksSaveException Canceled(
        OperationCanceledException exception) =>
        new(
            ChromeBookmarksSaveError.CanceledBeforeReplacement,
            "Saving was canceled before the replacement critical section began.",
            innerException: exception);
}
