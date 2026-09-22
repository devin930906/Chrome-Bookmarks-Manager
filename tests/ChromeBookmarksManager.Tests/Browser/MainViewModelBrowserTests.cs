using System.Text.Json;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Browser;

public sealed class MainViewModelBrowserTests
{
    [Fact]
    public void Constructor_StartsWithEmptyBrowserState()
    {
        var viewModel = new MainViewModel(
            new StubReader((_, _) => Task.FromResult(CreateFixture().Document)));

        Assert.Empty(viewModel.FolderRoots);
        Assert.Null(viewModel.SelectedFolder);
        Assert.Empty(viewModel.CurrentItems);
        Assert.Empty(viewModel.DisplayedItems);
        Assert.Empty(viewModel.CurrentBookmarks);
        Assert.Null(viewModel.SelectedBookmark);
        Assert.False(viewModel.CanBrowseDocument);
        Assert.Equal(string.Empty, viewModel.DocumentSummaryText);
        Assert.Equal(string.Empty, viewModel.SelectionSummaryText);
    }

    [Fact]
    public async Task LoadBookmarksAsync_Success_BuildsThreeRootsAndSelectsBookmarkBar()
    {
        var fixture = CreateFixture();
        var viewModel = new MainViewModel(
            new StubReader((_, _) => Task.FromResult(fixture.Document)));

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.True(viewModel.CanBrowseDocument);
        Assert.Equal(3, viewModel.FolderRoots.Count);
        Assert.Same(fixture.BookmarkBar, viewModel.FolderRoots[0].Folder);
        Assert.Same(fixture.Other, viewModel.FolderRoots[1].Folder);
        Assert.Same(fixture.Synced, viewModel.FolderRoots[2].Folder);
        Assert.True(viewModel.FolderRoots[0].IsSelected);
        Assert.False(viewModel.FolderRoots[0].IsExpanded);
        Assert.False(viewModel.FolderRoots[1].IsSelected);
        Assert.False(viewModel.FolderRoots[2].IsSelected);
        Assert.Same(fixture.BookmarkBar, viewModel.SelectedFolder);
        Assert.Equal("4 URLs | 4 folders", viewModel.DocumentSummaryText);
        Assert.Equal("Bookmarks bar | 1 folder | 2 bookmarks", viewModel.SelectionSummaryText);
    }

    [Fact]
    public async Task LoadBookmarksAsync_Success_ExposesDirectChildrenInOriginalMixedOrder()
    {
        var fixture = CreateFixture();
        var viewModel = new MainViewModel(
            new StubReader((_, _) => Task.FromResult(fixture.Document)));

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        Assert.Collection(
            viewModel.CurrentItems,
            item =>
            {
                Assert.Same(fixture.BarUrlFirst, item.Node);
                Assert.True(item.IsBookmark);
            },
            item =>
            {
                Assert.Same(fixture.ChildFolder, item.Node);
                Assert.True(item.IsFolder);
            },
            item =>
            {
                Assert.Same(fixture.BarUrlSecond, item.Node);
                Assert.True(item.IsBookmark);
            });

        Assert.Same(viewModel.CurrentItems, viewModel.DisplayedItems);
        Assert.Equal(2, viewModel.CurrentBookmarks.Count);
        Assert.Same(fixture.BarUrlFirst, viewModel.CurrentBookmarks[0]);
        Assert.Same(fixture.BarUrlSecond, viewModel.CurrentBookmarks[1]);
        Assert.DoesNotContain(
            viewModel.CurrentBookmarks,
            bookmark => ReferenceEquals(bookmark, fixture.NestedUrl));
    }

    [Fact]
    public async Task SelectingChildFolderRow_DoesNotReplaceCurrentNavigationFolder()
    {
        var fixture = CreateFixture();
        var viewModel = new MainViewModel(
            new StubReader((_, _) => Task.FromResult(fixture.Document)));

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var childRow = Assert.Single(
            viewModel.CurrentItems,
            item => item.IsFolder);

        viewModel.UpdateSelectedContentItems(new[] { childRow });

        Assert.Same(fixture.BookmarkBar, viewModel.SelectedFolder);
        Assert.Same(childRow, viewModel.SelectedContentItem);
        Assert.Same(
            fixture.ChildFolder,
            viewModel.SelectedContentFolder);
        Assert.Same(
            childRow,
            Assert.Single(viewModel.SelectedContentItems));
        Assert.Empty(viewModel.SelectedBookmarks);
        Assert.Null(viewModel.SelectedBookmark);

        Assert.False(viewModel.CanRenameSelectedFolder);
        Assert.True(viewModel.CanRenameSelectedContentFolder);
        Assert.True(viewModel.CanMoveSelectedContentFolder);
        Assert.True(viewModel.CanDeleteSelectedContentFolder);
    }

