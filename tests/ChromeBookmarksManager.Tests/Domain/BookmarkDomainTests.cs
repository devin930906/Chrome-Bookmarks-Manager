using System.Text.Json;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Domain;

public sealed class BookmarkDomainTests
{
    [Fact]
    public void Folder_ExposesOrderedChildrenAndParentLinks()
    {
        var child = new BookmarkUrl("2", Guid.Parse("11111111-1111-4111-8111-111111111111"), "Docs", "https://example.com/docs", "10", null, "0", null, EmptyProperties());
        var folder = new BookmarkFolder("1", Guid.Parse("22222222-2222-4222-8222-222222222222"), "Examples", "9", "11", null, null, EmptyProperties(), new BookmarkNode[] { child });

        Assert.Same(folder, child.Parent);
        Assert.Same(child, folder.Children[0]);
        Assert.Equal(BookmarkNodeKind.Url, child.Kind);
    }

    [Fact]
    public void Document_ReportsExactCountsAcrossAllRoots()
    {
        var barUrl = new BookmarkUrl("2", Guid.Parse("11111111-1111-4111-8111-111111111111"), "Docs", "custom-scheme:value", "10", null, null, null, EmptyProperties());
        var bar = Folder("1", "22222222-2222-4222-8222-222222222222", "Bar", barUrl);
        var other = Folder("3", "33333333-3333-4333-8333-333333333333", "Other");
        var synced = Folder("4", "44444444-4444-4444-8444-444444444444", "Mobile");
        var document = new BookmarkDocument(1, "md5", "sha256", new BookmarkRoots(bar, other, synced, EmptyProperties()), EmptyProperties());

        Assert.Equal(1, document.UrlCount);
        Assert.Equal(3, document.FolderCount);
        Assert.Equal(4, document.TotalNodeCount);
        Assert.Equal("custom-scheme:value", barUrl.Url);
    }

    private static BookmarkFolder Folder(string id, string guid, string name, params BookmarkNode[] children) =>
        new(id, Guid.Parse(guid), name, null, null, null, null, EmptyProperties(), children);

    private static IReadOnlyDictionary<string, JsonElement> EmptyProperties() =>
        new Dictionary<string, JsonElement>();
}
