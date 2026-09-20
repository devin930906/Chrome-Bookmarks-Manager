using System.Text.Json;
using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Moving;

public sealed class BookmarkMoveServiceTests
{
    [Fact]
    public void MoveNode_BookmarkAcrossFolders_UpdatesParentAndPreservesCounts()
    {
        var fixture = CreateFixture();
        var service = new BookmarkMoveService();
        var originalId = fixture.BarFirst.Id;
        var originalGuid = fixture.BarFirst.Guid;
        var urlCount = fixture.Document.UrlCount;
        var folderCount = fixture.Document.FolderCount;

        var result = service.MoveNode(
            fixture.Document,
            fixture.BarFirst,
            fixture.Other,
            fixture.Other.Children.Count);

        Assert.True(result.Changed);
        Assert.Same(fixture.BookmarkBar, result.SourceParent);
        Assert.Equal(0, result.SourceIndex);
        Assert.Same(fixture.Other, result.TargetParent);
        Assert.Same(fixture.Other, fixture.BarFirst.Parent);
        Assert.DoesNotContain(
            fixture.BookmarkBar.Children,
            node => ReferenceEquals(node, fixture.BarFirst));
        Assert.Same(fixture.BarFirst, fixture.Other.Children[^1]);
        Assert.Equal(originalId, fixture.BarFirst.Id);
        Assert.Equal(originalGuid, fixture.BarFirst.Guid);
        Assert.Equal(urlCount, fixture.Document.UrlCount);
        Assert.Equal(folderCount, fixture.Document.FolderCount);
    }

    [Fact]
    public void MoveNode_FolderAcrossParents_PreservesWholeSubtreeAndIdentity()
    {
        var fixture = CreateFixture();
        var service = new BookmarkMoveService();
        var nested = fixture.DeepLeaf;
        var originalId = fixture.ChildFolder.Id;
        var originalGuid = fixture.ChildFolder.Guid;

        var result = service.MoveNode(
            fixture.Document,
            fixture.ChildFolder,
            fixture.Other,
            0);

        Assert.True(result.Changed);
        Assert.Same(fixture.Other, fixture.ChildFolder.Parent);
        Assert.Same(fixture.ChildFolder, fixture.Other.Children[0]);
        Assert.Same(fixture.ChildFolder, fixture.DeepFolder.Parent);
        Assert.Same(fixture.DeepFolder, nested.Parent);
        Assert.Equal(originalId, fixture.ChildFolder.Id);
        Assert.Equal(originalGuid, fixture.ChildFolder.Guid);
    }

    [Fact]
    public void MoveNode_RejectsPermanentChromeRoot()
    {
        var fixture = CreateFixture();
        var service = new BookmarkMoveService();

        var exception = Assert.Throws<BookmarkMoveException>(
            () => service.MoveNode(
                fixture.Document,
                fixture.BookmarkBar,
                fixture.Other,
                0));

        Assert.Equal(BookmarkMoveError.ProtectedRoot, exception.Error);
        Assert.Null(fixture.BookmarkBar.Parent);
    }

    [Fact]
    public void MoveNode_RejectsNodeFromAnotherDocument()
    {
        var fixture = CreateFixture();
        var foreign = CreateFixture("Foreign").BarFirst;
        var service = new BookmarkMoveService();

        var exception = Assert.Throws<BookmarkMoveException>(
            () => service.MoveNode(
                fixture.Document,
                foreign,
                fixture.Other,
                0));

        Assert.Equal(BookmarkMoveError.NodeNotInDocument, exception.Error);
    }

    [Fact]
    public void MoveNode_RejectsTargetFromAnotherDocument()
    {
        var fixture = CreateFixture();
        var foreignTarget = CreateFixture("Foreign").Other;
        var service = new BookmarkMoveService();

        var exception = Assert.Throws<BookmarkMoveException>(
            () => service.MoveNode(
                fixture.Document,
                fixture.BarFirst,
                foreignTarget,
                0));

        Assert.Equal(BookmarkMoveError.TargetNotInDocument, exception.Error);
        Assert.Same(fixture.BookmarkBar, fixture.BarFirst.Parent);
    }

