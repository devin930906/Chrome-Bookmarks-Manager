using System.Text.Json;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Moving;

public sealed class BookmarkFolderMutationTests
{
    [Fact]
    public void RemoveChildAt_ReturnsExactChildAndClearsParent()
    {
        var first = Url("10", "First");
        var second = Url("11", "Second");
        var folder = Folder("1", first, second);

        var removed = folder.RemoveChildAt(0);

        Assert.Same(first, removed);
        Assert.Null(first.Parent);
        Assert.Same(second, Assert.Single(folder.Children));
        Assert.Same(folder, second.Parent);
    }

    [Fact]
    public void InsertChild_InsertsAtExactIndexAndAssignsParent()
    {
        var first = Url("10", "First");
        var second = Url("11", "Second");
        var folder = Folder("1", second);

        folder.InsertChild(0, first);

        Assert.Equal(2, folder.Children.Count);
        Assert.Same(first, folder.Children[0]);
        Assert.Same(second, folder.Children[1]);
        Assert.Same(folder, first.Parent);
    }

    [Fact]
    public void InsertChild_RejectsNodeThatAlreadyHasParent()
    {
        var child = Url("10", "Child");
        var source = Folder("1", child);
        var target = Folder("2");

        var exception = Assert.Throws<InvalidOperationException>(
            () => target.InsertChild(0, child));

        Assert.Contains("existing parent", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(source, child.Parent);
        Assert.Same(child, Assert.Single(source.Children));
        Assert.Empty(target.Children);
    }

    [Fact]
    public void RemoveAndInsert_RejectInvalidIndexes()
    {
        var child = Url("10", "Child");
        var folder = Folder("1", child);
        var detached = Url("11", "Detached");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => folder.RemoveChildAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => folder.RemoveChildAt(1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => folder.InsertChild(-1, detached));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => folder.InsertChild(2, detached));

        Assert.Same(folder, child.Parent);
        Assert.Null(detached.Parent);
        Assert.Same(child, Assert.Single(folder.Children));
    }

    [Fact]
    public void IndexOfChild_ReturnsReferencePositionOrMinusOne()
    {
        var first = Url("10", "First");
        var second = Url("11", "Second");
        var missing = Url("12", "Missing");
        var folder = Folder("1", first, second);

        Assert.Equal(0, folder.IndexOfChild(first));
        Assert.Equal(1, folder.IndexOfChild(second));
        Assert.Equal(-1, folder.IndexOfChild(missing));
    }

    private static BookmarkFolder Folder(
        string id,
        params BookmarkNode[] children) =>
        new(
            id,
            GuidFor(int.Parse(id)),
            $"Folder {id}",
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
