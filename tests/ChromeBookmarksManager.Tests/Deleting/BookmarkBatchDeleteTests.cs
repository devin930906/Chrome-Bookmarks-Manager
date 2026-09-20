using System.Text.Json;
using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Deleting;

public sealed class BookmarkBatchDeleteTests
{
    [Fact]
    public void DeleteBookmarks_AcrossParents_RemovesEveryBookmarkOnce()
    {
        var fixture = CreateFixture();
        var service = new BookmarkDeleteService();
        var beforeUrls = fixture.Document.UrlCount;
        var beforeFolders = fixture.Document.FolderCount;

        var result = service.DeleteBookmarks(
            fixture.Document,
            new[]
            {
                fixture.BarSecond,
                fixture.OtherFirst,
                fixture.BarFirst
            });

        Assert.True(result.Changed);
        Assert.Equal(3, result.RemovedItems.Count);
        Assert.Equal(3, result.RemovedUrlCount);
        Assert.Equal(beforeUrls - 3, fixture.Document.UrlCount);
        Assert.Equal(beforeFolders, fixture.Document.FolderCount);

        Assert.Null(fixture.BarFirst.Parent);
        Assert.Null(fixture.BarSecond.Parent);
        Assert.Null(fixture.OtherFirst.Parent);
        Assert.Equal(
            new BookmarkNode[] { fixture.BarFolder },
            fixture.BookmarkBar.Children);
        Assert.Empty(fixture.Other.Children);
    }

    [Fact]
    public void DeleteBookmarks_DeduplicatesRepeatedReferences()
    {
        var fixture = CreateFixture();
        var service = new BookmarkDeleteService();
        var beforeUrls = fixture.Document.UrlCount;

        var result = service.DeleteBookmarks(
            fixture.Document,
            new[]
            {
                fixture.BarFirst,
                fixture.BarFirst,
                fixture.BarFirst
            });

        Assert.True(result.Changed);
        var removed = Assert.Single(result.RemovedItems);
        Assert.Same(fixture.BarFirst, removed.Node);
        Assert.Equal(1, result.RemovedUrlCount);
        Assert.Equal(beforeUrls - 1, fixture.Document.UrlCount);
    }

    [Fact]
    public void DeleteBookmarks_ReturnsOriginalParentAndMixedChildIndexes()
    {
        var fixture = CreateFixture();
        var service = new BookmarkDeleteService();

        var result = service.DeleteBookmarks(
            fixture.Document,
            new[]
            {
                fixture.BarSecond,
                fixture.BarFirst
            });

        var first = Assert.Single(
            result.RemovedItems.Where(
                item => ReferenceEquals(
                    item.Node,
                    fixture.BarFirst)));
        var second = Assert.Single(
            result.RemovedItems.Where(
                item => ReferenceEquals(
                    item.Node,
                    fixture.BarSecond)));

        Assert.Same(fixture.BookmarkBar, first.SourceParent);
        Assert.Equal(0, first.SourceIndex);
        Assert.Same(fixture.BookmarkBar, second.SourceParent);
        Assert.Equal(2, second.SourceIndex);
    }

    [Fact]
    public void DeleteBookmarks_RejectsForeignNodeBeforeAnyMutation()
    {
        var fixture = CreateFixture();
        var foreign = CreateFixture("Foreign ").OtherFirst;
        var service = new BookmarkDeleteService();
        var beforeBar = fixture.BookmarkBar.Children.ToArray();
        var beforeUrls = fixture.Document.UrlCount;

        var exception = Assert.Throws<BookmarkDeleteException>(
            () => service.DeleteBookmarks(
                fixture.Document,
                new[]
                {
                    fixture.BarFirst,
                    foreign,
                    fixture.BarSecond
                }));

        Assert.Equal(
            BookmarkDeleteError.NodeNotInDocument,
            exception.Error);
        Assert.Equal(beforeBar, fixture.BookmarkBar.Children);
        Assert.Equal(beforeUrls, fixture.Document.UrlCount);
        Assert.Same(
            fixture.BookmarkBar,
            fixture.BarFirst.Parent);
        Assert.Same(
            fixture.BookmarkBar,
            fixture.BarSecond.Parent);
    }

    [Fact]
    public void DeleteBookmarks_RejectsDetachedNodeBeforeAnyMutation()
    {
        var fixture = CreateFixture();
        var detached = fixture.Other.RemoveChildAt(0);
        var service = new BookmarkDeleteService();
        var beforeBar = fixture.BookmarkBar.Children.ToArray();
        var beforeUrls = fixture.Document.UrlCount;

        var exception = Assert.Throws<BookmarkDeleteException>(
            () => service.DeleteBookmarks(
                fixture.Document,
                new[]
                {
                    fixture.BarFirst,
                    (BookmarkUrl)detached
                }));

        Assert.Equal(
            BookmarkDeleteError.NodeNotInDocument,
            exception.Error);
        Assert.Equal(beforeBar, fixture.BookmarkBar.Children);
        Assert.Equal(beforeUrls, fixture.Document.UrlCount);
        Assert.Same(
            fixture.BookmarkBar,
            fixture.BarFirst.Parent);
    }

    [Fact]
    public void DeleteBookmarks_EmptySelection_IsNoOp()
    {
        var fixture = CreateFixture();
        var service = new BookmarkDeleteService();
        var beforeUrls = fixture.Document.UrlCount;
        var beforeFolders = fixture.Document.FolderCount;

        var result = service.DeleteBookmarks(
            fixture.Document,
            Array.Empty<BookmarkUrl>());

        Assert.False(result.Changed);
        Assert.Empty(result.RemovedItems);
        Assert.Equal(0, result.RemovedUrlCount);
        Assert.Equal(beforeUrls, fixture.Document.UrlCount);
        Assert.Equal(beforeFolders, fixture.Document.FolderCount);
    }

    private static Fixture CreateFixture(string prefix = "")
    {
        var barFirst = Url("10", $"{prefix}Bar first");
        var barFolder = Folder("20", $"{prefix}Folder");
        var barSecond = Url("11", $"{prefix}Bar second");
        var bar = Folder(
            "1",
            $"{prefix}Bookmarks bar",
            barFirst,
            barFolder,
            barSecond);
        var otherFirst = Url("12", $"{prefix}Other first");
        var other = Folder(
            "2",
            $"{prefix}Other bookmarks",
            otherFirst);
        var synced = Folder("3", $"{prefix}Mobile bookmarks");

        var document = new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                bar,
                other,
                synced,
                EmptyProperties),
            EmptyProperties);

        return new Fixture(
            document,
            bar,
            other,
            barFirst,
            barSecond,
            barFolder,
            otherFirst);
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

    private static BookmarkUrl Url(string id, string name) =>
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

    private sealed record Fixture(
        BookmarkDocument Document,
        BookmarkFolder BookmarkBar,
        BookmarkFolder Other,
        BookmarkUrl BarFirst,
        BookmarkUrl BarSecond,
        BookmarkFolder BarFolder,
        BookmarkUrl OtherFirst);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
