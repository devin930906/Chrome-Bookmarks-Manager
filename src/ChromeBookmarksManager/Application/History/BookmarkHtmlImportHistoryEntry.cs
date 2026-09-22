using ChromeBookmarksManager.Application.Importing;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

public sealed class BookmarkHtmlImportHistoryEntry
    : IBookmarkHistoryEntry
{
    private readonly BookmarkFolder _importedFolder;
    private readonly BookmarkFolder _targetParent;
    private readonly int _targetIndex;
    private readonly int _urlCount;
    private readonly int _folderCount;

    public BookmarkHtmlImportHistoryEntry(
        BookmarkHtmlImportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Changed)
        {
            throw new ArgumentException(
                "A no-op bookmark HTML import cannot be recorded in history.",
                nameof(result));
        }

        _importedFolder = result.ImportedFolder;
        _targetParent = result.TargetParent;
        _targetIndex = result.TargetIndex;
        _urlCount = result.AddedUrlCount;
        _folderCount = result.AddedFolderCount;
    }

    public string Description =>
        "Import bookmarks HTML";

    public BookmarkHistoryImpact Impact =>
        BookmarkHistoryImpact.SearchRelevant;

    public void Undo(
        BookmarkDocument document) =>
        BookmarkHistoryMutation.DetachWithCounts(
            document,
            _importedFolder,
            _targetParent,
            _targetIndex,
            _urlCount,
            _folderCount);

    public void Redo(
        BookmarkDocument document) =>
        BookmarkHistoryMutation.RestoreDetachedWithCounts(
            document,
            _importedFolder,
            _targetParent,
            _targetIndex,
            _urlCount,
            _folderCount);
}