    [Fact]
    public async Task MixedContentSelection_NormalizesToSingleFolder()
    {
        var fixture = CreateFixture();
        var viewModel = new MainViewModel(
            new StubReader((_, _) => Task.FromResult(fixture.Document)));

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var firstBookmark = viewModel.CurrentItems[0];
        var childFolder = viewModel.CurrentItems[1];
        var secondBookmark = viewModel.CurrentItems[2];

        viewModel.UpdateSelectedContentItems(
            new[] { firstBookmark, childFolder, secondBookmark });

        Assert.Same(
            childFolder,
            Assert.Single(viewModel.SelectedContentItems));
        Assert.Same(childFolder, viewModel.SelectedContentItem);
        Assert.Same(
            fixture.ChildFolder,
            viewModel.SelectedContentFolder);
        Assert.Empty(viewModel.SelectedBookmarks);
        Assert.Null(viewModel.SelectedBookmark);
    }

    [Fact]
    public async Task BookmarkContentSelection_PreservesDisplayedOrderAndBatchSelection()
    {
        var fixture = CreateFixture();
        var viewModel = new MainViewModel(
            new StubReader((_, _) => Task.FromResult(fixture.Document)));

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var firstBookmark = viewModel.CurrentItems[0];
        var secondBookmark = viewModel.CurrentItems[2];

        viewModel.UpdateSelectedContentItems(
            new[] { secondBookmark, firstBookmark });

        Assert.Collection(
            viewModel.SelectedContentItems,
            item => Assert.Same(firstBookmark, item),
            item => Assert.Same(secondBookmark, item));
        Assert.Collection(
            viewModel.SelectedBookmarks,
            bookmark => Assert.Same(fixture.BarUrlFirst, bookmark),
            bookmark => Assert.Same(fixture.BarUrlSecond, bookmark));
        Assert.Same(firstBookmark, viewModel.SelectedContentItem);
        Assert.Same(fixture.BarUrlFirst, viewModel.SelectedBookmark);
        Assert.Null(viewModel.SelectedContentFolder);
    }

    [Fact]
    public async Task ChangingNavigationFolder_ClearsRightPaneContentSelection()
    {
        var fixture = CreateFixture();
        var viewModel = new MainViewModel(
            new StubReader((_, _) => Task.FromResult(fixture.Document)));

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        var childRow = Assert.Single(
            viewModel.CurrentItems,
            item => item.IsFolder);
        viewModel.UpdateSelectedContentItems(new[] { childRow });

        var childTreeItem = Assert.Single(
            viewModel.FolderRoots[0].Children);
        viewModel.SelectFolder(childTreeItem);

        Assert.Same(fixture.ChildFolder, viewModel.SelectedFolder);
        Assert.Empty(viewModel.SelectedContentItems);
        Assert.Null(viewModel.SelectedContentItem);
        Assert.Null(viewModel.SelectedContentFolder);
        Assert.Empty(viewModel.SelectedBookmarks);
        Assert.Null(viewModel.SelectedBookmark);
    }

    [Fact]
    public async Task SelectFolder_UsesOriginalFolderAndClearsBookmarkSelection()
    {
        var fixture = CreateFixture();
        var viewModel = new MainViewModel(
            new StubReader((_, _) => Task.FromResult(fixture.Document)));

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SelectedBookmark = fixture.BarUrlFirst;
        var childItem = Assert.Single(viewModel.FolderRoots[0].Children);

        viewModel.SelectFolder(childItem);

        Assert.False(viewModel.FolderRoots[0].IsSelected);
        Assert.True(childItem.IsSelected);
        Assert.Same(fixture.ChildFolder, viewModel.SelectedFolder);
        var current = Assert.Single(viewModel.CurrentBookmarks);
        Assert.Same(fixture.NestedUrl, current);
        Assert.Null(viewModel.SelectedBookmark);
        Assert.Equal("Child folder | 0 folders | 1 bookmark", viewModel.SelectionSummaryText);
    }


    [Fact]
    public async Task NavigateToFolder_SelectsMatchingTreeItemAndShowsItsDirectContents()
    {
        var fixture = CreateFixture();
        var viewModel = new MainViewModel(
            new StubReader((_, _) => Task.FromResult(fixture.Document)));

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var changed = viewModel.NavigateToFolder(fixture.ChildFolder);

        Assert.True(changed);
        Assert.Same(fixture.ChildFolder, viewModel.SelectedFolder);

        var childTreeItem = Assert.Single(
            viewModel.FolderRoots[0].Children);
        Assert.True(viewModel.FolderRoots[0].IsExpanded);
        Assert.True(childTreeItem.IsSelected);

        var current = Assert.Single(viewModel.CurrentItems);
        Assert.Same(fixture.NestedUrl, current.Node);
        Assert.True(current.IsBookmark);
        Assert.Equal(
            "Child folder | 0 folders | 1 bookmark",
            viewModel.SelectionSummaryText);
    }

