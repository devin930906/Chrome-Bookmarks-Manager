using System.Text.Json;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Deleting;

public sealed class BookmarkRemovalDomainTests
{
    [Fact]
    public void RecordRemovedSubtree_UpdatesUrlAndFolderCountsExactly()
    {
        var nestedUrl = Url("12", "Nested");
        var nestedFolder = Folder("20", "Nested folder", nestedUrl);
        var directUrl = Url("10", "Direct");
        var bar = Folder("1", "Bookmarks bar", directUrl, nestedFolder);
        var document = Document(bar);

        Assert.Equal(2, document.UrlCount);
        Assert.Equal(4, document.FolderCount);

        document.RecordRemovedSubtree(
            removedUrlCount: 1,
            removedFolderCount: 1);

        Assert.Equal(1, document.UrlCount);
        Assert.Equal(3, document.FolderCount);
        Assert.Equal(4, document.TotalNodeCount);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void RecordRemovedSubtree_RejectsNegativeCounts(
        int removedUrlCount,
        int removedFolderCount)
    {
        var document = Document(Folder("1", "Bookmarks bar"));
        var beforeUrls = document.UrlCount;
        var beforeFolders = document.FolderCount;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => document.RecordRemovedSubtree(
                removedUrlCount,
                removedFolderCount));

        Assert.Equal(beforeUrls, document.UrlCount);
        Assert.Equal(beforeFolders, document.FolderCount);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 4)]
    public void RecordRemovedSubtree_RejectsUnderflowWithoutMutation(
        int removedUrlCount,
        int removedFolderCount)
    {
        var document = Document(Folder("1", "Bookmarks bar"));
        var beforeUrls = document.UrlCount;
        var beforeFolders = document.FolderCount;

        Assert.Throws<InvalidOperationException>(
            () => document.RecordRemovedSubtree(
                removedUrlCount,
                removedFolderCount));

        Assert.Equal(beforeUrls, document.UrlCount);
        Assert.Equal(beforeFolders, document.FolderCount);
    }

    [Fact]
    public void RemovalAccounting_DoesNotRewindFutureNodeIds()
    {
        var existing = Url("100", "Existing");
        var bar = Folder("1", "Bookmarks bar", existing);
        var document = Document(bar);
        var editing = new BookmarkEditingService();

        var removed = bar.RemoveChildAt(0);
        Assert.Same(existing, removed);

        document.RecordRemovedSubtree(
            removedUrlCount: 1,
            removedFolderCount: 0);

        var added = editing.AddBookmark(
            document,
            bar,
            "Replacement",
            "https://example.com/replacement");

        Assert.Equal("101", added.Id);
        Assert.Equal(1, document.UrlCount);
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

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
