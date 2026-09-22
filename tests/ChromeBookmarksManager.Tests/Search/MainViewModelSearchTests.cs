using System.Text.Json;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Search;

public sealed class MainViewModelSearchTests
{
    [Fact]
    public void Constructor_SearchStartsDisabledAndDefaultsToAllBookmarks()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.CanSearchDocument);
        Assert.Equal(BookmarkSearchScope.AllBookmarks, viewModel.SearchScope);
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.False(viewModel.IsSearchActive);
        Assert.False(viewModel.IsSearchBusy);
        Assert.Empty(viewModel.SearchResults);
        Assert.Empty(viewModel.DisplayedBookmarks);
        Assert.Empty(viewModel.DisplayedItems);
        Assert.Equal(string.Empty, viewModel.SearchSummaryText);
    }

    [Fact]
    public async Task LoadBookmarksAsync_BuildsIndexBeforeSearchBecomesAvailable()
    {
        var fixture = CreateFixture();
        var buildEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBuild = new TaskCompletionSource<BookmarkSearchIndex>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new StubSearchService
        {
            BuildIndex = (_, _) =>
            {
                buildEntered.TrySetResult();
                return releaseBuild.Task;
            }
        };
        var viewModel = CreateViewModel(fixture, service);

        var load = viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        await buildEntered.Task;

        Assert.Equal(DocumentState.Loading, viewModel.State);
        Assert.False(viewModel.CanSearchDocument);
        Assert.Null(viewModel.Document);
        Assert.Equal("Building search index...", viewModel.StatusText);

        releaseBuild.SetResult(new BookmarkSearchIndex(fixture.Document));
        await load;

        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.True(viewModel.CanSearchDocument);
        Assert.Same(fixture.Document, viewModel.Document);
        Assert.Equal(1, service.BuildIndexCalls);
    }

    [Fact]
    public async Task CancelLoad_DuringIndexBuild_ReturnsToNoDocumentAndClearsSearch()
    {
        var fixture = CreateFixture();
        var buildEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new StubSearchService
        {
            BuildIndex = async (_, token) =>
            {
                buildEntered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("Unreachable.");
            }
        };
        var viewModel = CreateViewModel(fixture, service);

        var load = viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        await buildEntered.Task;
        viewModel.CancelLoad();
        await load;

        Assert.Equal(DocumentState.NoDocument, viewModel.State);
        Assert.Null(viewModel.Document);
        Assert.False(viewModel.CanSearchDocument);
        Assert.False(viewModel.IsSearchActive);
        Assert.False(viewModel.IsSearchBusy);
        Assert.Empty(viewModel.SearchResults);
        Assert.Equal(string.Empty, viewModel.SearchText);
    }

    [Fact]
    public async Task EmptySearch_DisplaysNormalCurrentFolderMixedItems()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        Assert.False(viewModel.IsSearchActive);
        Assert.Same(viewModel.CurrentBookmarks, viewModel.DisplayedBookmarks);
        Assert.Same(viewModel.CurrentItems, viewModel.DisplayedItems);
        Assert.Collection(
            viewModel.DisplayedItems,
            item => Assert.Same(fixture.BarFirst, item.Node),
            item => Assert.Same(fixture.ChildFolder, item.Node),
            item => Assert.Same(fixture.BarSecond, item.Node));
    }

    [Fact]
    public async Task ActiveSearch_DisplaysSearchResults()
    {
        var fixture = CreateFixture();
        var service = new StubSearchService
        {
            Search = (_, query, _, _, _) =>
                Task.FromResult<IReadOnlyList<BookmarkUrl>>(
                    query == "target"
                        ? new[] { fixture.OtherUrl }
                        : Array.Empty<BookmarkUrl>())
        };
        var viewModel = CreateViewModel(fixture, service);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SearchText = "target";
        await viewModel.WaitForPendingSearchAsync();

        Assert.True(viewModel.IsSearchActive);
        var result = Assert.Single(viewModel.SearchResults);
        Assert.Same(fixture.OtherUrl, result);
        Assert.Same(viewModel.SearchResults, viewModel.DisplayedBookmarks);
        var displayed = Assert.Single(viewModel.DisplayedItems);
        Assert.Same(fixture.OtherUrl, displayed.Node);
        Assert.True(displayed.IsBookmark);
        Assert.False(displayed.IsFolder);
        Assert.False(viewModel.IsSearchBusy);
    }

    [Fact]
    public async Task Debounce_RapidTextChangesCollapsesToLatestSearch()
    {
        var fixture = CreateFixture();
        var service = new StubSearchService();
        var viewModel = CreateViewModel(
            fixture,
            service,
            TimeSpan.FromMilliseconds(40));

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.SearchText = "a";
        viewModel.SearchText = "ab";
        viewModel.SearchText = "abc";
        await viewModel.WaitForPendingSearchAsync();

        Assert.Equal(1, service.SearchCalls);
        Assert.Equal(new[] { "abc" }, service.Queries);
    }

    [Fact]
    public async Task StaleSlowerSearch_CannotOverwriteNewerResult()
    {
        var fixture = CreateFixture();
        var oldStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var newStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOld = new TaskCompletionSource<IReadOnlyList<BookmarkUrl>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseNew = new TaskCompletionSource<IReadOnlyList<BookmarkUrl>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var service = new StubSearchService
        {
            Search = (_, query, _, _, _) =>
            {
                if (query == "old")
                {
                    oldStarted.TrySetResult();
                    return releaseOld.Task;
                }

                newStarted.TrySetResult();
                return releaseNew.Task;
            }
        };
        var viewModel = CreateViewModel(fixture, service);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.SearchText = "old";
        await oldStarted.Task;

        viewModel.SearchText = "new";
        await newStarted.Task;

        releaseNew.SetResult(new[] { fixture.OtherUrl });
        await viewModel.WaitForPendingSearchAsync();

        Assert.Same(fixture.OtherUrl, Assert.Single(viewModel.SearchResults));

        releaseOld.SetResult(new[] { fixture.BarFirst });
        await Task.Yield();

        Assert.Same(fixture.OtherUrl, Assert.Single(viewModel.SearchResults));
        Assert.Equal("new", viewModel.SearchText);
    }

    [Fact]
    public async Task ClearingQuery_CancelsSearchAndRestoresFolderView()
    {
        var fixture = CreateFixture();
        var service = new StubSearchService
        {
            Search = (_, _, _, _, _) =>
                Task.FromResult<IReadOnlyList<BookmarkUrl>>(
                    new[] { fixture.OtherUrl })
        };
        var viewModel = CreateViewModel(fixture, service);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SearchText = "target";
        await viewModel.WaitForPendingSearchAsync();
        Assert.True(viewModel.IsSearchActive);

        viewModel.SearchText = "   ";
        await viewModel.WaitForPendingSearchAsync();

        Assert.False(viewModel.IsSearchActive);
        Assert.Empty(viewModel.SearchResults);
        Assert.Same(viewModel.CurrentBookmarks, viewModel.DisplayedBookmarks);
        Assert.Equal(string.Empty, viewModel.SearchSummaryText);
    }

    [Fact]
    public async Task ScopeChange_WithActiveQuery_RerunsSearch()
    {
        var fixture = CreateFixture();
        var service = new StubSearchService();
        var viewModel = CreateViewModel(fixture, service);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SearchText = "match";
        await viewModel.WaitForPendingSearchAsync();

        service.ResetSearchRecording();
        viewModel.SearchScope = BookmarkSearchScope.CurrentFolder;
        await viewModel.WaitForPendingSearchAsync();

        Assert.Equal(1, service.SearchCalls);
        Assert.Equal(BookmarkSearchScope.CurrentFolder, Assert.Single(service.Scopes));
        Assert.Same(fixture.BookmarkBar, Assert.Single(service.Folders));
    }

    [Fact]
    public async Task FolderChange_CurrentFolderSearchRerunsButAllBookmarksDoesNot()
    {
        var fixture = CreateFixture();
        var service = new StubSearchService();
        var viewModel = CreateViewModel(fixture, service);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SearchScope = BookmarkSearchScope.CurrentFolder;
        viewModel.SearchText = "match";
        await viewModel.WaitForPendingSearchAsync();

        service.ResetSearchRecording();
        var child = Assert.Single(viewModel.FolderRoots[0].Children);
        viewModel.SelectFolder(child);
        await viewModel.WaitForPendingSearchAsync();

        Assert.Equal(1, service.SearchCalls);
        Assert.Same(fixture.ChildFolder, Assert.Single(service.Folders));

        viewModel.SearchScope = BookmarkSearchScope.AllBookmarks;
        await viewModel.WaitForPendingSearchAsync();
        service.ResetSearchRecording();

        viewModel.SelectFolder(viewModel.FolderRoots[1]);
        await viewModel.WaitForPendingSearchAsync();

        Assert.Equal(0, service.SearchCalls);
    }


    [Fact]
    public async Task Reopen_PreservesPreviousSearchUntilReplacementCommits()
    {
        var first = CreateFixture();
        var second = CreateFixture("Second bar");
        var secondReadEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource<BookmarkDocument>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var call = 0;
        var reader = new StubReader(async (_, token) =>
        {
            call++;
            if (call == 1)
            {
                return first.Document;
            }

            secondReadEntered.TrySetResult();
            return await releaseSecond.Task.WaitAsync(token);
        });
        var viewModel = new MainViewModel(
            reader,
            new BookmarkSearchService(),
            TimeSpan.Zero);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SearchText = "first";
        await viewModel.WaitForPendingSearchAsync();
        Assert.True(viewModel.IsSearchActive);

        var previousResult = Assert.Single(viewModel.SearchResults);

        var reopen = viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks-2");
        await secondReadEntered.Task;

        Assert.Equal(DocumentState.Loading, viewModel.State);
        Assert.Equal("first", viewModel.SearchText);
        Assert.False(viewModel.IsSearchActive);
        Assert.Same(previousResult, Assert.Single(viewModel.SearchResults));
        Assert.False(viewModel.CanSearchDocument);

        releaseSecond.SetResult(second.Document);
        await reopen;

        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.True(viewModel.CanSearchDocument);
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.False(viewModel.IsSearchActive);
        Assert.Empty(viewModel.SearchResults);
    }
    [Fact]
    public async Task SearchSummary_ExposesStateAndCountButNeverQueryText()
    {
        var fixture = CreateFixture();
        var release = new TaskCompletionSource<IReadOnlyList<BookmarkUrl>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new StubSearchService
        {
            Search = (_, _, _, _, _) =>
            {
                started.TrySetResult();
                return release.Task;
            }
        };
        var viewModel = CreateViewModel(fixture, service);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        const string privateQuery = "private-query-text";
        viewModel.SearchText = privateQuery;
        await started.Task;

        Assert.True(viewModel.IsSearchBusy);
        Assert.Equal("Searching...", viewModel.SearchSummaryText);
        Assert.DoesNotContain(privateQuery, viewModel.SearchSummaryText);

        release.SetResult(new[] { fixture.BarFirst, fixture.OtherUrl });
        await viewModel.WaitForPendingSearchAsync();

        Assert.False(viewModel.IsSearchBusy);
        Assert.Equal("2 matches", viewModel.SearchSummaryText);
        Assert.DoesNotContain(privateQuery, viewModel.SearchSummaryText);
    }

    [Fact]
    public async Task NoMatches_UsesStableSummaryWithoutQueryText()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SearchText = "not-present";
        await viewModel.WaitForPendingSearchAsync();

        Assert.Equal("No matches", viewModel.SearchSummaryText);
    }

    private static MainViewModel CreateViewModel(
        Fixture? fixture = null,
        StubSearchService? searchService = null,
        TimeSpan? debounce = null)
    {
        fixture ??= CreateFixture();
        searchService ??= new StubSearchService();

        return new MainViewModel(
            new StubReader((_, _) => Task.FromResult(fixture.Document)),
            searchService,
            debounce ?? TimeSpan.Zero);
    }

    private static MainViewModel CreateViewModel() =>
        CreateViewModel(CreateFixture());

    private static Fixture CreateFixture(string bookmarkBarName = "Bookmarks bar")
    {
        var barFirst = Url("10", "First target", "https://example.com/first");
        var nestedUrl = Url("11", "Nested target", "https://example.com/nested");
        var childFolder = Folder("20", "Child folder", nestedUrl);
        var barSecond = Url("12", "Second", "https://example.com/second");
        var bookmarkBar = Folder(
            "1",
            bookmarkBarName,
            barFirst,
            childFolder,
            barSecond);

        var otherUrl = Url("13", "Other target", "https://other.example/path");
        var other = Folder("2", "Other bookmarks", otherUrl);
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
            other,
            synced,
            childFolder,
            barFirst,
            barSecond,
            nestedUrl,
            otherUrl);
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

    private sealed class StubSearchService : IBookmarkSearchService
    {
        public Func<BookmarkDocument, CancellationToken, Task<BookmarkSearchIndex>>?
            BuildIndex { get; init; }

        public Func<
            BookmarkSearchIndex,
            string,
            BookmarkSearchScope,
            BookmarkFolder?,
            CancellationToken,
            Task<IReadOnlyList<BookmarkUrl>>>? Search { get; init; }

        public int BuildIndexCalls { get; private set; }

        public int SearchCalls { get; private set; }

        public List<string> Queries { get; } = new();

        public List<BookmarkSearchScope> Scopes { get; } = new();

        public List<BookmarkFolder?> Folders { get; } = new();

        public Task<BookmarkSearchIndex> BuildIndexAsync(
            BookmarkDocument document,
            CancellationToken cancellationToken)
        {
            BuildIndexCalls++;

            return BuildIndex?.Invoke(document, cancellationToken)
                ?? Task.FromResult(
                    new BookmarkSearchIndex(document, cancellationToken));
        }

        public Task<IReadOnlyList<BookmarkUrl>> SearchAsync(
            BookmarkSearchIndex index,
            string query,
            BookmarkSearchScope scope,
            BookmarkFolder? currentFolder,
            CancellationToken cancellationToken)
        {
            SearchCalls++;
            Queries.Add(query);
            Scopes.Add(scope);
            Folders.Add(currentFolder);

            return Search?.Invoke(
                    index,
                    query,
                    scope,
                    currentFolder,
                    cancellationToken)
                ?? Task.FromResult<IReadOnlyList<BookmarkUrl>>(
                    Array.Empty<BookmarkUrl>());
        }

        public void ResetSearchRecording()
        {
            SearchCalls = 0;
            Queries.Clear();
            Scopes.Clear();
            Folders.Clear();
        }
    }

    private sealed record Fixture(
        BookmarkDocument Document,
        BookmarkFolder BookmarkBar,
        BookmarkFolder Other,
        BookmarkFolder Synced,
        BookmarkFolder ChildFolder,
        BookmarkUrl BarFirst,
        BookmarkUrl BarSecond,
        BookmarkUrl NestedUrl,
        BookmarkUrl OtherUrl);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