    [Fact]
    public async Task LoadBookmarksAsync_ReadFailure_PreservesPreviousBrowserState()
    {
        var fixture = CreateFixture();
        var call = 0;
        var reader = new StubReader((_, _) =>
        {
            call++;
            if (call == 1)
            {
                return Task.FromResult(fixture.Document);
            }

            return Task.FromException<BookmarkDocument>(
                new ChromeBookmarksReadException(
                    ChromeBookmarksReadError.MalformedJson,
                    "Invalid file."));
        });
        var viewModel = new MainViewModel(reader);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var previousDocument = viewModel.Document;
        var previousFolderRoots = viewModel.FolderRoots;
        var previousSelectedFolder = viewModel.SelectedFolder;
        var previousCurrentItems = viewModel.CurrentItems;
        var previousCurrentBookmarks = viewModel.CurrentBookmarks;
        var previousSelectedBookmark = viewModel.SelectedBookmark;
        var previousDocumentSummary = viewModel.DocumentSummaryText;
        var previousSelectionSummary = viewModel.SelectionSummaryText;

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Invalid");

        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.Same(previousDocument, viewModel.Document);
        Assert.Same(previousFolderRoots, viewModel.FolderRoots);
        Assert.Same(previousSelectedFolder, viewModel.SelectedFolder);
        Assert.Same(previousCurrentItems, viewModel.CurrentItems);
        Assert.Same(previousCurrentBookmarks, viewModel.CurrentBookmarks);
        Assert.Same(previousSelectedBookmark, viewModel.SelectedBookmark);
        Assert.True(viewModel.CanBrowseDocument);
        Assert.Equal(previousDocumentSummary, viewModel.DocumentSummaryText);
        Assert.Equal(previousSelectionSummary, viewModel.SelectionSummaryText);
    }

    [Fact]
    public async Task CancelReplacementLoad_PreservesPreviousBrowserState()
    {
        var fixture = CreateFixture();
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var call = 0;
        var reader = new StubReader(async (_, token) =>
        {
            call++;
            if (call == 1)
            {
                return fixture.Document;
            }

            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return fixture.Document;
        });
        var viewModel = new MainViewModel(reader);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var previousDocument = viewModel.Document;
        var previousFolderRoots = viewModel.FolderRoots;
        var previousSelectedFolder = viewModel.SelectedFolder;
        var previousCurrentItems = viewModel.CurrentItems;
        var previousCurrentBookmarks = viewModel.CurrentBookmarks;
        var previousSelectedBookmark = viewModel.SelectedBookmark;
        var previousDocumentSummary = viewModel.DocumentSummaryText;
        var previousSelectionSummary = viewModel.SelectionSummaryText;

        var load = viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks-2");
        await entered.Task;

        viewModel.CancelLoad();
        await load;

        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.Same(previousDocument, viewModel.Document);
        Assert.Same(previousFolderRoots, viewModel.FolderRoots);
        Assert.Same(previousSelectedFolder, viewModel.SelectedFolder);
        Assert.Same(previousCurrentItems, viewModel.CurrentItems);
        Assert.Same(previousCurrentBookmarks, viewModel.CurrentBookmarks);
        Assert.Same(previousSelectedBookmark, viewModel.SelectedBookmark);
        Assert.True(viewModel.CanBrowseDocument);
        Assert.Equal(previousDocumentSummary, viewModel.DocumentSummaryText);
        Assert.Equal(previousSelectionSummary, viewModel.SelectionSummaryText);
    }

