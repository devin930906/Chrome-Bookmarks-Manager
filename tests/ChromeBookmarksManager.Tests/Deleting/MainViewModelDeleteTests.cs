using System.Text.Json;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Deleting;

public sealed class MainViewModelDeleteTests
{
    [Fact]
    public void Constructor_DeleteAndBatchCapabilitiesStartDisabled()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);

        Assert.Empty(viewModel.SelectedBookmarks);
        Assert.Equal(0, viewModel.SelectedBookmarkCount);
        Assert.False(viewModel.HasMultipleSelectedBookmarks);
        Assert.False(viewModel.CanDeleteSelectedBookmarks);
        Assert.False(viewModel.CanDeleteSelectedFolder);
        Assert.False(viewModel.CanMoveSelectedBookmarks);
    }

    [Fact]
    public async Task MultiSelection_IsNormalizedToDisplayedOrderAndDisablesAmbiguousEditing()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.UpdateSelectedBookmarks(
            new[]
            {
                fixture.BarSecond,
                fixture.BarFirst,
                fixture.BarSecond
            });

        Assert.Equal(
            new[] { fixture.BarFirst, fixture.BarSecond },
            viewModel.SelectedBookmarks);
        Assert.Equal(2, viewModel.SelectedBookmarkCount);
        Assert.True(viewModel.HasMultipleSelectedBookmarks);
        Assert.True(viewModel.CanDeleteSelectedBookmarks);
        Assert.True(viewModel.CanMoveSelectedBookmarks);
        Assert.False(viewModel.CanRenameSelectedBookmark);
        Assert.False(viewModel.CanEditSelectedBookmarkUrl);
        Assert.False(viewModel.CanMoveSelectedBookmark);
    }

    [Fact]
    public async Task DeleteSelectedBookmarks_RebuildsIndexMarksDirtyAndClearsSelection()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        Assert.Equal(1, search.BuildIndexCalls);

        viewModel.UpdateSelectedBookmarks(
            new[] { fixture.BarFirst, fixture.BarSecond });

        var changed = await viewModel.DeleteSelectedBookmarksAsync();

        Assert.True(changed);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(2, search.BuildIndexCalls);
        Assert.Empty(viewModel.SelectedBookmarks);
        Assert.Null(viewModel.SelectedBookmark);
        Assert.Empty(viewModel.CurrentBookmarks);
        Assert.Equal("3 URLs | 4 folders", viewModel.DocumentSummaryText);
        Assert.Equal(3, fixture.Document.UrlCount);
    }

    [Fact]
    public async Task DeleteSelectedSearchResult_RemovesItFromActiveSearch()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.SearchText = "https://example.com/first";
        await viewModel.WaitForPendingSearchAsync();
        var result = Assert.Single(viewModel.SearchResults);
        viewModel.UpdateSelectedBookmarks(new[] { result });

        var changed = await viewModel.DeleteSelectedBookmarksAsync();
        await viewModel.WaitForPendingSearchAsync();

        Assert.True(changed);
        Assert.True(viewModel.IsSearchActive);
        Assert.Empty(viewModel.SearchResults);
        Assert.Equal(2, search.BuildIndexCalls);
        Assert.Same(fixture.BookmarkBar, viewModel.SelectedFolder);
        Assert.Empty(viewModel.SelectedBookmarks);
    }

    [Fact]
    public async Task DeleteSelectedFolder_SelectsOriginalParentAndRebuildsIndex()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var childItem = Assert.Single(viewModel.FolderRoots[0].Children);
        viewModel.SelectFolder(childItem);
        Assert.Same(fixture.ChildFolder, viewModel.SelectedFolder);
        Assert.True(viewModel.CanDeleteSelectedFolder);

        var changed = await viewModel.DeleteSelectedFolderAsync();

        Assert.True(changed);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(2, search.BuildIndexCalls);
        Assert.Same(fixture.BookmarkBar, viewModel.SelectedFolder);
        Assert.Empty(viewModel.FolderRoots[0].Children);
        Assert.Equal("4 URLs | 3 folders", viewModel.DocumentSummaryText);
        Assert.Equal(4, fixture.Document.UrlCount);
        Assert.Equal(3, fixture.Document.FolderCount);
    }

    [Fact]
    public async Task DeleteFolderAsync_RightPaneChildDeletesChildAndKeepsParentSelected()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        Assert.Same(fixture.BookmarkBar, viewModel.SelectedFolder);
        Assert.Single(viewModel.FolderRoots[0].Children);

        var changed = await viewModel.DeleteFolderAsync(
            fixture.ChildFolder);

        Assert.True(changed);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(2, search.BuildIndexCalls);
        Assert.Same(fixture.BookmarkBar, viewModel.SelectedFolder);
        Assert.Empty(viewModel.FolderRoots[0].Children);
        Assert.DoesNotContain(
            fixture.BookmarkBar.Children,
            node => ReferenceEquals(node, fixture.ChildFolder));
    }

    [Fact]
    public async Task PermanentRoot_DeleteRemainsDisabledAndRejected()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        Assert.Same(fixture.BookmarkBar, viewModel.SelectedFolder);
        Assert.False(viewModel.CanDeleteSelectedFolder);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.DeleteSelectedFolderAsync());

        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.Same(fixture.BookmarkBar, viewModel.SelectedFolder);
    }

    [Fact]
    public async Task MoveSelectedBookmarksToEnd_UsesDisplayOrderWithoutIndexRebuild()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.UpdateSelectedBookmarks(
            new[]
            {
                fixture.BarSecond,
                fixture.BarFirst
            });

        var changed = await viewModel.MoveSelectedBookmarksToEndAsync(
            fixture.Other);

        Assert.True(changed);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(1, search.BuildIndexCalls);
        Assert.Empty(viewModel.SelectedBookmarks);
        Assert.Null(viewModel.SelectedBookmark);
        Assert.Same(fixture.Other, viewModel.SelectedFolder);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.OtherFirst,
                fixture.OtherSecond,
                fixture.BarFirst,
                fixture.BarSecond
            },
            fixture.Other.Children);
    }

    [Fact]
    public async Task BatchMoveAlreadyAtEnd_IsNoOpAndDoesNotDirty()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SelectFolder(viewModel.FolderRoots[1]);

        viewModel.UpdateSelectedBookmarks(
            new[]
            {
                fixture.OtherFirst,
                fixture.OtherSecond
            });

        var changed = await viewModel.MoveSelectedBookmarksToEndAsync(
            fixture.Other);

        Assert.False(changed);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.Equal(1, search.BuildIndexCalls);
        Assert.Equal(2, viewModel.SelectedBookmarkCount);
    }

    [Fact]
    public async Task FolderAndSearchChanges_ClearBatchSelection()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.UpdateSelectedBookmarks(
            new[] { fixture.BarFirst, fixture.BarSecond });
        Assert.Equal(2, viewModel.SelectedBookmarkCount);

        viewModel.SearchText = "example.com";
        Assert.Empty(viewModel.SelectedBookmarks);

        viewModel.SearchText = string.Empty;
        viewModel.UpdateSelectedBookmarks(
            new[] { fixture.BarFirst, fixture.BarSecond });
        viewModel.SelectFolder(viewModel.FolderRoots[1]);

        Assert.Empty(viewModel.SelectedBookmarks);
        Assert.Null(viewModel.SelectedBookmark);
    }


    [Fact]
    public async Task DeleteSingleBookmark_UndoRedo_RestoresIdentityCountsAndSearch()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.SearchText = "https://example.com/first";
        await viewModel.WaitForPendingSearchAsync();
        viewModel.UpdateSelectedBookmarks(new[] { fixture.BarFirst });

        Assert.True(await viewModel.DeleteSelectedBookmarksAsync());
        await viewModel.WaitForPendingSearchAsync();

        Assert.Null(fixture.BarFirst.Parent);
        Assert.Empty(viewModel.SearchResults);
        Assert.Equal(4, fixture.Document.UrlCount);
        Assert.Equal(2, search.BuildIndexCalls);
        Assert.Equal("Delete 1 bookmarks", viewModel.UndoDescription);

        Assert.True(await viewModel.UndoAsync());
        await viewModel.WaitForPendingSearchAsync();

        Assert.Same(fixture.BookmarkBar, fixture.BarFirst.Parent);
        Assert.Same(
            fixture.BarFirst,
            Assert.Single(viewModel.SearchResults));
        Assert.Equal(5, fixture.Document.UrlCount);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.Equal(3, search.BuildIndexCalls);

        Assert.True(await viewModel.RedoAsync());
        await viewModel.WaitForPendingSearchAsync();

        Assert.Null(fixture.BarFirst.Parent);
        Assert.Empty(viewModel.SearchResults);
        Assert.Equal(4, fixture.Document.UrlCount);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.Equal(4, search.BuildIndexCalls);
    }

    [Fact]
    public async Task BatchDelete_UndoRedo_RestoresOriginalMixedChildOrderAndIdentity()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.UpdateSelectedBookmarks(
            new[] { fixture.BarFirst, fixture.BarSecond });

        Assert.True(await viewModel.DeleteSelectedBookmarksAsync());
        Assert.Equal(
            new BookmarkNode[] { fixture.ChildFolder },
            fixture.BookmarkBar.Children);
        Assert.Equal(3, fixture.Document.UrlCount);

        Assert.True(await viewModel.UndoAsync());
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.ChildFolder,
                fixture.BarSecond
            },
            fixture.BookmarkBar.Children);
        Assert.Same(fixture.BookmarkBar, fixture.BarFirst.Parent);
        Assert.Same(fixture.BookmarkBar, fixture.BarSecond.Parent);
        Assert.Equal(5, fixture.Document.UrlCount);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);

        Assert.True(await viewModel.RedoAsync());
        Assert.Equal(
            new BookmarkNode[] { fixture.ChildFolder },
            fixture.BookmarkBar.Children);
        Assert.Null(fixture.BarFirst.Parent);
        Assert.Null(fixture.BarSecond.Parent);
        Assert.Equal(3, fixture.Document.UrlCount);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
    }

    [Fact]
    public async Task DeleteSelectedContentItems_MixedSelection_UndoRedoRestoresExactOrderCountsAndIdentity()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.UpdateSelectedContentItems(
            new[]
            {
                viewModel.CurrentItems[2],
                viewModel.CurrentItems[1],
                viewModel.CurrentItems[0]
            });

        Assert.True(viewModel.CanDeleteSelectedContentItems);
        Assert.True(
            await viewModel.DeleteSelectedContentItemsAsync());

        Assert.Empty(fixture.BookmarkBar.Children);
        Assert.Null(fixture.BarFirst.Parent);
        Assert.Null(fixture.ChildFolder.Parent);
        Assert.Null(fixture.BarSecond.Parent);
        Assert.Same(fixture.ChildFolder, fixture.Nested.Parent);
        Assert.Equal(2, fixture.Document.UrlCount);
        Assert.Equal(3, fixture.Document.FolderCount);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);

        Assert.True(await viewModel.UndoAsync());

        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.ChildFolder,
                fixture.BarSecond
            },
            fixture.BookmarkBar.Children);
        Assert.Same(fixture.BookmarkBar, fixture.BarFirst.Parent);
        Assert.Same(fixture.BookmarkBar, fixture.ChildFolder.Parent);
        Assert.Same(fixture.ChildFolder, fixture.Nested.Parent);
        Assert.Same(fixture.BookmarkBar, fixture.BarSecond.Parent);
        Assert.Equal(5, fixture.Document.UrlCount);
        Assert.Equal(4, fixture.Document.FolderCount);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);

        Assert.True(await viewModel.RedoAsync());

        Assert.Empty(fixture.BookmarkBar.Children);
        Assert.Null(fixture.BarFirst.Parent);
        Assert.Null(fixture.ChildFolder.Parent);
        Assert.Null(fixture.BarSecond.Parent);
        Assert.Same(fixture.ChildFolder, fixture.Nested.Parent);
        Assert.Equal(2, fixture.Document.UrlCount);
        Assert.Equal(3, fixture.Document.FolderCount);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
    }

    [Fact]
    public async Task DeleteFolderSubtree_UndoRedo_RestoresExactSubtreeCountsAndSelection()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var childItem = Assert.Single(viewModel.FolderRoots[0].Children);
        viewModel.SelectFolder(childItem);

        Assert.True(await viewModel.DeleteSelectedFolderAsync());
        Assert.Null(fixture.ChildFolder.Parent);
        Assert.Equal(4, fixture.Document.UrlCount);
        Assert.Equal(3, fixture.Document.FolderCount);
        Assert.Equal("Delete folder", viewModel.UndoDescription);

        Assert.True(await viewModel.UndoAsync());
        Assert.Same(fixture.BookmarkBar, fixture.ChildFolder.Parent);
        Assert.Same(fixture.BookmarkBar, viewModel.SelectedFolder);
        Assert.Same(
            fixture.ChildFolder,
            Assert.Single(viewModel.FolderRoots[0].Children).Folder);
        Assert.Equal(5, fixture.Document.UrlCount);
        Assert.Equal(4, fixture.Document.FolderCount);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);

        Assert.True(await viewModel.RedoAsync());
        Assert.Null(fixture.ChildFolder.Parent);
        Assert.Empty(viewModel.FolderRoots[0].Children);
        Assert.Equal(4, fixture.Document.UrlCount);
        Assert.Equal(3, fixture.Document.FolderCount);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
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
        var nested = Url("12", "Nested", "https://example.com/nested");
        var childFolder = Folder("20", "Child folder", nested);
        var barSecond = Url("11", "Second", "https://example.com/second");
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            barFirst,
            childFolder,
            barSecond);

        var otherFirst = Url("13", "Other first", "https://other.example/first");
        var otherSecond = Url("14", "Other second", "https://other.example/second");
        var other = Folder(
            "2",
            "Other bookmarks",
            otherFirst,
            otherSecond);
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
            nested,
            otherFirst,
            otherSecond);
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
        BookmarkUrl Nested,
        BookmarkUrl OtherFirst,
        BookmarkUrl OtherSecond);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
