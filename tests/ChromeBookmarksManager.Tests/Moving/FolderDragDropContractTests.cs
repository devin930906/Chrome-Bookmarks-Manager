using System.Text.Json;
using ChromeBookmarksManager.DragDrop;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Moving;

public sealed class FolderDragDropContractTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void FolderDragSource_RejectsPermanentRoots(
        bool isPermanentRoot,
        bool expected)
    {
        Assert.Equal(
            expected,
            DragDropRules.CanStartFolderDrag(isPermanentRoot));
    }

    [Theory]
    [InlineData(0, 30, false, "Before")]
    [InlineData(9.99, 30, false, "Before")]
    [InlineData(10, 30, false, "Into")]
    [InlineData(20, 30, false, "Into")]
    [InlineData(20.01, 30, false, "After")]
    [InlineData(30, 30, false, "After")]
    [InlineData(0, 30, true, "Into")]
    [InlineData(15, 30, true, "Into")]
    [InlineData(30, 30, true, "Into")]
    public void FolderRowPlacement_UsesThirdsAndRootsOnlyAcceptInto(
        double pointerY,
        double rowHeight,
        bool isPermanentRootTarget,
        string expectedName)
    {
        Assert.Equal(
            Enum.Parse<DropPlacement>(expectedName),
            DragDropRules.GetFolderRowPlacement(
                pointerY,
                rowHeight,
                isPermanentRootTarget));
    }

    [Fact]
    public void FolderIntoValidation_RejectsSelfAndDeepDescendant()
    {
        var deep = Folder("23", "Deep");
        var child = Folder("22", "Child", deep);
        var moving = Folder("20", "Moving", child);
        var unrelated = Folder("21", "Unrelated");
        var bar = Folder("1", "Bookmarks bar", moving, unrelated);
        _ = Document(bar);

        Assert.False(DragDropRules.CanMoveFolderInto(moving, moving));
        Assert.False(DragDropRules.CanMoveFolderInto(moving, child));
        Assert.False(DragDropRules.CanMoveFolderInto(moving, deep));
        Assert.True(DragDropRules.CanMoveFolderInto(moving, unrelated));
    }

    [Fact]
    public void FolderRelativeValidation_RejectsRootsSelfAndDescendantBranches()
    {
        var deep = Folder("23", "Deep");
        var child = Folder("22", "Child", deep);
        var moving = Folder("20", "Moving", child);
        var sibling = Folder("21", "Sibling");
        var bar = Folder("1", "Bookmarks bar", moving, sibling);
        var document = Document(bar);

        Assert.False(
            DragDropRules.CanMoveFolderRelativeTo(
                moving,
                document.Roots.Other));
        Assert.False(
            DragDropRules.CanMoveFolderRelativeTo(
                moving,
                moving));
        Assert.False(
            DragDropRules.CanMoveFolderRelativeTo(
                moving,
                child));
        Assert.False(
            DragDropRules.CanMoveFolderRelativeTo(
                moving,
                deep));
        Assert.True(
            DragDropRules.CanMoveFolderRelativeTo(
                moving,
                sibling));
    }

    [Fact]
    public void FolderIntoValidation_AllowsMovingAcrossChromeRoots()
    {
        var moving = Folder("20", "Moving");
        var bar = Folder("1", "Bookmarks bar", moving);
        var document = Document(bar);

        Assert.True(
            DragDropRules.CanMoveFolderInto(
                moving,
                document.Roots.Other));
        Assert.True(
            DragDropRules.CanMoveFolderInto(
                moving,
                document.Roots.Synced));
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
