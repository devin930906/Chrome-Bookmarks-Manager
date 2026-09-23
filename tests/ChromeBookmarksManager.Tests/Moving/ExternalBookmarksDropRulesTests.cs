using ChromeBookmarksManager.DragDrop;

namespace ChromeBookmarksManager.Tests.Moving;

public sealed class ExternalBookmarksDropRulesTests
{
    [Theory]
    [InlineData(@"C:\Chrome\Default\Bookmarks")]
    [InlineData(@"C:\Chrome\Default\bookmarks")]
    [InlineData(@"C:\Chrome\Default\BOOKMARKS")]
    public void IsSupportedBookmarksFile_AcceptsChromeBookmarksFile(string path)
    {
        Assert.True(ExternalBookmarksDropRules.IsSupportedBookmarksFile(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\Chrome\Default\Bookmarks.json")]
    [InlineData(@"C:\Chrome\Default\bookmarks.html")]
    [InlineData(@"C:\Chrome\Default\notes.txt")]
    [InlineData(@"C:\Chrome\Default\Bookmarks.bak")]
    public void IsSupportedBookmarksFile_RejectsOtherFiles(string? path)
    {
        Assert.False(ExternalBookmarksDropRules.IsSupportedBookmarksFile(path));
    }
}
