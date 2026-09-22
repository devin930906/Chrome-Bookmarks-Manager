namespace ChromeBookmarksManager.Application;

public enum DocumentState
{
    NoDocument,
    Loading,
    LoadedClean,
    LoadedDirty,
    Saving,
    SaveFailed,
    RecoveryRequired,
    LoadFailed
}
