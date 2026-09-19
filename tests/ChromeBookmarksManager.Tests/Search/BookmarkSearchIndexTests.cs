using System.Text.Json;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Search;

public sealed class BookmarkSearchIndexTests
{
    [Fact]
    public void Construction_FlattensAllRootsInStableDocumentOrder_UsingOriginalReferences()
    {
        var barFirst = Url("10", "Bar first", "https://bar.example/first");
        var nestedFirst = Url("11", "Nested first", "https://bar.example/nested/first");
        var nestedSecond = Url("12", "Nested second", "https://bar.example/nested/second");
        var nested = Folder("20", "Nested", nestedFirst, nestedSecond);
        var barLast = Url("13", "Bar last", "https://bar.example/last");

        var otherUrl = Url("14", "Other", "https://other.example/");
        var syncedUrl = Url("15", "Synced", "custom-scheme:synced");

        var document = Document(
            Folder("1", "Bookmarks bar", barFirst, nested, barLast),
            Folder("2", "Other bookmarks", otherUrl),
            Folder("3", "Mobile bookmarks", syncedUrl));

        var index = new BookmarkSearchIndex(document);

        Assert.Equal(6, index.Count);
        Assert.Collection(
            index.Bookmarks,
            item => Assert.Same(barFirst, item),
            item => Assert.Same(nestedFirst, item),
            item => Assert.Same(nestedSecond, item),
            item => Assert.Same(barLast, item),
            item => Assert.Same(otherUrl, item),
            item => Assert.Same(syncedUrl, item));
    }

    [Fact]
    public void Construction_IndexesUrlsOnly_NotFolders()
    {
        var url = Url("10", "Only URL", "https://example.com/");
        var nested = Folder("20", "Nested", url);
        var document = Document(
            Folder("1", "Bookmarks bar", nested),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));

        var index = new BookmarkSearchIndex(document);

        Assert.Single(index.Bookmarks);
        Assert.Same(url, index.Bookmarks[0]);
        Assert.Equal(document.UrlCount, index.Count);
        Assert.True(document.FolderCount > index.Count);
    }

    [Fact]
    public void Construction_DoesNotRequireSourceFileAccess()
    {
        var url = Url("10", "Memory only", "opaque:value");
        var document = Document(
            Folder("1", "Bookmarks bar", url),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));

        var index = new BookmarkSearchIndex(document);

        Assert.Equal(1, index.Count);
        Assert.Same(url, index.Bookmarks[0]);
    }

    [Fact]
    public void Construction_WithCanceledToken_ThrowsOperationCanceledException()
    {
        var document = Document(
            Folder("1", "Bookmarks bar", Url("10", "One", "https://example.com/")),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => new BookmarkSearchIndex(document, cts.Token));
    }

    [Fact]
    public void SearchScope_ContainsOnlyV04Scopes()
    {
        Assert.Equal(
            new[] { BookmarkSearchScope.CurrentFolder, BookmarkSearchScope.AllBookmarks },
            Enum.GetValues<BookmarkSearchScope>());
    }

    private static BookmarkDocument Document(
        BookmarkFolder bookmarkBar,
        BookmarkFolder other,
        BookmarkFolder synced) =>
        new(
            1,
            null,
            null,
            new BookmarkRoots(bookmarkBar, other, synced, EmptyProperties),
            EmptyProperties);

    private static BookmarkFolder Folder(
        string id,
        string name,
        params BookmarkNode[] children) =>
        new(
            id,
            CreateGuid(int.Parse(id)),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties,
            children);

    private static BookmarkUrl Url(string id, string name, string url) =>
        new(
            id,
            CreateGuid(int.Parse(id)),
            name,
            url,
            null,
            null,
            null,
            null,
            EmptyProperties);

    private static Guid CreateGuid(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        bytes[7] = 0x40;
        bytes[8] = 0x80;
        return new Guid(bytes);
    }

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
