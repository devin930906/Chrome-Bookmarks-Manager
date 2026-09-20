using System.Text.Json;
using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Deleting;

public sealed class BookmarkDeleteServiceTests
{
    [Fact]
    public void DeleteNode_Bookmark_DetachesExactNodeAndUpdatesCounts()
    {
        var fixture = CreateFixture();
        var service = new BookmarkDeleteService();
        var beforeFolders = fixture.Document.FolderCount;
        var beforeUrls = fixture.Document.UrlCount;

        var result = service.DeleteNode(
            fixture.Document,
            fixture.FirstBookmark);

        Assert.Same(fixture.FirstBookmark, result.Node);
        Assert.Same(fixture.BookmarkBar, result.SourceParent);
        Assert.Equal(0, result.SourceIndex);
        Assert.Equal(1, result.RemovedUrlCount);
        Assert.Equal(0, result.RemovedFolderCount);
        Assert.Null(fixture.FirstBookmark.Parent);
        Assert.DoesNotContain(
            fixture.BookmarkBar.Children,
            node => ReferenceEquals(node, fixture.FirstBookmark));
        Assert.Equal(beforeUrls - 1, fixture.Document.UrlCount);
        Assert.Equal(beforeFolders, fixture.Document.FolderCount);
    }

    [Fact]
    public void DeleteNode_Folder_RemovesWholeSubtreeAndPreservesDescendantLinks()
    {
        var fixture = CreateFixture();
        var service = new BookmarkDeleteService();
        var child = fixture.ChildFolder;
        var deep = fixture.DeepFolder;
        var nested = fixture.NestedBookmark;
        var beforeFolders = fixture.Document.FolderCount;
        var beforeUrls = fixture.Document.UrlCount;

        var result = service.DeleteNode(
            fixture.Document,
            child);

        Assert.Same(child, result.Node);
        Assert.Same(fixture.BookmarkBar, result.SourceParent);
        Assert.Equal(1, result.SourceIndex);
        Assert.Equal(1, result.RemovedUrlCount);
        Assert.Equal(2, result.RemovedFolderCount);

        Assert.Null(child.Parent);
        Assert.Same(child, deep.Parent);
        Assert.Same(deep, nested.Parent);

        Assert.Equal(beforeUrls - 1, fixture.Document.UrlCount);
        Assert.Equal(beforeFolders - 2, fixture.Document.FolderCount);
        Assert.DoesNotContain(
            fixture.BookmarkBar.Children,
            node => ReferenceEquals(node, child));
    }

    [Fact]
    public void DeleteNode_PreservesSurvivorIdentityAndOrder()
    {
        var fixture = CreateFixture();
        var service = new BookmarkDeleteService();
        var survivorA = fixture.SecondBookmark;
        var survivorB = fixture.LastFolder;

        service.DeleteNode(
            fixture.Document,
            fixture.ChildFolder);

        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.FirstBookmark,
                survivorA,
                survivorB
            },
            fixture.BookmarkBar.Children);
        Assert.Same(fixture.BookmarkBar, survivorA.Parent);
        Assert.Same(fixture.BookmarkBar, survivorB.Parent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DeleteNode_RejectsPermanentRoots(int rootIndex)
    {
        var fixture = CreateFixture();
        var service = new BookmarkDeleteService();
        var root = new[]
        {
            fixture.Document.Roots.BookmarkBar,
            fixture.Document.Roots.Other,
            fixture.Document.Roots.Synced
        }[rootIndex];
        var urls = fixture.Document.UrlCount;
        var folders = fixture.Document.FolderCount;

        var exception = Assert.Throws<BookmarkDeleteException>(
            () => service.DeleteNode(
                fixture.Document,
                root));

        Assert.Equal(BookmarkDeleteError.ProtectedRoot, exception.Error);
        Assert.Null(root.Parent);
        Assert.Equal(urls, fixture.Document.UrlCount);
        Assert.Equal(folders, fixture.Document.FolderCount);
    }

    [Fact]
    public void DeleteNode_RejectsForeignDocumentNodeBeforeMutation()
    {
        var fixture = CreateFixture();
        var foreign = CreateFixture("Foreign ").FirstBookmark;
        var service = new BookmarkDeleteService();
        var urls = fixture.Document.UrlCount;
        var folders = fixture.Document.FolderCount;

        var exception = Assert.Throws<BookmarkDeleteException>(
            () => service.DeleteNode(
                fixture.Document,
                foreign));

        Assert.Equal(
            BookmarkDeleteError.NodeNotInDocument,
            exception.Error);
        Assert.Same(foreign.Parent, foreign.Parent);
        Assert.Equal(urls, fixture.Document.UrlCount);
        Assert.Equal(folders, fixture.Document.FolderCount);
    }

    [Fact]
    public void DeleteNode_RejectsDetachedNodeBeforeMutation()
    {
        var fixture = CreateFixture();
        var detached = fixture.BookmarkBar.RemoveChildAt(0);
        var service = new BookmarkDeleteService();
        var urls = fixture.Document.UrlCount;
        var folders = fixture.Document.FolderCount;

        var exception = Assert.Throws<BookmarkDeleteException>(
            () => service.DeleteNode(
                fixture.Document,
                detached));

        Assert.Equal(
            BookmarkDeleteError.NodeNotInDocument,
            exception.Error);
        Assert.Null(detached.Parent);
        Assert.Equal(urls, fixture.Document.UrlCount);
        Assert.Equal(folders, fixture.Document.FolderCount);
    }

    private static Fixture CreateFixture(string prefix = "")
    {
        var first = Url("10", $"{prefix}First");
        var nested = Url("12", $"{prefix}Nested");
        var deep = Folder("30", $"{prefix}Deep", nested);
        var child = Folder("20", $"{prefix}Child", deep);
        var second = Url("11", $"{prefix}Second");
        var lastFolder = Folder("21", $"{prefix}Last folder");
        var bar = Folder(
            "1",
            $"{prefix}Bookmarks bar",
            first,
            child,
            second,
            lastFolder);
        var other = Folder("2", $"{prefix}Other bookmarks");
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
            child,
            deep,
            first,
            second,
            nested,
            lastFolder);
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
        BookmarkFolder ChildFolder,
        BookmarkFolder DeepFolder,
        BookmarkUrl FirstBookmark,
        BookmarkUrl SecondBookmark,
        BookmarkUrl NestedBookmark,
        BookmarkFolder LastFolder);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
