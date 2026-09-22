using System.Text.Json;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Search;

public sealed class BookmarkNodeSearchParityTests
{
    [Fact]
    public async Task BuildIndexAsync_IncludesFoldersAndBookmarksInDeterministicTraversalOrder()
    {
        var nestedUrl = Url("12", "Nested target", "https://example.com/nested");
        var childFolder = Folder("20", "Target folder", nestedUrl);
        var first = Url("10", "First target", "https://example.com/first");
        var otherUrl = Url("11", "Other target", "https://example.com/other");
        var document = Document(
            Folder("1", "Bookmarks bar", first, childFolder),
            Folder("2", "Other bookmarks", otherUrl),
            Folder("3", "Mobile bookmarks"));
        var service = new BookmarkSearchService();

        var index = await service.BuildIndexAsync(
            document,
            CancellationToken.None);

        Assert.Equal(
            new BookmarkNode[]
            {
                first,
                childFolder,
                nestedUrl,
                otherUrl
            },
            index.Nodes);
    }

    [Fact]
    public async Task SearchAsync_AllBookmarks_MatchesFolderTitleAndBookmarkTitleOrUrl()
    {
        var folder = Folder("20", "Target folder");
        var bookmarkByTitle = Url(
            "10",
            "Target bookmark",
            "https://example.com/first");
        var bookmarkByUrl = Url(
            "11",
            "Reference",
            "https://target.example/path");
        var document = Document(
            Folder(
                "1",
                "Bookmarks bar",
                folder,
                bookmarkByTitle,
                bookmarkByUrl),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
        var service = new BookmarkSearchService();
        var index = await service.BuildIndexAsync(
            document,
            CancellationToken.None);

        var results = await service.SearchAsync(
            index,
            "target",
            BookmarkSearchScope.AllBookmarks,
            null,
            CancellationToken.None);

        Assert.Equal(
            new BookmarkNode[]
            {
                folder,
                bookmarkByTitle,
                bookmarkByUrl
            },
            results);
    }

    [Fact]
    public async Task SearchAsync_CurrentFolder_MatchesDirectFoldersAndBookmarksOnly()
    {
        var descendant = Url(
            "13",
            "Target descendant",
            "https://example.com/descendant");
        var directFolder = Folder(
            "20",
            "Target folder",
            descendant);
        var directBookmark = Url(
            "10",
            "Target bookmark",
            "https://example.com/direct");
        var nonMatch = Url(
            "11",
            "Other",
            "https://example.com/other");
        var current = Folder(
            "1",
            "Bookmarks bar",
            directFolder,
            directBookmark,
            nonMatch);
        var document = Document(
            current,
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
        var service = new BookmarkSearchService();
        var index = await service.BuildIndexAsync(
            document,
            CancellationToken.None);

        var results = await service.SearchAsync(
            index,
            "target",
            BookmarkSearchScope.CurrentFolder,
            current,
            CancellationToken.None);

        Assert.Equal(
            new BookmarkNode[]
            {
                directFolder,
                directBookmark
            },
            results);
        Assert.DoesNotContain(descendant, results);
    }

    [Fact]
    public async Task MainViewModel_SearchFolderResult_CanNavigateIntoOriginalFolder()
    {
        var nested = Url(
            "11",
            "Nested",
            "https://example.com/nested");
        var targetFolder = Folder(
            "20",
            "Target folder",
            nested);
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            Url("10", "First", "https://example.com/first"),
            targetFolder);
        var document = Document(
            bookmarkBar,
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
        var viewModel = new MainViewModel(
            new StubReader((_, _) => Task.FromResult(document)),
            new BookmarkSearchService(),
            TimeSpan.Zero);

        await viewModel.LoadBookmarksAsync(
            @"C:\Synthetic\Bookmarks");
        viewModel.SearchText = "target";
        await viewModel.WaitForPendingSearchAsync();

        var result = Assert.Single(viewModel.DisplayedItems);
        Assert.True(result.IsFolder);
        Assert.Same(targetFolder, result.Node);

        viewModel.UpdateSelectedContentItems(new[] { result });
        Assert.True(viewModel.CanOpenSelectedContentItem);
        Assert.True(
            await viewModel.OpenSelectedContentItemAsync());

        Assert.Same(targetFolder, viewModel.SelectedFolder);
        Assert.False(viewModel.IsSearchActive);
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Same(
            nested,
            Assert.Single(viewModel.CurrentItems).Node);
    }

    private sealed class StubReader(
        Func<string, CancellationToken, Task<BookmarkDocument>> read)
        : IChromeBookmarksReader
    {
        public Task<BookmarkDocument> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            read(path, cancellationToken);
    }

    private static BookmarkDocument Document(
        BookmarkFolder bookmarkBar,
        BookmarkFolder other,
        BookmarkFolder synced) =>
        new(
            1,
            null,
            null,
            new BookmarkRoots(
                bookmarkBar,
                other,
                synced,
                EmptyProperties),
            EmptyProperties);

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
        string name,
        string url) =>
        new(
            id,
            GuidFor(int.Parse(id)),
            name,
            url,
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
