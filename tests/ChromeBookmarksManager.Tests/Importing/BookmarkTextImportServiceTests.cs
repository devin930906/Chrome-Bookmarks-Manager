using ChromeBookmarksManager.Application.Importing;

namespace ChromeBookmarksManager.Tests.Importing;

public sealed class BookmarkTextImportServiceTests
{
    [Fact]
    public void Parse_AcceptsAbsoluteAndBareHostsAndIgnoresBlankLines()
    {
        var result = BookmarkTextImportService.Parse(
            "https://www.example.com/path\r\n\r\nexample.org/docs\r\nlocalhost:3000/page");

        Assert.Empty(result.Errors);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal("example.com", result.Items[0].Name);
        Assert.Equal("https://www.example.com/path", result.Items[0].Url);
        Assert.Equal("https://example.org/docs", result.Items[1].Url);
        Assert.Equal("https://localhost:3000/page", result.Items[2].Url);
    }

    [Fact]
    public void Parse_ReportsInvalidLineWithoutDroppingItsLineNumber()
    {
        var result = BookmarkTextImportService.Parse(
            "https://valid.example\nnot a url\nsecond.example");

        Assert.Equal(2, result.Items.Count);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Equal("not a url", error.Value);
    }

    [Fact]
    public void Parse_PreservesExplicitNonHttpBookmarkUrls()
    {
        var result = BookmarkTextImportService.Parse(
            "chrome://bookmarks/\nmailto:test@example.com");

        Assert.Empty(result.Errors);
        Assert.Equal("chrome://bookmarks/", result.Items[0].Url);
        Assert.Equal("mailto:test@example.com", result.Items[1].Url);
    }
}
