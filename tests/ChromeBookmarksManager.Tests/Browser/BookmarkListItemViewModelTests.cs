using System.Text.Json;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Browser;

public sealed class BookmarkListItemViewModelTests
{
    [Fact]
    public void FolderItem_ExposesNameKindAndEmptyUrl()
    {
        var folder = Folder(
            "10",
            "10101010-1010-4010-8010-101010101010",
            "Projects");

        var item = new BookmarkListItemViewModel(folder);

        Assert.Same(folder, item.Node);
        Assert.Equal("Projects", item.Name);
        Assert.Equal(BookmarkNodeKind.Folder, item.Kind);
        Assert.True(item.IsFolder);
        Assert.False(item.IsBookmark);
        Assert.Equal(string.Empty, item.Url);
    }

    [Fact]
    public void BookmarkItem_ExposesNameKindAndUrl()
    {
        var bookmark = Url(
            "11",
            "11111111-1111-4111-8111-111111111111",
            "OpenAI",
            "https://openai.com/");

        var item = new BookmarkListItemViewModel(bookmark);

        Assert.Same(bookmark, item.Node);
        Assert.Equal("OpenAI", item.Name);
        Assert.Equal(BookmarkNodeKind.Url, item.Kind);
        Assert.False(item.IsFolder);
        Assert.True(item.IsBookmark);
        Assert.Equal("https://openai.com/", item.Url);
    }

    private static BookmarkFolder Folder(
        string id,
        string guid,
        string name,
        params BookmarkNode[] children) =>
        new(
            id,
            Guid.Parse(guid),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties(),
            children);

    private static BookmarkUrl Url(
        string id,
        string guid,
        string name,
        string url) =>
        new(
            id,
            Guid.Parse(guid),
            name,
            url,
            null,
            null,
            null,
            null,
            EmptyProperties());

    private static IReadOnlyDictionary<string, JsonElement> EmptyProperties() =>
        new Dictionary<string, JsonElement>();
}