    [Fact]
    public async Task LoadBookmarksAsync_SecondLoad_PreservesBrowserStateUntilReplacementCommits()
    {
        var first = CreateFixture();
        var second = CreateFixture("Second bar");
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<BookmarkDocument>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var call = 0;
        var reader = new StubReader(async (_, token) =>
        {
            call++;
            if (call == 1)
            {
                return first.Document;
            }

            entered.TrySetResult();
            return await release.Task.WaitAsync(token);
        });
        var viewModel = new MainViewModel(reader);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var previousDocument = viewModel.Document;
        var previousFolderRoots = viewModel.FolderRoots;
        var previousSelectedFolder = viewModel.SelectedFolder;
        var previousCurrentItems = viewModel.CurrentItems;
        var previousCurrentBookmarks = viewModel.CurrentBookmarks;
        var previousSelectedBookmark = viewModel.SelectedBookmark;
        var previousDocumentSummary = viewModel.DocumentSummaryText;
        var previousSelectionSummary = viewModel.SelectionSummaryText;

        var secondLoad = viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks-2");
        await entered.Task;

        Assert.Equal(DocumentState.Loading, viewModel.State);
        Assert.Same(previousDocument, viewModel.Document);
        Assert.Same(previousFolderRoots, viewModel.FolderRoots);
        Assert.Same(previousSelectedFolder, viewModel.SelectedFolder);
        Assert.Same(previousCurrentItems, viewModel.CurrentItems);
        Assert.Same(previousCurrentBookmarks, viewModel.CurrentBookmarks);
        Assert.Same(previousSelectedBookmark, viewModel.SelectedBookmark);
        Assert.False(viewModel.CanBrowseDocument);
        Assert.Equal(previousDocumentSummary, viewModel.DocumentSummaryText);
        Assert.Equal(previousSelectionSummary, viewModel.SelectionSummaryText);

        release.SetResult(second.Document);
        await secondLoad;

        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.Same(second.Document, viewModel.Document);
        Assert.Same(second.BookmarkBar, viewModel.SelectedFolder);
        Assert.Equal("Second bar", viewModel.FolderRoots[0].Name);
        Assert.True(viewModel.CanBrowseDocument);
    }
    [Fact]
    public async Task LoadBookmarksAsync_Reopen_ReplacesSummariesAndSourcePath()
    {
        var first = CreateFixture();
        var second = CreateFixture("Second bar");
        var call = 0;
        var reader = new StubReader((_, _) =>
        {
            call++;
            return Task.FromResult(call == 1 ? first.Document : second.Document);
        });
        var viewModel = new MainViewModel(reader);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SelectFolder(Assert.Single(viewModel.FolderRoots[0].Children));

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks-2");

        Assert.Equal(@"C:\Synthetic\Bookmarks-2", viewModel.SourcePath);
        Assert.Equal("4 URLs | 4 folders", viewModel.DocumentSummaryText);
        Assert.Equal("Second bar | 1 folder | 2 bookmarks", viewModel.SelectionSummaryText);
        Assert.Same(second.BookmarkBar, viewModel.SelectedFolder);
        Assert.True(viewModel.FolderRoots[0].IsSelected);
        Assert.False(viewModel.FolderRoots[0].IsExpanded);
    }

    private static Fixture CreateFixture(string bookmarkBarName = "Bookmarks bar")
    {
        var barUrlFirst = Url(
            "4",
            "44444444-4444-4444-8444-444444444444",
            "First",
            "https://example.com/first");
        var nestedUrl = Url(
            "6",
            "66666666-6666-4666-8666-666666666666",
            "Nested",
            "https://example.com/nested");
        var childFolder = Folder(
            "5",
            "55555555-5555-4555-8555-555555555555",
            "Child folder",
            nestedUrl);
        var barUrlSecond = Url(
            "7",
            "77777777-7777-4777-8777-777777777777",
            "Second",
            "https://example.com/second");
        var bookmarkBar = Folder(
            "1",
            "11111111-1111-4111-8111-111111111111",
            bookmarkBarName,
            barUrlFirst,
            childFolder,
            barUrlSecond);

        var otherUrl = Url(
            "8",
            "88888888-8888-4888-8888-888888888888",
            "Other",
            "https://example.com/other");
        var other = Folder(
            "2",
            "22222222-2222-4222-8222-222222222222",
            "Other bookmarks",
            otherUrl);
        var synced = Folder(
            "3",
            "33333333-3333-4333-8333-333333333333",
            "Mobile bookmarks");

        var document = new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                bookmarkBar,
                other,
                synced,
                EmptyProperties()),
            EmptyProperties());

        return new Fixture(
            document,
            bookmarkBar,
            other,
            synced,
            childFolder,
            barUrlFirst,
            barUrlSecond,
            nestedUrl);
    }

    private static BookmarkFolder Folder(
        string id,
        string guid,
        string name,
        params BookmarkNode[] children) =>
        new(
            id,
            Guid.Parse(guid),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties(),
            children);

    private static BookmarkUrl Url(
        string id,
        string guid,
        string name,
        string url) =>
        new(
            id,
            Guid.Parse(guid),
            name,
            url,
            null,
            null,
            null,
            null,
            EmptyProperties());

    private static IReadOnlyDictionary<string, JsonElement> EmptyProperties() =>
        new Dictionary<string, JsonElement>();

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
        BookmarkFolder Other,
        BookmarkFolder Synced,
        BookmarkFolder ChildFolder,
        BookmarkUrl BarUrlFirst,
        BookmarkUrl BarUrlSecond,
        BookmarkUrl NestedUrl);
}
