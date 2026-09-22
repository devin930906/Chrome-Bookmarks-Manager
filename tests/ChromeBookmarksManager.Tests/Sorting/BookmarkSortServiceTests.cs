using System.Globalization;
using System.Text.Json;
using ChromeBookmarksManager.Application.Sorting;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Sorting;

public sealed class BookmarkSortServiceTests
{
    [Fact]
    public void SortByName_MixedChildren_PutsFoldersFirstAndUsesStableUiCultureOrder()
    {
        var firstSameName = Url("10", "Alpha");
        var folderZulu = Folder("20", "Zulu");
        var bookmarkZulu = Url("11", "Zulu");
        var folderAlpha = Folder("21", "Alpha");
        var secondSameName = Url("12", "Alpha");
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            bookmarkZulu,
            folderZulu,
            firstSameName,
            folderAlpha,
            secondSameName);
        var document = Document(bookmarkBar);
        var service = new BookmarkSortService();

        var result = service.SortByName(
            document,
            bookmarkBar,
            CultureInfo.CurrentUICulture);

        Assert.True(result.Changed);
        Assert.Equal(
            new BookmarkNode[]
            {
                folderAlpha,
                folderZulu,
                firstSameName,
                secondSameName,
                bookmarkZulu
            },
            bookmarkBar.Children);
        Assert.Same(bookmarkBar, folderAlpha.Parent);
        Assert.Same(bookmarkBar, firstSameName.Parent);
        Assert.Equal(
            new BookmarkNode[]
            {
                bookmarkZulu,
                folderZulu,
                firstSameName,
                folderAlpha,
                secondSameName
            },
            result.OriginalOrder);
        Assert.Equal(bookmarkBar.Children, result.SortedOrder);
    }

    [Fact]
    public void SortByName_AlreadySorted_IsNoOp()
    {
        var folderAlpha = Folder("20", "Alpha");
        var folderZulu = Folder("21", "Zulu");
        var bookmarkAlpha = Url("10", "Alpha");
        var bookmarkZulu = Url("11", "Zulu");
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            folderAlpha,
            folderZulu,
            bookmarkAlpha,
            bookmarkZulu);
        var document = Document(bookmarkBar);
        var service = new BookmarkSortService();
        var before = bookmarkBar.Children.ToArray();

        var result = service.SortByName(
            document,
            bookmarkBar,
            CultureInfo.CurrentUICulture);

        Assert.False(result.Changed);
        Assert.Equal(before, bookmarkBar.Children);
        Assert.Equal(before, result.OriginalOrder);
        Assert.Equal(before, result.SortedOrder);
    }

    [Fact]
    public void SortByName_PermanentRootSortsChildrenWithoutMovingRoot()
    {
        var bookmark = Url("10", "A");
        var folder = Folder("20", "Z");
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            bookmark,
            folder);
        var document = Document(bookmarkBar);
        var service = new BookmarkSortService();

        var result = service.SortByName(
            document,
            document.Roots.BookmarkBar,
            CultureInfo.CurrentUICulture);

        Assert.True(result.Changed);
        Assert.Null(bookmarkBar.Parent);
        Assert.Same(document.Roots.BookmarkBar, bookmarkBar);
        Assert.Equal(
            new BookmarkNode[] { folder, bookmark },
            bookmarkBar.Children);
    }

    private static BookmarkDocument Document(
        BookmarkFolder bookmarkBar)
    {
        var other = Folder("2", "Other bookmarks");
        var synced = Folder("3", "Mobile bookmarks");

        return new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                bookmarkBar,
                other,
                synced,
                EmptyProperties),
            EmptyProperties);
    }

    private static BookmarkFolder Folder(
        string id,
        string name,
        params BookmarkNode[] children) =>
        new(
            id,
            GuidFor(int.Parse(id)),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties,
            children);

    private static BookmarkUrl Url(
        string id,
        string name) =>
        new(
            id,
            GuidFor(int.Parse(id)),
            name,
            $"https://example.com/{id}",
            null,
            null,
            null,
            null,
            EmptyProperties);

    private static Guid GuidFor(int value)
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
