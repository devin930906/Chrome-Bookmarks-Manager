using System.Text.Json;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Editing;

public sealed class BookmarkCreationTests
{
    private static readonly DateTimeOffset FixedNow =
        new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ChromeBookmarkTime_UsesMicrosecondsSinceWindowsEpoch()
    {
        Assert.Equal("0", ChromeBookmarkTime.ToRaw(new DateTimeOffset(1601, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.Equal("11644473600000000", ChromeBookmarkTime.ToRaw(FixedNow));
    }

    [Fact]
    public void AddBookmark_AppendsChromeCompatibleNodeAndUpdatesCounts()
    {
        var (document, target) = CreateDocument();
        var service = new BookmarkEditingService(new FixedTimeProvider(FixedNow));
        var originalUrlCount = document.UrlCount;
        var originalFolderCount = document.FolderCount;
        var originalChildCount = target.Children.Count;

        var bookmark = service.AddBookmark(
            document,
            target,
            "New\nBookmark",
            "custom-scheme:new-value");

        Assert.Equal("1000", bookmark.Id);
        Assert.Equal('4', bookmark.Guid.ToString("D")[14]);
        Assert.NotEqual(Guid.Empty, bookmark.Guid);
        Assert.Equal("New Bookmark", bookmark.Name);
        Assert.Equal("custom-scheme:new-value", bookmark.Url);
        Assert.Equal("11644473600000000", bookmark.DateAddedRaw);
        Assert.Null(bookmark.DateModifiedRaw);
        Assert.Equal("0", bookmark.DateLastUsedRaw);
        Assert.Null(bookmark.MetaInfo);
        Assert.Empty(bookmark.ExtensionData);
        Assert.Same(target, bookmark.Parent);
        Assert.Same(bookmark, target.Children[originalChildCount]);
        Assert.Equal(originalUrlCount + 1, document.UrlCount);
        Assert.Equal(originalFolderCount, document.FolderCount);
        Assert.Equal(document.UrlCount + document.FolderCount, document.TotalNodeCount);
    }

    [Fact]
    public void AddFolder_UsesNextSequentialIdRandomV4GuidAndCurrentChromeTimes()
    {
        var (document, target) = CreateDocument();
        var service = new BookmarkEditingService(new FixedTimeProvider(FixedNow));

        var bookmark = service.AddBookmark(document, target, "First", "https://example.test/first");
        var folder = service.AddFolder(document, target, "New\tFolder");

        Assert.Equal("1000", bookmark.Id);
        Assert.Equal("1001", folder.Id);
        Assert.NotEqual(bookmark.Guid, folder.Guid);
        Assert.Equal('4', folder.Guid.ToString("D")[14]);
        Assert.Equal("New Folder", folder.Name);
        Assert.Equal("11644473600000000", folder.DateAddedRaw);
        Assert.Equal("11644473600000000", folder.DateModifiedRaw);
        Assert.Equal("0", folder.DateLastUsedRaw);
        Assert.Empty(folder.Children);
        Assert.Same(target, folder.Parent);
        Assert.Equal(2, document.UrlCount);
        Assert.Equal(5, document.FolderCount);
        Assert.Equal(7, document.TotalNodeCount);
    }

    [Fact]
    public void AddBookmark_AllowsPermanentChromeRootAsParent()
    {
        var (document, _) = CreateDocument();
        var service = new BookmarkEditingService(new FixedTimeProvider(FixedNow));

        var bookmark = service.AddBookmark(
            document,
            document.Roots.Other,
            "Root child",
            "https://example.test/root-child");

        Assert.Same(document.Roots.Other, bookmark.Parent);
        Assert.Same(bookmark, document.Roots.Other.Children[^1]);
    }

    [Fact]
    public void AddFolder_AllowsPermanentChromeRootAsParent()
    {
        var (document, _) = CreateDocument();
        var service = new BookmarkEditingService(new FixedTimeProvider(FixedNow));

        var folder = service.AddFolder(document, document.Roots.Synced, "Root Folder");

        Assert.Same(document.Roots.Synced, folder.Parent);
        Assert.Same(folder, document.Roots.Synced.Children[^1]);
    }

    [Fact]
    public void AddBookmark_RejectsEmptyUrlWithoutMutatingDocument()
    {
        var (document, target) = CreateDocument();
        var service = new BookmarkEditingService(new FixedTimeProvider(FixedNow));
        var urls = document.UrlCount;
        var total = document.TotalNodeCount;
        var children = target.Children.Count;

        var exception = Assert.Throws<BookmarkEditException>(
            () => service.AddBookmark(document, target, "Invalid", "   "));

        Assert.Equal(BookmarkEditError.InvalidValue, exception.Error);
        Assert.Equal(urls, document.UrlCount);
        Assert.Equal(total, document.TotalNodeCount);
        Assert.Equal(children, target.Children.Count);
    }

    [Fact]
    public void AddFolder_RejectsParentFromDifferentDocument()
    {
        var (document, _) = CreateDocument();
        var (_, foreignTarget) = CreateDocument(2000);
        var service = new BookmarkEditingService(new FixedTimeProvider(FixedNow));

        var exception = Assert.Throws<BookmarkEditException>(
            () => service.AddFolder(document, foreignTarget, "Foreign"));

        Assert.Equal(BookmarkEditError.NodeNotInDocument, exception.Error);
    }

    [Fact]
    public void AddOperations_KeepPublicChildrenCollectionReadOnly()
    {
        var (document, target) = CreateDocument();
        var service = new BookmarkEditingService(new FixedTimeProvider(FixedNow));

        service.AddBookmark(document, target, "Added", "https://example.test/added");

        var list = Assert.IsAssignableFrom<IList<BookmarkNode>>(target.Children);
        Assert.True(list.IsReadOnly);
    }

    private static (BookmarkDocument Document, BookmarkFolder Target) CreateDocument(int idOffset = 0)
    {
        var existingBookmark = new BookmarkUrl(
            (idOffset + 999).ToString(),
            GuidFrom(idOffset + 999),
            "Existing",
            "https://example.test/existing",
            "100",
            null,
            "0",
            null,
            EmptyProperties());

        var target = new BookmarkFolder(
            (idOffset + 10).ToString(),
            GuidFrom(idOffset + 10),
            "Target",
            "90",
            "95",
            null,
            null,
            EmptyProperties(),
            new BookmarkNode[] { existingBookmark });

        var bar = Folder(idOffset + 1, "Bookmarks bar", target);
        var other = Folder(idOffset + 50, "Other bookmarks");
        var synced = Folder(idOffset + 3, "Mobile bookmarks");

        return (
            new BookmarkDocument(
                1,
                "md5",
                "sha256",
                new BookmarkRoots(bar, other, synced, EmptyProperties()),
                EmptyProperties()),
            target);
    }

    private static BookmarkFolder Folder(int id, string name, params BookmarkNode[] children) =>
        new(
            id.ToString(),
            GuidFrom(id),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties(),
            children);

    private static Guid GuidFrom(int value) =>
        Guid.Parse($"00000000-0000-4000-8000-{value:000000000000}");

    private static IReadOnlyDictionary<string, JsonElement> EmptyProperties() =>
        new Dictionary<string, JsonElement>();

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
