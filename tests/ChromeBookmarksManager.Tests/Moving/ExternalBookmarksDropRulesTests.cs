using ChromeBookmarksManager.DragDrop;

namespace ChromeBookmarksManager.Tests.Moving;

public sealed class ExternalBookmarksDropRulesTests
{
    [Theory]
    [InlineData(@"C:\Chrome\Default\Bookmarks")]
    [InlineData(@"C:\Chrome\Default\bookmarks")]
    [InlineData(@"C:\Chrome\Default\Bookmarks.bak")]
    [InlineData(@"C:\Chrome\Default\BOOKMARKS.BAK")]
    public void IsSupportedBookmarksFile_AcceptsChromeBookmarkFiles(string path)
    {
        Assert.True(ExternalBookmarksDropRules.IsSupportedBookmarksFile(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\Chrome\Default\Bookmarks.json")]
    [InlineData(@"C:\Chrome\Default\bookmarks.html")]
    [InlineData(@"C:\Chrome\Default\notes.txt")]
    public void IsSupportedBookmarksFile_RejectsOtherFiles(string? path)
    {
        Assert.False(ExternalBookmarksDropRules.IsSupportedBookmarksFile(path));
    }
}
