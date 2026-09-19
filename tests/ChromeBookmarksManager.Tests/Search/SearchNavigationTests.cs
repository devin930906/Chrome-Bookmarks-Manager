using System.Text.Json;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Search;

public sealed class SearchNavigationTests
{
    [Fact]
    public async Task FolderWrappers_ExposeCorrectPresentationParentChain()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var root = viewModel.FolderRoots[0];
        var levelOne = Assert.Single(root.Children);
        var levelTwo = Assert.Single(levelOne.Children);

        Assert.Null(root.Parent);
        Assert.Same(root, levelOne.Parent);
        Assert.Same(levelOne, levelTwo.Parent);
        Assert.Same(fixture.LevelOne, levelOne.Folder);
        Assert.Same(fixture.LevelTwo, levelTwo.Folder);
    }

    [Fact]
    public async Task NavigateToSearchResult_ExpandsOnlyAncestorPathAndSelectsOriginalBookmark()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SearchText = "needle";
        await viewModel.WaitForPendingSearchAsync();

        var result = Assert.Single(viewModel.SearchResults);
        Assert.Same(fixture.Target, result);

        var root = viewModel.FolderRoots[0];
        var levelOne = Assert.Single(root.Children);
        var levelTwo = Assert.Single(levelOne.Children);
        var unrelatedRoot = viewModel.FolderRoots[1];

        Assert.False(root.IsExpanded);
        Assert.False(levelOne.IsExpanded);
        Assert.False(levelTwo.IsExpanded);
        Assert.False(unrelatedRoot.IsExpanded);

        viewModel.NavigateToSearchResult(result);

        Assert.True(root.IsExpanded);
        Assert.True(levelOne.IsExpanded);
        Assert.False(levelTwo.IsExpanded);
        Assert.False(unrelatedRoot.IsExpanded);
        Assert.Same(fixture.LevelTwo, viewModel.SelectedFolder);
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.False(viewModel.IsSearchActive);
        Assert.Empty(viewModel.SearchResults);
        Assert.Same(fixture.Target, viewModel.SelectedBookmark);
        Assert.Same(viewModel.CurrentBookmarks, viewModel.DisplayedBookmarks);
        Assert.Same(fixture.Target, Assert.Single(viewModel.CurrentBookmarks));
    }

    [Fact]
    public async Task NavigateToSearchResult_UsesOriginalDomainReferencesWithoutMutatingHierarchy()
    {
        var fixture = CreateFixture();
        var rootChildren = fixture.BookmarkBar.Children.ToArray();
        var levelOneChildren = fixture.LevelOne.Children.ToArray();
        var levelTwoChildren = fixture.LevelTwo.Children.ToArray();
        var targetParent = fixture.Target.Parent;
        var viewModel = CreateViewModel(fixture);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SearchText = "needle";
        await viewModel.WaitForPendingSearchAsync();

        viewModel.NavigateToSearchResult(Assert.Single(viewModel.SearchResults));

        Assert.Equal(rootChildren, fixture.BookmarkBar.Children);
        Assert.Equal(levelOneChildren, fixture.LevelOne.Children);
        Assert.Equal(levelTwoChildren, fixture.LevelTwo.Children);
        Assert.Same(targetParent, fixture.Target.Parent);
        Assert.Same(fixture.LevelTwo, fixture.Target.Parent);
    }

    [Fact]
    public async Task NavigateToSearchResult_InvalidOrStaleBookmark_IsSafeNoOp()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SearchText = "needle";
        await viewModel.WaitForPendingSearchAsync();

        var selectedFolderBefore = viewModel.SelectedFolder;
        var stale = Url("99", "Stale", "https://stale.example/");

        viewModel.NavigateToSearchResult(stale);

        Assert.True(viewModel.IsSearchActive);
        Assert.Equal("needle", viewModel.SearchText);
        Assert.Same(selectedFolderBefore, viewModel.SelectedFolder);
        Assert.Null(viewModel.SelectedBookmark);
        Assert.Same(fixture.Target, Assert.Single(viewModel.SearchResults));
    }

    [Fact]
    public async Task NavigateToSearchResult_WhenSearchInactive_IsSafeNoOp()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.NavigateToSearchResult(fixture.Target);

        Assert.Same(fixture.BookmarkBar, viewModel.SelectedFolder);
        Assert.Null(viewModel.SelectedBookmark);
        Assert.False(viewModel.FolderRoots[0].IsExpanded);
    }

    private static MainViewModel CreateViewModel(Fixture fixture) =>
        new(
            new StubReader((_, _) => Task.FromResult(fixture.Document)),
            new BookmarkSearchService(),
            TimeSpan.Zero);

    private static Fixture CreateFixture()
    {
        var target = Url("10", "Needle bookmark", "https://example.com/needle");
        var levelTwo = Folder("30", "Level two", target);
        var levelOne = Folder("20", "Level one", levelTwo);
        var bookmarkBar = Folder("1", "Bookmarks bar", levelOne);

        var unrelatedUrl = Url("11", "Other", "https://other.example/");
        var other = Folder("2", "Other bookmarks", unrelatedUrl);
        var synced = Folder("3", "Mobile bookmarks");

        var document = new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(bookmarkBar, other, synced, EmptyProperties),
            EmptyProperties);

        return new Fixture(
            document,
            bookmarkBar,
            levelOne,
            levelTwo,
            target,
            other,
            synced);
    }

    private static BookmarkFolder Folder(
        string id,
        string name,
        params BookmarkNode[] children) =>
        new(
            id,
            CreateGuid(int.Parse(id)),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties,
            children);

    private static BookmarkUrl Url(string id, string name, string url) =>
        new(
            id,
            CreateGuid(int.Parse(id)),
            name,
            url,
            null,
            null,
            null,
            null,
            EmptyProperties);

    private static Guid CreateGuid(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        bytes[7] = 0x40;
        bytes[8] = 0x80;
        return new Guid(bytes);
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

    private sealed record Fixture(
        BookmarkDocument Document,
        BookmarkFolder BookmarkBar,
        BookmarkFolder LevelOne,
        BookmarkFolder LevelTwo,
        BookmarkUrl Target,
        BookmarkFolder Other,
        BookmarkFolder Synced);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
