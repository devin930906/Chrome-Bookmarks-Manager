using System.Text.Json;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Editing;

public sealed class BookmarkEditingServiceTests
{
    private readonly BookmarkEditingService _service = new();

    [Fact]
    public void RenameNode_RenamesOrdinaryFolderInPlaceAndPreservesIdentity()
    {
        var (document, folder, bookmark) = CreateDocument();
        var id = folder.Id;
        var guid = folder.Guid;
        var metadata = folder.MetaInfo;
        var extensionData = folder.ExtensionData;
        var parent = folder.Parent;

        var changed = _service.RenameNode(document, folder, "Renamed Folder");

        Assert.True(changed);
        Assert.Equal("Renamed Folder", folder.Name);
        Assert.Equal(id, folder.Id);
        Assert.Equal(guid, folder.Guid);
        Assert.Same(metadata, folder.MetaInfo);
        Assert.Same(extensionData, folder.ExtensionData);
        Assert.Same(parent, folder.Parent);
        Assert.Same(bookmark, folder.Children[0]);
    }

    [Fact]
    public void RenameNode_RenamesBookmarkInPlaceAndSanitizesChromiumInvalidWhitespace()
    {
        var (document, _, bookmark) = CreateDocument();
        var id = bookmark.Id;
        var guid = bookmark.Guid;
        var url = bookmark.Url;
        var parent = bookmark.Parent;

        var changed = _service.RenameNode(document, bookmark, "Line1\nLine2\tTitle");

        Assert.True(changed);
        Assert.Equal("Line1 Line2 Title", bookmark.Name);
        Assert.Equal(id, bookmark.Id);
        Assert.Equal(guid, bookmark.Guid);
        Assert.Equal(url, bookmark.Url);
        Assert.Same(parent, bookmark.Parent);
    }

    [Fact]
    public void EditUrl_ChangesOnlyUrlAndPreservesNodeIdentityAndMetadata()
    {
        var (document, _, bookmark) = CreateDocument();
        var name = bookmark.Name;
        var id = bookmark.Id;
        var guid = bookmark.Guid;
        var metadata = bookmark.MetaInfo;
        var extensionData = bookmark.ExtensionData;
        var parent = bookmark.Parent;

        var changed = _service.EditUrl(document, bookmark, "custom-scheme:new-value");

        Assert.True(changed);
        Assert.Equal("custom-scheme:new-value", bookmark.Url);
        Assert.Equal(name, bookmark.Name);
        Assert.Equal(id, bookmark.Id);
        Assert.Equal(guid, bookmark.Guid);
        Assert.Same(metadata, bookmark.MetaInfo);
        Assert.Same(extensionData, bookmark.ExtensionData);
        Assert.Same(parent, bookmark.Parent);
    }

    [Fact]
    public void RenameNode_NoOpReturnsFalse()
    {
        var (document, folder, _) = CreateDocument();

        var changed = _service.RenameNode(document, folder, folder.Name);

        Assert.False(changed);
    }

    [Fact]
    public void EditUrl_NoOpReturnsFalse()
    {
        var (document, _, bookmark) = CreateDocument();

        var changed = _service.EditUrl(document, bookmark, bookmark.Url);

        Assert.False(changed);
    }

    [Fact]
    public void EditUrl_RejectsEmptyValue()
    {
        var (document, _, bookmark) = CreateDocument();

        var exception = Assert.Throws<BookmarkEditException>(
            () => _service.EditUrl(document, bookmark, string.Empty));

        Assert.Equal(BookmarkEditError.InvalidValue, exception.Error);
    }

    [Theory]
    [InlineData("bookmark_bar")]
    [InlineData("other")]
    [InlineData("synced")]
    public void RenameNode_RejectsPermanentRoots(string rootName)
    {
        var (document, _, _) = CreateDocument();
        BookmarkNode root = rootName switch
        {
            "bookmark_bar" => document.Roots.BookmarkBar,
            "other" => document.Roots.Other,
            _ => document.Roots.Synced
        };

        var exception = Assert.Throws<BookmarkEditException>(
            () => _service.RenameNode(document, root, "Forbidden"));

        Assert.Equal(BookmarkEditError.ProtectedRoot, exception.Error);
    }

    [Fact]
    public void RenameNode_RejectsNodeFromDifferentDocument()
    {
        var (document, _, _) = CreateDocument();
        var (_, foreignFolder, _) = CreateDocument(idOffset: 100);

        var exception = Assert.Throws<BookmarkEditException>(
            () => _service.RenameNode(document, foreignFolder, "Foreign"));

        Assert.Equal(BookmarkEditError.NodeNotInDocument, exception.Error);
    }

    [Fact]
    public void EditUrl_RejectsBookmarkFromDifferentDocument()
    {
        var (document, _, _) = CreateDocument();
        var (_, _, foreignBookmark) = CreateDocument(idOffset: 100);

        var exception = Assert.Throws<BookmarkEditException>(
            () => _service.EditUrl(document, foreignBookmark, "https://example.com/new"));

        Assert.Equal(BookmarkEditError.NodeNotInDocument, exception.Error);
    }

    [Fact]
    public void FolderChildren_RemainReadOnlyFacingAfterEditingSurfaceIsIntroduced()
    {
        var (_, folder, bookmark) = CreateDocument();

        var list = Assert.IsAssignableFrom<IList<BookmarkNode>>(folder.Children);

        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list.Add(bookmark));
    }

    private static (BookmarkDocument Document, BookmarkFolder Folder, BookmarkUrl Bookmark) CreateDocument(int idOffset = 0)
    {
        var bookmark = new BookmarkUrl(
            (idOffset + 5).ToString(),
            GuidFrom(idOffset + 5),
            "Docs",
            "https://example.com/docs",
            "13300000000000000",
            null,
            "0",
            Json("{" + "\"source\":\"test\"" + "}"),
            Properties(("future_url", "\"kept\"")));

        var folder = new BookmarkFolder(
            (idOffset + 4).ToString(),
            GuidFrom(idOffset + 4),
            "Folder",
            "13300000000000000",
            "13300000000000000",
            null,
            Json("{" + "\"folder\":true" + "}"),
            Properties(("future_folder", "123")),
            new BookmarkNode[] { bookmark });

        var bar = new BookmarkFolder(
            (idOffset + 1).ToString(),
            GuidFrom(idOffset + 1),
            "Bookmarks bar",
            null,
            null,
            null,
            null,
            EmptyProperties(),
            new BookmarkNode[] { folder });

        var other = new BookmarkFolder(
            (idOffset + 2).ToString(),
            GuidFrom(idOffset + 2),
            "Other bookmarks",
            null,
            null,
            null,
            null,
            EmptyProperties(),
            Array.Empty<BookmarkNode>());

        var synced = new BookmarkFolder(
            (idOffset + 3).ToString(),
            GuidFrom(idOffset + 3),
            "Mobile bookmarks",
            null,
            null,
            null,
            null,
            EmptyProperties(),
            Array.Empty<BookmarkNode>());

        var document = new BookmarkDocument(
            1,
            "md5",
            "sha256",
            new BookmarkRoots(bar, other, synced, EmptyProperties()),
            EmptyProperties());

        return (document, folder, bookmark);
    }

    private static Guid GuidFrom(int value) =>
        Guid.Parse($"00000000-0000-4000-8000-{value:000000000000}");

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static IReadOnlyDictionary<string, JsonElement> Properties(params (string Key, string Json)[] values)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, json) in values)
        {
            result[key] = Json(json);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, JsonElement> EmptyProperties() =>
        new Dictionary<string, JsonElement>();
}
