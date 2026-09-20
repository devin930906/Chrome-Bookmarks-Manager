using System.Text.Json;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.History;

public sealed class MainViewModelEditingHistoryTests
{
    [Fact]
    public async Task Load_StartsAtCleanHistoryBaseline()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.Null(viewModel.UndoDescription);
        Assert.Null(viewModel.RedoDescription);
    }

    [Fact]
    public async Task AddBookmark_UndoRedo_PreservesIdentityCountsAndDirtyState()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        var beforeUrls = fixture.Document.UrlCount;

        var added = await viewModel.AddBookmarkAsync(
            "Added",
            "https://added.example/path");

        Assert.True(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.Equal("Add bookmark", viewModel.UndoDescription);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.Equal(beforeUrls + 1, fixture.Document.UrlCount);
        Assert.Equal(2, search.BuildIndexCalls);

        var undone = await viewModel.UndoAsync();

        Assert.True(undone);
        Assert.Null(added.Parent);
        Assert.Equal(beforeUrls, fixture.Document.UrlCount);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.CanUndo);
        Assert.True(viewModel.CanRedo);
        Assert.Equal("Add bookmark", viewModel.RedoDescription);
        Assert.Equal(3, search.BuildIndexCalls);

        var redone = await viewModel.RedoAsync();

        Assert.True(redone);
        Assert.Same(fixture.BookmarkBar, added.Parent);
        Assert.Contains(
            fixture.BookmarkBar.Children,
            node => ReferenceEquals(node, added));
        Assert.Equal(beforeUrls + 1, fixture.Document.UrlCount);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.Equal(4, search.BuildIndexCalls);
    }

    [Fact]
    public async Task AddFolder_Undo_RemovesFolderAndFallsBackToSafeSelection()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        var beforeFolders = fixture.Document.FolderCount;

        var added = await viewModel.AddFolderAsync("Temporary folder");

        Assert.Same(added, viewModel.SelectedFolder);
        Assert.Equal(beforeFolders + 1, fixture.Document.FolderCount);

        Assert.True(await viewModel.UndoAsync());

        Assert.Null(added.Parent);
        Assert.Equal(beforeFolders, fixture.Document.FolderCount);
        Assert.Same(fixture.BookmarkBar, viewModel.SelectedFolder);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
    }

    [Fact]
    public async Task RenameBookmark_UndoRedo_TracksExactValuesAndCleanState()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SelectedBookmark = fixture.BarFirst;
        var original = fixture.BarFirst.Name;

        Assert.True(
            await viewModel.RenameSelectedBookmarkAsync(
                "First renamed"));

        Assert.Equal("First renamed", fixture.BarFirst.Name);
        Assert.Equal("Rename bookmark", viewModel.UndoDescription);

        Assert.True(await viewModel.UndoAsync());

        Assert.Equal(original, fixture.BarFirst.Name);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.True(viewModel.CanRedo);

        Assert.True(await viewModel.RedoAsync());

        Assert.Equal("First renamed", fixture.BarFirst.Name);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
    }

    [Fact]
    public async Task RenameNoOp_DoesNotCreateHistoryEntry()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SelectedBookmark = fixture.BarFirst;

        var changed = await viewModel.RenameSelectedBookmarkAsync(
            fixture.BarFirst.Name);

        Assert.False(changed);
        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
    }

    [Fact]
    public async Task UrlEdit_UndoRedo_RebuildsActiveSearchAndPreservesIdentity()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SelectedBookmark = fixture.BarFirst;
        var originalUrl = fixture.BarFirst.Url;

        Assert.True(
            await viewModel.EditSelectedBookmarkUrlAsync(
                "https://changed.example/path"));

        viewModel.SearchText = "changed.example";
        await viewModel.WaitForPendingSearchAsync();

        Assert.Same(
            fixture.BarFirst,
            Assert.Single(viewModel.SearchResults));

        Assert.True(await viewModel.UndoAsync());
        await viewModel.WaitForPendingSearchAsync();

        Assert.Equal(originalUrl, fixture.BarFirst.Url);
        Assert.True(viewModel.IsSearchActive);
        Assert.Empty(viewModel.SearchResults);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);

        Assert.True(await viewModel.RedoAsync());
        await viewModel.WaitForPendingSearchAsync();

        Assert.Equal(
            "https://changed.example/path",
            fixture.BarFirst.Url);
        Assert.Same(
            fixture.BarFirst,
            Assert.Single(viewModel.SearchResults));
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.True(search.BuildIndexCalls >= 4);
    }

    [Fact]
    public async Task NewEditAfterUndo_TruncatesRedoBranch()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SelectedBookmark = fixture.BarFirst;

        Assert.True(
            await viewModel.RenameSelectedBookmarkAsync(
                "First renamed"));
        Assert.True(await viewModel.UndoAsync());
        Assert.True(viewModel.CanRedo);

        viewModel.SelectedBookmark = fixture.BarFirst;
        Assert.True(
            await viewModel.EditSelectedBookmarkUrlAsync(
                "https://branch.example/new"));

        Assert.False(viewModel.CanRedo);
        Assert.Null(viewModel.RedoDescription);
        Assert.True(viewModel.CanUndo);
        Assert.Equal("Edit bookmark URL", viewModel.UndoDescription);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
    }

    [Fact]
    public async Task SuccessfulDiscardReload_ClearsHistoryAndReturnsClean()
    {
        var first = CreateFixture();
        var second = CreateFixture("Second bar");
        var call = 0;
        var reader = new DelegateReader((_, _) =>
        {
            call++;
            return Task.FromResult(
                call == 1 ? first.Document : second.Document);
        });
        var viewModel = new MainViewModel(
            reader,
            new BookmarkSearchService(),
            TimeSpan.Zero);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        await viewModel.AddFolderAsync("Temporary");

        Assert.True(viewModel.CanUndo);
        Assert.True(viewModel.IsDirty);

        await viewModel.LoadBookmarksAsync(
            @"C:\Synthetic\Bookmarks-2",
            discardDirtyChanges: true);

        Assert.Same(second.Document, viewModel.Document);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.Null(viewModel.UndoDescription);
        Assert.Null(viewModel.RedoDescription);
    }

    [Fact]
    public async Task UndoRedoWithoutHistory_AreNoOps()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        Assert.False(await viewModel.UndoAsync());
        Assert.False(await viewModel.RedoAsync());
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
    }

    private static MainViewModel CreateViewModel(
        BookmarkDocument document,
        IBookmarkSearchService? searchService = null) =>
        new(
            new DelegateReader((_, _) => Task.FromResult(document)),
            searchService ?? new BookmarkSearchService(),
            TimeSpan.Zero);

    private static Fixture CreateFixture(
        string bookmarkBarName = "Bookmarks bar")
    {
        var barFirst = Url(
            "10",
            "First",
            "https://example.com/first");
        var nested = Url(
            "11",
            "Nested",
            "https://example.com/nested");
        var childFolder = Folder(
            "20",
            "Child folder",
            nested);
        var bookmarkBar = Folder(
            "1",
            bookmarkBarName,
            barFirst,
            childFolder);
        var other = Folder("2", "Other bookmarks");
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
            nested);
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

        public Task<IReadOnlyList<BookmarkUrl>> SearchAsync(
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
        BookmarkUrl Nested);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
