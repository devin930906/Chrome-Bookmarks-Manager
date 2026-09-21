namespace ChromeBookmarksManager.Application.Saving;

public enum ChromeBookmarksSaveError
{
    NoLoadedSource,
    UnsupportedSource,
    ChromeRunning,
    ProcessCheckFailed,
    SourceChangedExternally,
    SourceMissing,
    AccessDenied,
    SourceReadFailed,
    SerializationValidationFailed,
    TempWriteFailed,
    BackupCreationFailed,
    BackupVerificationFailed,
    AtomicReplacementFailed,
    RecoveryRequired,
    CanceledBeforeReplacement
}
