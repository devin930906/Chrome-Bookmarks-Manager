namespace ChromeBookmarksManager.DragDrop;

public static class ExternalBookmarksDropRules
{
    public static bool IsSupportedBookmarksFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fileName = Path.GetFileName(path);
        return string.Equals(
                   fileName,
                   "Bookmarks",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   fileName,
                   "Bookmarks.bak",
                   StringComparison.OrdinalIgnoreCase);
    }
}
