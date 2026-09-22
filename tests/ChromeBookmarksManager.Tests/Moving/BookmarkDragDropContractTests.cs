using System.Text.Json;
using ChromeBookmarksManager.DragDrop;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Moving;

public sealed class BookmarkDragDropContractTests
{
    [Fact]
    public void Payload_SelectedPointerCapturesOrderedSelectionAndCopiesIt()
    {
        var first = Url("10", "First");
        var folder = Folder("20", "Folder");
        var second = Url("11", "Second");
        var selected = new List<BookmarkNode>
        {
            first,
            folder,
            second
        };

        var payload = BookmarkDragPayload.ForContentRow(
            folder,
            selected);

        selected.Clear();

        Assert.Equal(
            new BookmarkNode[]
            {
                first,
                folder,
                second
            },
            payload.Nodes);
    }

    [Fact]
    public void Payload_UnselectedPointerCapturesOnlyPointerRow()
    {
        var first = Url("10", "First");
        var second = Url("11", "Second");
        var pointer = Folder("20", "Pointer folder");

        var payload = BookmarkDragPayload.ForContentRow(
            pointer,
            new BookmarkNode[]
            {
                first,
                second
            });

        var dragged = Assert.Single(payload.Nodes);
        Assert.Same(pointer, dragged);
    }

    [Fact]
    public void Payload_ConstructorRejectsEmptyNodeSet()
    {
        Assert.Throws<ArgumentException>(
            () => new BookmarkDragPayload(
                Array.Empty<BookmarkNode>()));
    }

    [Theory]
    [InlineData(0, 0, 4, 4)]
    [InlineData(4, 0, 4, 4)]
    [InlineData(0, -4, 4, 4)]
    [InlineData(-4, 0, 4, 4)]
    public void DragThreshold_DoesNotStartUntilPointerExceedsSystemThreshold(
        double deltaX,
        double deltaY,
        double horizontalThreshold,
        double verticalThreshold)
    {
        Assert.False(
            DragDropRules.HasExceededDragThreshold(
                deltaX,
                deltaY,
                horizontalThreshold,
                verticalThreshold));
    }

    [Theory]
    [InlineData(4.01, 0, 4, 4)]
    [InlineData(-4.01, 0, 4, 4)]
    [InlineData(0, 4.01, 4, 4)]
    [InlineData(0, -4.01, 4, 4)]
    public void DragThreshold_StartsWhenEitherAxisExceedsSystemThreshold(
        double deltaX,
        double deltaY,
        double horizontalThreshold,
        double verticalThreshold)
    {
        Assert.True(
            DragDropRules.HasExceededDragThreshold(
                deltaX,
                deltaY,
                horizontalThreshold,
                verticalThreshold));
    }

    [Theory]
    [InlineData(0, 20, "Before")]
    [InlineData(9.99, 20, "Before")]
    [InlineData(10, 20, "After")]
    [InlineData(20, 20, "After")]
    public void BookmarkRowPlacement_UsesUpperAndLowerHalves(
        double pointerY,
        double rowHeight,
        string expectedName)
    {
        Assert.Equal(
            Enum.Parse<DropPlacement>(expectedName),
            DragDropRules.GetBookmarkRowPlacement(
                pointerY,
                rowHeight));
    }

    [Fact]
    public void BookmarkRowPlacement_RejectsNonPositiveHeight()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DragDropRules.GetBookmarkRowPlacement(0, 0));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void PositionalBookmarkDrop_IsDisabledWhileSearchResultsAreDisplayed(
        bool isSearchActive,
        bool expected)
    {
        Assert.Equal(
            expected,
            DragDropRules.CanPositionallyReorderBookmarks(isSearchActive));
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
