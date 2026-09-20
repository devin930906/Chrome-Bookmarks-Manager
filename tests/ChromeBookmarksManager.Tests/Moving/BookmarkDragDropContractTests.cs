using System.Text.Json;
using ChromeBookmarksManager.DragDrop;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Moving;

public sealed class BookmarkDragDropContractTests
{
    [Fact]
    public void Payload_PreservesExactDraggedNodeReference()
    {
        var bookmark = Url("10", "Dragged");

        var payload = new BookmarkDragPayload(bookmark);

        Assert.Same(bookmark, payload.Node);
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
