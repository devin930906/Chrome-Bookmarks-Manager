using System.Text.Json;
using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Moving;

public sealed class BookmarkReorderSemanticsTests
{
    [Fact]
    public void MoveBookmarkBefore_SameParent_PreservesFolderSlots()
    {
        var folderA = Folder("20", "Folder A");
        var bookmark1 = Url("10", "Bookmark 1");
        var folderB = Folder("21", "Folder B");
        var bookmark2 = Url("11", "Bookmark 2");
        var bar = Folder(
            "1",
            "Bookmarks bar",
            folderA,
            bookmark1,
            folderB,
            bookmark2);
        var document = Document(bar);
        var service = new BookmarkMoveService();

        var result = service.MoveBookmarkBefore(
            document,
            bookmark2,
            bookmark1);

        Assert.True(result.Changed);
        Assert.Equal(
            new BookmarkNode[] { folderA, bookmark2, folderB, bookmark1 },
            bar.Children);
        Assert.Equal(1, result.TargetIndex);
        Assert.All(bar.Children, node => Assert.Same(bar, node.Parent));
    }

    [Fact]
    public void MoveFolderBefore_SameParent_PreservesBookmarkSlots()
    {
        var bookmark1 = Url("10", "Bookmark 1");
        var folderA = Folder("20", "Folder A");
        var bookmark2 = Url("11", "Bookmark 2");
        var folderB = Folder("21", "Folder B");
        var bar = Folder(
            "1",
            "Bookmarks bar",
            bookmark1,
            folderA,
            bookmark2,
            folderB);
        var document = Document(bar);
        var service = new BookmarkMoveService();

        var result = service.MoveFolderBefore(
            document,
            folderB,
            folderA);

        Assert.True(result.Changed);
        Assert.Equal(
            new BookmarkNode[] { bookmark1, folderB, bookmark2, folderA },
            bar.Children);
        Assert.Equal(1, result.TargetIndex);
        Assert.All(bar.Children, node => Assert.Same(bar, node.Parent));
    }

    [Fact]
    public void MoveBookmarkAfter_AdjacentVisibleBookmark_IsNoOp()
    {
        var folder = Folder("20", "Folder");
        var first = Url("10", "First");
        var second = Url("11", "Second");
        var bar = Folder("1", "Bookmarks bar", first, folder, second);
        var document = Document(bar);
        var service = new BookmarkMoveService();

        var result = service.MoveBookmarkAfter(document, first, second);

        Assert.True(result.Changed);
        Assert.Equal(
            new BookmarkNode[] { second, folder, first },
            bar.Children);
    }

    [Fact]
    public void MoveBookmarkBefore_AlreadyBeforeTarget_IsNoOp()
    {
        var folder = Folder("20", "Folder");
        var first = Url("10", "First");
        var second = Url("11", "Second");
        var bar = Folder("1", "Bookmarks bar", first, folder, second);
        var document = Document(bar);
        var service = new BookmarkMoveService();

        var result = service.MoveBookmarkBefore(document, first, second);

        Assert.False(result.Changed);
        Assert.Equal(
            new BookmarkNode[] { first, folder, second },
            bar.Children);
    }

    [Fact]
    public void MoveBookmarkBefore_CrossParent_InsertsAtExactMixedTargetIndex()
    {
        var sourceBookmark = Url("10", "Source");
        var source = Folder("20", "Source folder", sourceBookmark);
        var targetFolder = Folder("21", "Target folder");
        var targetBookmark = Url("11", "Target");
        var other = Folder(
            "2",
            "Other bookmarks",
            targetFolder,
            targetBookmark);
        var bar = Folder("1", "Bookmarks bar", source);
        var document = Document(bar, other);
        var service = new BookmarkMoveService();

        var result = service.MoveBookmarkBefore(
            document,
            sourceBookmark,
            targetBookmark);

        Assert.True(result.Changed);
        Assert.Equal(
            new BookmarkNode[] { targetFolder, sourceBookmark, targetBookmark },
            other.Children);
        Assert.Same(other, sourceBookmark.Parent);
    }