    [Fact]
    public void MoveNode_RejectsFolderIntoItself()
    {
        var fixture = CreateFixture();
        var service = new BookmarkMoveService();

        var exception = Assert.Throws<BookmarkMoveException>(
            () => service.MoveNode(
                fixture.Document,
                fixture.ChildFolder,
                fixture.ChildFolder,
                0));

        Assert.Equal(BookmarkMoveError.SelfTarget, exception.Error);
    }

    [Fact]
    public void MoveNode_RejectsFolderIntoDeepDescendant()
    {
        var fixture = CreateFixture();
        var service = new BookmarkMoveService();

        var exception = Assert.Throws<BookmarkMoveException>(
            () => service.MoveNode(
                fixture.Document,
                fixture.ChildFolder,
                fixture.DeepFolder,
                fixture.DeepFolder.Children.Count));

        Assert.Equal(BookmarkMoveError.DescendantTarget, exception.Error);
        Assert.Same(fixture.BookmarkBar, fixture.ChildFolder.Parent);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void MoveNode_RejectsInvalidTargetIndex(int targetIndex)
    {
        var fixture = CreateFixture();
        var service = new BookmarkMoveService();

        var exception = Assert.Throws<BookmarkMoveException>(
            () => service.MoveNode(
                fixture.Document,
                fixture.BarFirst,
                fixture.Other,
                targetIndex));

        Assert.Equal(BookmarkMoveError.InvalidIndex, exception.Error);
        Assert.Same(fixture.BookmarkBar, fixture.BarFirst.Parent);
    }

    [Fact]
    public void MoveNode_SameParentForward_NormalizesRemovalShift()
    {
        var a = Url("10", "A");
        var b = Url("11", "B");
        var c = Url("12", "C");
        var d = Url("13", "D");
        var bar = Folder("1", "Bookmarks bar", a, b, c, d);
        var document = Document(bar);
        var service = new BookmarkMoveService();

        var result = service.MoveNode(document, b, bar, 4);

        Assert.True(result.Changed);
        Assert.Equal(new BookmarkNode[] { a, c, d, b }, bar.Children);
        Assert.Equal(3, result.TargetIndex);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void MoveNode_SameEffectivePosition_IsNoOp(int targetIndex)
    {
        var a = Url("10", "A");
        var b = Url("11", "B");
        var c = Url("12", "C");
        var bar = Folder("1", "Bookmarks bar", a, b, c);
        var document = Document(bar);
        var service = new BookmarkMoveService();

        var result = service.MoveNode(document, b, bar, targetIndex);

        Assert.False(result.Changed);
        Assert.Equal(new BookmarkNode[] { a, b, c }, bar.Children);
        Assert.Same(bar, b.Parent);
    }

    private static Fixture CreateFixture(string prefix = "")
    {
        var barFirst = Url("10", $"{prefix}First");
        var deepLeaf = Url("12", $"{prefix}Deep leaf");
        var deepFolder = Folder("30", $"{prefix}Deep", deepLeaf);
        var childFolder = Folder("20", $"{prefix}Child", deepFolder);
        var bookmarkBar = Folder(
            "1",
            $"{prefix}Bookmarks bar",
            barFirst,
            childFolder);
        var otherUrl = Url("11", $"{prefix}Other");
        var other = Folder("2", $"{prefix}Other bookmarks", otherUrl);
        var synced = Folder("3", $"{prefix}Mobile bookmarks");
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

        return new Fixture(
            document,
            bookmarkBar,
            other,
            synced,
            childFolder,
            deepFolder,
            deepLeaf,
            barFirst);
    }

    private static BookmarkDocument Document(BookmarkFolder bookmarkBar)
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
        BookmarkFolder Synced,
        BookmarkFolder ChildFolder,
        BookmarkFolder DeepFolder,
        BookmarkUrl DeepLeaf,
        BookmarkUrl BarFirst);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
