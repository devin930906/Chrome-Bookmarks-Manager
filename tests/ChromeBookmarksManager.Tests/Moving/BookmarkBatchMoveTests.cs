using System.Text.Json;
using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Moving;

public sealed class BookmarkBatchMoveTests
{
    [Fact]
    public void MoveBookmarksToEnd_AcrossParents_PreservesSuppliedOrderAndCounts()
    {
        var fixture = CreateFixture();
        var service = new BookmarkMoveService();
        var beforeUrls = fixture.Document.UrlCount;
        var beforeFolders = fixture.Document.FolderCount;

        var result = service.MoveBookmarksToEnd(
            fixture.Document,
            new[]
            {
                fixture.BarSecond,
                fixture.OtherFirst,
                fixture.BarFirst
            },
            fixture.Target);

        Assert.True(result.Changed);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.TargetExistingFolder,
                fixture.TargetEarlierBookmark,
                fixture.TargetExistingBookmark,
                fixture.BarSecond,
                fixture.OtherFirst,
                fixture.BarFirst
            },
            fixture.Target.Children);
        Assert.Equal(beforeUrls, fixture.Document.UrlCount);
        Assert.Equal(beforeFolders, fixture.Document.FolderCount);
        Assert.Same(fixture.Target, fixture.BarSecond.Parent);
        Assert.Same(fixture.Target, fixture.OtherFirst.Parent);
        Assert.Same(fixture.Target, fixture.BarFirst.Parent);
    }

    [Fact]
    public void MoveBookmarksToEnd_DeduplicatesReferencesAndPreservesFirstOccurrenceOrder()
    {
        var fixture = CreateFixture();
        var service = new BookmarkMoveService();

        var result = service.MoveBookmarksToEnd(
            fixture.Document,
            new[]
            {
                fixture.BarFirst,
                fixture.BarSecond,
                fixture.BarFirst,
                fixture.BarSecond
            },
            fixture.Target);

        Assert.True(result.Changed);
        Assert.Equal(2, result.Moves.Count);
        Assert.Same(
            fixture.BarFirst,
            fixture.Target.Children[^2]);
        Assert.Same(
            fixture.BarSecond,
            fixture.Target.Children[^1]);
    }

    [Fact]
    public void MoveBookmarksToEnd_SameParentMovesSelectedGroupToRealEnd()
    {
        var fixture = CreateFixture();
        var service = new BookmarkMoveService();

        var result = service.MoveBookmarksToEnd(
            fixture.Document,
            new[]
            {
                fixture.TargetExistingBookmark,
                fixture.TargetEarlierBookmark
            },
            fixture.Target);

        Assert.True(result.Changed);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.TargetExistingFolder,
                fixture.TargetExistingBookmark,
                fixture.TargetEarlierBookmark
            },
            fixture.Target.Children);
    }

    [Fact]
    public void MoveBookmarksToEnd_AlreadyEffectiveEndState_IsNoOp()
    {
        var fixture = CreateFixture();
        var service = new BookmarkMoveService();

        var result = service.MoveBookmarksToEnd(
            fixture.Document,
            new[]
            {
                fixture.TargetExistingBookmark
            },
            fixture.Target);

        Assert.False(result.Changed);
        Assert.Empty(result.Moves);
    }

    [Fact]
    public void MoveBookmarksToEnd_RejectsForeignBookmarkBeforeMutation()
    {
        var fixture = CreateFixture();
        var foreign = CreateFixture("Foreign ").BarFirst;
        var service = new BookmarkMoveService();
        var beforeBar = fixture.BookmarkBar.Children.ToArray();
        var beforeTarget = fixture.Target.Children.ToArray();

        var exception = Assert.Throws<BookmarkMoveException>(
            () => service.MoveBookmarksToEnd(
                fixture.Document,
                new[]
                {
                    fixture.BarFirst,
                    foreign,
                    fixture.BarSecond
                },
                fixture.Target));

        Assert.Equal(
            BookmarkMoveError.NodeNotInDocument,
            exception.Error);
        Assert.Equal(beforeBar, fixture.BookmarkBar.Children);
        Assert.Equal(beforeTarget, fixture.Target.Children);
    }

    [Fact]
    public void MoveBookmarksToEnd_RejectsForeignTargetBeforeMutation()
    {
        var fixture = CreateFixture();
        var foreignTarget = CreateFixture("Foreign ").Target;
        var service = new BookmarkMoveService();
        var beforeBar = fixture.BookmarkBar.Children.ToArray();

        var exception = Assert.Throws<BookmarkMoveException>(
            () => service.MoveBookmarksToEnd(
                fixture.Document,
                new[]
                {
                    fixture.BarFirst,
                    fixture.BarSecond
                },
                foreignTarget));

        Assert.Equal(
            BookmarkMoveError.TargetNotInDocument,
            exception.Error);
        Assert.Equal(beforeBar, fixture.BookmarkBar.Children);
    }

    [Fact]
    public void MoveBookmarksToEnd_EmptySelection_IsNoOp()
    {
        var fixture = CreateFixture();
        var service = new BookmarkMoveService();

        var result = service.MoveBookmarksToEnd(
            fixture.Document,
            Array.Empty<BookmarkUrl>(),
            fixture.Target);

        Assert.False(result.Changed);
        Assert.Empty(result.Moves);
    }

    [Fact]
    public void MoveNodes_MixedSelection_PreservesSuppliedOrderAcrossParents()
    {
        var fixture = CreateFixture();
        var service = new BookmarkMoveService();
        var beforeUrls = fixture.Document.UrlCount;
        var beforeFolders = fixture.Document.FolderCount;

        var result = service.MoveNodes(
            fixture.Document,
            new BookmarkNode[]
            {
                fixture.BarSecond,
                fixture.BarFolder,
                fixture.BarFirst
            },
            fixture.Target,
            fixture.Target.Children.Count);

        Assert.True(result.Changed);
        Assert.Equal(3, result.Moves.Count);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.TargetExistingFolder,
                fixture.TargetEarlierBookmark,
                fixture.TargetExistingBookmark,
                fixture.BarSecond,
                fixture.BarFolder,
                fixture.BarFirst
            },
            fixture.Target.Children);
        Assert.Same(fixture.Target, fixture.BarSecond.Parent);
        Assert.Same(fixture.Target, fixture.BarFolder.Parent);
        Assert.Same(fixture.Target, fixture.BarFirst.Parent);
        Assert.Equal(beforeUrls, fixture.Document.UrlCount);
        Assert.Equal(beforeFolders, fixture.Document.FolderCount);
    }

    [Fact]
    public void MoveNodes_SameFolderTargetIndex_NormalizesAfterRemovingSelectedNodes()
    {
        var fixture = CreateFixture();
        var service = new BookmarkMoveService();

        var result = service.MoveNodes(
            fixture.Document,
            new BookmarkNode[]
            {
                fixture.TargetExistingFolder,
                fixture.TargetExistingBookmark
            },
            fixture.Target,
            fixture.Target.Children.Count);

        Assert.True(result.Changed);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.TargetEarlierBookmark,
                fixture.TargetExistingFolder,
                fixture.TargetExistingBookmark
            },
            fixture.Target.Children);
    }

    [Fact]
    public void MoveNodes_AncestorAndDescendantSelection_MovesSubtreeOnlyOnce()
    {
        var nested = Url("31", "Nested");
        var movingFolder = Folder("30", "Moving", nested);
        var sibling = Url("32", "Sibling");
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            movingFolder,
            sibling);
        var other = Folder("2", "Other bookmarks");
        var synced = Folder("3", "Mobile bookmarks");
        var document = new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                bookmarkBar,
                other,
                synced,
                EmptyProperties),
            EmptyProperties);
        var service = new BookmarkMoveService();

        var result = service.MoveNodes(
            document,
            new BookmarkNode[]
            {
                nested,
                movingFolder
            },
            other,
            0);

        Assert.True(result.Changed);
        Assert.Single(result.Moves);
        Assert.Equal(
            new BookmarkNode[] { sibling },
            bookmarkBar.Children);
        Assert.Same(
            movingFolder,
            Assert.Single(other.Children));
        Assert.Same(movingFolder, nested.Parent);
    }

    [Fact]
    public void MoveNodes_DescendantTarget_RejectsEntireBatchBeforeMutation()
    {
        var nestedTarget = Folder("31", "Nested target");
        var movingFolder = Folder("30", "Moving", nestedTarget);
        var sibling = Url("32", "Sibling");
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            sibling,
            movingFolder);
        var other = Folder("2", "Other bookmarks");
        var synced = Folder("3", "Mobile bookmarks");
        var document = new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                bookmarkBar,
                other,
                synced,
                EmptyProperties),
            EmptyProperties);
        var service = new BookmarkMoveService();
        var beforeBar = bookmarkBar.Children.ToArray();

        var exception = Assert.Throws<BookmarkMoveException>(
            () => service.MoveNodes(
                document,
                new BookmarkNode[]
                {
                    sibling,
                    movingFolder
                },
                nestedTarget,
                0));

        Assert.Equal(
            BookmarkMoveError.DescendantTarget,
            exception.Error);
        Assert.Equal(beforeBar, bookmarkBar.Children);
        Assert.Same(bookmarkBar, sibling.Parent);
        Assert.Same(bookmarkBar, movingFolder.Parent);
        Assert.Same(movingFolder, nestedTarget.Parent);
    }

    private static Fixture CreateFixture(string prefix = "")
    {
        var barFirst = Url("10", $"{prefix}Bar first");
        var barFolder = Folder("20", $"{prefix}Bar folder");
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

        var targetExistingFolder = Folder(
            "21",
            $"{prefix}Target folder slot");
        var targetEarlierBookmark = Url(
            "13",
            $"{prefix}Target earlier");
        var targetExistingBookmark = Url(
            "14",
            $"{prefix}Target existing");
        var target = Folder(
            "22",
            $"{prefix}Target",
            targetExistingFolder,
            targetEarlierBookmark,
            targetExistingBookmark);

        var synced = Folder(
            "3",
            $"{prefix}Mobile bookmarks",
            target);

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
            target,
            barFirst,
            barFolder,
            barSecond,
            otherFirst,
            targetExistingFolder,
            targetEarlierBookmark,
            targetExistingBookmark);
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

    private sealed record Fixture(
        BookmarkDocument Document,
        BookmarkFolder BookmarkBar,
        BookmarkFolder Other,
        BookmarkFolder Target,
        BookmarkUrl BarFirst,
        BookmarkFolder BarFolder,
        BookmarkUrl BarSecond,
        BookmarkUrl OtherFirst,
        BookmarkFolder TargetExistingFolder,
        BookmarkUrl TargetEarlierBookmark,
        BookmarkUrl TargetExistingBookmark);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
