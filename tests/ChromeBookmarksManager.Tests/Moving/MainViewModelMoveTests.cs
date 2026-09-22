using System.Text.Json;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Moving;

public sealed class MainViewModelMoveTests
{
    [Fact]
    public void Constructor_MoveStartsDisabledWithoutDocument()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);

        Assert.False(viewModel.CanMoveSelectedBookmark);
        Assert.False(viewModel.CanMoveSelectedFolder);
    }

    [Fact]
    public async Task Selection_EnablesOnlyMovableNodes()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        Assert.False(viewModel.CanMoveSelectedFolder);

        viewModel.SelectedBookmark = fixture.BarFirst;
        Assert.True(viewModel.CanMoveSelectedBookmark);

        var childItem = Assert.Single(viewModel.FolderRoots[0].Children);
        viewModel.SelectFolder(childItem);
        Assert.True(viewModel.CanMoveSelectedFolder);
    }

    [Fact]
    public async Task MoveBookmarkToEnd_CrossFolderMarksDirtySelectsDestinationAndAvoidsIndexRebuild()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SelectedBookmark = fixture.BarFirst;

        Assert.Equal(1, search.BuildIndexCalls);

        var changed = await viewModel.MoveBookmarkToEndAsync(
            fixture.BarFirst,
            fixture.Other);

        Assert.True(changed);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.Same(fixture.Other, fixture.BarFirst.Parent);
        Assert.Same(fixture.Other, viewModel.SelectedFolder);
        Assert.Same(fixture.BarFirst, viewModel.SelectedBookmark);
        Assert.Equal(
            new[] { fixture.OtherUrl, fixture.BarFirst },
            viewModel.CurrentBookmarks);
        Assert.Equal(1, search.BuildIndexCalls);
    }

    [Fact]
    public async Task MoveBookmarkToEnd_SameEffectivePositionIsNoOpAndDoesNotDirty()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var changed = await viewModel.MoveBookmarkToEndAsync(
            fixture.OtherUrl,
            fixture.Other);

        Assert.False(changed);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(1, search.BuildIndexCalls);
    }

    [Fact]
    public async Task MoveFolderToEnd_CrossParentRefreshesTreeAndKeepsMovedFolderSelected()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var changed = await viewModel.MoveFolderToEndAsync(
            fixture.ChildFolder,
            fixture.Other);

        Assert.True(changed);
        Assert.Same(fixture.Other, fixture.ChildFolder.Parent);
        Assert.Empty(viewModel.FolderRoots[0].Children);
        var movedItem = Assert.Single(viewModel.FolderRoots[1].Children);
        Assert.Same(fixture.ChildFolder, movedItem.Folder);
        Assert.Same(fixture.ChildFolder, viewModel.SelectedFolder);
    }

    [Fact]
    public async Task MoveBookmarkBefore_ReordersCurrentFolderAndPreservesSelection()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.SelectedBookmark = fixture.BarSecond;

        var changed = await viewModel.MoveBookmarkBeforeAsync(
            fixture.BarSecond,
            fixture.BarFirst);

        Assert.True(changed);
        Assert.Equal(
            new[] { fixture.BarSecond, fixture.BarFirst },
            viewModel.CurrentBookmarks);
        Assert.Same(fixture.BarSecond, viewModel.SelectedBookmark);
    }

    [Fact]
    public async Task MoveNodeBeforeAsync_FolderBeforeBookmarkUsesMixedOrderAndHistory()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var changed = await viewModel.MoveNodeBeforeAsync(
            fixture.ChildFolder,
            fixture.BarFirst);

        Assert.True(changed);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.ChildFolder,
                fixture.BarFirst,
                fixture.BarSecond
            },
            fixture.BookmarkBar.Children);
        Assert.Equal("Move folder", viewModel.UndoDescription);

        Assert.True(await viewModel.UndoAsync());
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.ChildFolder,
                fixture.BarSecond
            },
            fixture.BookmarkBar.Children);

        Assert.True(await viewModel.RedoAsync());
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.ChildFolder,
                fixture.BarFirst,
                fixture.BarSecond
            },
            fixture.BookmarkBar.Children);
    }

    [Fact]
    public async Task GlobalSearch_MoveBookmarkKeepsSameSearchReferenceWithoutIndexRebuild()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.SearchText = "First";
        await viewModel.WaitForPendingSearchAsync();
        var result = Assert.IsType<BookmarkUrl>(
            Assert.Single(viewModel.SearchResults));
        Assert.Same(fixture.BarFirst, result);
        viewModel.SelectedBookmark = result;

        var changed = await viewModel.MoveBookmarkToEndAsync(
            fixture.BarFirst,
            fixture.Other);
        await viewModel.WaitForPendingSearchAsync();

        Assert.True(changed);
        Assert.Equal(1, search.BuildIndexCalls);
        Assert.Same(fixture.BarFirst, Assert.Single(viewModel.SearchResults));
        Assert.Same(fixture.BarFirst, viewModel.SelectedBookmark);
    }

    [Fact]
    public async Task CurrentFolderSearch_MoveBookmarkOutRefreshesResultsWithoutIndexRebuild()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.SearchScope = BookmarkSearchScope.CurrentFolder;
        viewModel.SearchText = "First";
        await viewModel.WaitForPendingSearchAsync();
        Assert.Same(fixture.BarFirst, Assert.Single(viewModel.SearchResults));

        var changed = await viewModel.MoveBookmarkToEndAsync(
            fixture.BarFirst,
            fixture.Other);
        await viewModel.WaitForPendingSearchAsync();

        Assert.True(changed);
        Assert.Equal(1, search.BuildIndexCalls);
        Assert.Empty(viewModel.SearchResults);
        Assert.Same(fixture.BookmarkBar, viewModel.SelectedFolder);
    }

    [Fact]
    public async Task PositionalBookmarkReorder_IsRejectedWhileSearchIsActive()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.SearchText = "example.com";
        await viewModel.WaitForPendingSearchAsync();
        Assert.True(viewModel.IsSearchActive);

        var exception = await Assert.ThrowsAsync<BookmarkMoveException>(
            () => viewModel.MoveBookmarkBeforeAsync(
                fixture.BarSecond,
                fixture.BarFirst));

        Assert.Equal(BookmarkMoveError.InvalidDropTarget, exception.Error);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.Equal(
            new BookmarkUrl[] { fixture.BarFirst, fixture.BarSecond },
            fixture.BookmarkBar.Children.OfType<BookmarkUrl>().ToArray());
    }


    [Fact]
    public async Task MoveBookmarkToEnd_UndoRedo_RestoresPlacementDirtyStateAndAvoidsIndexRebuild()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var changed = await viewModel.MoveBookmarkToEndAsync(
            fixture.BarFirst,
            fixture.Other);

        Assert.True(changed);
        Assert.True(viewModel.CanUndo);
        Assert.Equal("Move bookmark", viewModel.UndoDescription);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.Equal(1, search.BuildIndexCalls);

        var undone = await viewModel.UndoAsync();

        Assert.True(undone);
        Assert.Same(fixture.BookmarkBar, fixture.BarFirst.Parent);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.ChildFolder,
                fixture.BarSecond
            },
            fixture.BookmarkBar.Children);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.CanUndo);
        Assert.True(viewModel.CanRedo);
        Assert.Equal("Move bookmark", viewModel.RedoDescription);
        Assert.Equal(1, search.BuildIndexCalls);

        var redone = await viewModel.RedoAsync();

        Assert.True(redone);
        Assert.Same(fixture.Other, fixture.BarFirst.Parent);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.OtherUrl,
                fixture.BarFirst
            },
            fixture.Other.Children);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.True(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.Equal(1, search.BuildIndexCalls);
    }

    [Fact]
    public async Task MoveBookmarkBefore_UndoRedo_RestoresMixedChildOrder()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var changed = await viewModel.MoveBookmarkBeforeAsync(
            fixture.BarSecond,
            fixture.BarFirst);

        Assert.True(changed);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.BarSecond,
                fixture.ChildFolder,
                fixture.BarFirst
            },
            fixture.BookmarkBar.Children);

        Assert.True(await viewModel.UndoAsync());
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.ChildFolder,
                fixture.BarSecond
            },
            fixture.BookmarkBar.Children);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);

        Assert.True(await viewModel.RedoAsync());
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.BarSecond,
                fixture.ChildFolder,
                fixture.BarFirst
            },
            fixture.BookmarkBar.Children);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
    }

    [Fact]
    public async Task MoveFolderToEnd_UndoRedo_RestoresOriginalParentAndSelection()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        Assert.True(await viewModel.MoveFolderToEndAsync(
            fixture.ChildFolder,
            fixture.Other));
        Assert.Same(fixture.Other, fixture.ChildFolder.Parent);

        Assert.True(await viewModel.UndoAsync());
        Assert.Same(fixture.BookmarkBar, fixture.ChildFolder.Parent);
        Assert.Same(fixture.ChildFolder, viewModel.SelectedFolder);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);

        Assert.True(await viewModel.RedoAsync());
        Assert.Same(fixture.Other, fixture.ChildFolder.Parent);
        Assert.Same(fixture.ChildFolder, viewModel.SelectedFolder);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
    }

    [Fact]
    public async Task BatchMove_UndoRedo_RestoresEveryOriginalMixedChildSlotWithoutIndexRebuild()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.UpdateSelectedBookmarks(
            new[] { fixture.BarFirst, fixture.BarSecond });

        var changed = await viewModel.MoveSelectedBookmarksToEndAsync(
            fixture.Other);

        Assert.True(changed);
        Assert.Equal("Move 2 bookmarks", viewModel.UndoDescription);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.OtherUrl,
                fixture.BarFirst,
                fixture.BarSecond
            },
            fixture.Other.Children);
        Assert.Equal(1, search.BuildIndexCalls);

        Assert.True(await viewModel.UndoAsync());
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.ChildFolder,
                fixture.BarSecond
            },
            fixture.BookmarkBar.Children);
        Assert.Equal(
            new BookmarkNode[] { fixture.OtherUrl },
            fixture.Other.Children);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.Equal(1, search.BuildIndexCalls);

        Assert.True(await viewModel.RedoAsync());
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.OtherUrl,
                fixture.BarFirst,
                fixture.BarSecond
            },
            fixture.Other.Children);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.Equal(1, search.BuildIndexCalls);
    }

    [Fact]
    public async Task MoveSelectedContentItemsToEnd_MixedSelection_UndoRedoRestoresExactOrderAndIdentity()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.UpdateSelectedContentItems(
            new[]
            {
                viewModel.CurrentItems[2],
                viewModel.CurrentItems[0],
                viewModel.CurrentItems[1]
            });

        Assert.True(viewModel.CanMoveSelectedContentItems);

        var changed =
            await viewModel.MoveSelectedContentItemsToEndAsync(
                fixture.Other);

        Assert.True(changed);
        Assert.Equal("Move 3 items", viewModel.UndoDescription);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.OtherUrl,
                fixture.BarFirst,
                fixture.ChildFolder,
                fixture.BarSecond
            },
            fixture.Other.Children);
        Assert.Same(fixture.Other, fixture.BarFirst.Parent);
        Assert.Same(fixture.Other, fixture.ChildFolder.Parent);
        Assert.Same(fixture.Other, fixture.BarSecond.Parent);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.Equal(1, search.BuildIndexCalls);

        Assert.True(await viewModel.UndoAsync());

        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.ChildFolder,
                fixture.BarSecond
            },
            fixture.BookmarkBar.Children);
        Assert.Equal(
            new BookmarkNode[] { fixture.OtherUrl },
            fixture.Other.Children);
        Assert.Same(fixture.BookmarkBar, fixture.BarFirst.Parent);
        Assert.Same(fixture.BookmarkBar, fixture.ChildFolder.Parent);
        Assert.Same(fixture.BookmarkBar, fixture.BarSecond.Parent);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.Equal(1, search.BuildIndexCalls);

        Assert.True(await viewModel.RedoAsync());

        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.OtherUrl,
                fixture.BarFirst,
                fixture.ChildFolder,
                fixture.BarSecond
            },
            fixture.Other.Children);
        Assert.Same(fixture.Other, fixture.BarFirst.Parent);
        Assert.Same(fixture.Other, fixture.ChildFolder.Parent);
        Assert.Same(fixture.Other, fixture.BarSecond.Parent);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.Equal(1, search.BuildIndexCalls);
    }

    [Fact]
    public async Task MoveContentNodesAsync_MixedPayloadToIndex_UndoRedoRestoresExactOrder()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var changed = await viewModel.MoveContentNodesAsync(
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.ChildFolder
            },
            fixture.Other,
            0);

        Assert.True(changed);
        Assert.Equal("Move 2 items", viewModel.UndoDescription);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.ChildFolder,
                fixture.OtherUrl
            },
            fixture.Other.Children);
        Assert.Equal(
            new BookmarkNode[] { fixture.BarSecond },
            fixture.BookmarkBar.Children);

        Assert.True(await viewModel.UndoAsync());
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.ChildFolder,
                fixture.BarSecond
            },
            fixture.BookmarkBar.Children);
        Assert.Equal(
            new BookmarkNode[] { fixture.OtherUrl },
            fixture.Other.Children);

        Assert.True(await viewModel.RedoAsync());
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.ChildFolder,
                fixture.OtherUrl
            },
            fixture.Other.Children);
    }

    private static MainViewModel CreateViewModel(
        BookmarkDocument document,
        IBookmarkSearchService? searchService = null) =>
        new(
            new DelegateReader((_, _) => Task.FromResult(document)),
            searchService ?? new BookmarkSearchService(),
            TimeSpan.Zero);

    private static Fixture CreateFixture()
    {
        var barFirst = Url("10", "First", "https://example.com/first");
        var childNested = Url("12", "Nested", "https://example.com/nested");
        var childFolder = Folder("20", "Child folder", childNested);
        var barSecond = Url("11", "Second", "https://example.com/second");
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            barFirst,
            childFolder,
            barSecond);

        var otherUrl = Url(
            "13",
            "Other",
            "https://other.example/path");
        var other = Folder("2", "Other bookmarks", otherUrl);
        var synced = Folder("3", "Mobile bookmarks");

        var document = new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                bookmarkBar,
                other,
                synced,
                EmptyProperties),
            EmptyProperties);

        return new Fixture(
            document,
            bookmarkBar,
            other,
            synced,
            childFolder,
            barFirst,
            barSecond,
            otherUrl);
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

    private sealed class DelegateReader(
        Func<string, CancellationToken, Task<BookmarkDocument>> read)
        : IChromeBookmarksReader
    {
        public Task<BookmarkDocument> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            read(path, cancellationToken);
    }

    private sealed class CountingSearchService : IBookmarkSearchService
    {
        private readonly BookmarkSearchService _inner = new();

        public int BuildIndexCalls { get; private set; }

        public Task<BookmarkSearchIndex> BuildIndexAsync(
            BookmarkDocument document,
            CancellationToken cancellationToken)
        {
            BuildIndexCalls++;
            return _inner.BuildIndexAsync(document, cancellationToken);
        }

        public Task<IReadOnlyList<BookmarkNode>> SearchAsync(
            BookmarkSearchIndex index,
            string query,
            BookmarkSearchScope scope,
            BookmarkFolder? currentFolder,
            CancellationToken cancellationToken) =>
            _inner.SearchAsync(
                index,
                query,
                scope,
                currentFolder,
                cancellationToken);
    }

    private sealed record Fixture(
        BookmarkDocument Document,
        BookmarkFolder BookmarkBar,
        BookmarkFolder Other,
        BookmarkFolder Synced,
        BookmarkFolder ChildFolder,
        BookmarkUrl BarFirst,
        BookmarkUrl BarSecond,
        BookmarkUrl OtherUrl);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