    [Fact]
    public void MoveFolderAfter_CrossParent_InsertsAfterTarget()
    {
        var moving = Folder("20", "Moving");
        var bar = Folder("1", "Bookmarks bar", moving);
        var target = Folder("21", "Target");
        var marker = Url("10", "Marker");
        var other = Folder("2", "Other bookmarks", target, marker);
        var document = Document(bar, other);
        var service = new BookmarkMoveService();

        var result = service.MoveFolderAfter(document, moving, target);

        Assert.True(result.Changed);
        Assert.Equal(
            new BookmarkNode[] { target, moving, marker },
            other.Children);
        Assert.Same(other, moving.Parent);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void MoveNodeBefore_AllowsMixedKindsAndUsesExactChildOrder(
        bool movingIsFolder,
        bool targetIsFolder)
    {
        BookmarkNode moving = movingIsFolder
            ? Folder("20", "Moving folder")
            : Url("10", "Moving bookmark");
        BookmarkNode target = targetIsFolder
            ? Folder("21", "Target folder")
            : Url("11", "Target bookmark");
        var marker = Url("12", "Marker");
        var bar = Folder("1", "Bookmarks bar", marker, target, moving);
        var document = Document(bar);
        var service = new BookmarkMoveService();

        var result = service.MoveNodeBefore(
            document,
            moving,
            target);

        Assert.True(result.Changed);
        Assert.Equal(
            new BookmarkNode[] { marker, moving, target },
            bar.Children);
        Assert.Same(bar, moving.Parent);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void MoveNodeAfter_AllowsMixedKindsAndUsesExactChildOrder(
        bool movingIsFolder,
        bool targetIsFolder)
    {
        BookmarkNode moving = movingIsFolder
            ? Folder("20", "Moving folder")
            : Url("10", "Moving bookmark");
        BookmarkNode target = targetIsFolder
            ? Folder("21", "Target folder")
            : Url("11", "Target bookmark");
        var marker = Url("12", "Marker");
        var bar = Folder("1", "Bookmarks bar", moving, target, marker);
        var document = Document(bar);
        var service = new BookmarkMoveService();

        var result = service.MoveNodeAfter(
            document,
            moving,
            target);

        Assert.True(result.Changed);
        Assert.Equal(
            new BookmarkNode[] { target, moving, marker },
            bar.Children);
        Assert.Same(bar, moving.Parent);
    }

    [Fact]
    public void MoveToEnd_AppendsAtRealEndOfTargetChildren()
    {
        var moving = Url("10", "Moving");
        var bar = Folder("1", "Bookmarks bar", moving);
        var existingFolder = Folder("20", "Existing folder");
        var existingBookmark = Url("11", "Existing bookmark");
        var other = Folder(
            "2",
            "Other bookmarks",
            existingFolder,
            existingBookmark);
        var document = Document(bar, other);
        var service = new BookmarkMoveService();

        var result = service.MoveToEnd(document, moving, other);

        Assert.True(result.Changed);
        Assert.Equal(
            new BookmarkNode[] { existingFolder, existingBookmark, moving },
            other.Children);
        Assert.Equal(2, result.TargetIndex);
    }

    [Fact]
    public void PositionalFolderMove_RejectsRootAsSiblingTarget()
    {
        var child = Folder("20", "Child");
        var bar = Folder("1", "Bookmarks bar", child);
        var document = Document(bar);
        var service = new BookmarkMoveService();

        var exception = Assert.Throws<BookmarkMoveException>(
            () => service.MoveFolderBefore(
                document,
                child,
                document.Roots.Other));

        Assert.Equal(BookmarkMoveError.InvalidDropTarget, exception.Error);
        Assert.Same(bar, child.Parent);
    }

    private static BookmarkDocument Document(
        BookmarkFolder bookmarkBar,
        BookmarkFolder? other = null)
    {
        other ??= Folder("2", "Other bookmarks");
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

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
