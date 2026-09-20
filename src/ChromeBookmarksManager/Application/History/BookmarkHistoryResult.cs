namespace ChromeBookmarksManager.Application.History;

public sealed record BookmarkHistoryResult(
    bool Changed,
    string? Description,
    BookmarkHistoryImpact Impact)
{
    public static BookmarkHistoryResult NoChange { get; } =
        new(false, null, BookmarkHistoryImpact.None);
}
