using System.ComponentModel;
using System.Text.Json;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Editing;

public sealed class MainViewModelEditingTests
{
    [Fact]
    public void Constructor_EditingStartsDisabledWithoutDocument()
    {
        var viewModel = CreateViewModel(CreateFixture().Document);

        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.CanAddBookmark);
        Assert.False(viewModel.CanAddFolder);
        Assert.False(viewModel.CanRenameSelectedFolder);
        Assert.False(viewModel.CanRenameSelectedBookmark);
        Assert.False(viewModel.CanEditSelectedBookmarkUrl);
    }

    [Fact]
    public async Task Load_SelectsPermanentRootWhichAcceptsChildrenButCannotBeRenamed()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.IsDirty);
        Assert.True(viewModel.CanAddBookmark);
        Assert.True(viewModel.CanAddFolder);
        Assert.False(viewModel.CanRenameSelectedFolder);
        Assert.False(viewModel.CanRenameSelectedBookmark);
        Assert.False(viewModel.CanEditSelectedBookmarkUrl);
    }

    [Fact]
    public async Task Selection_UpdatesEditingAvailability()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var child = Assert.Single(viewModel.FolderRoots[0].Children);
        viewModel.SelectFolder(child);

        Assert.True(viewModel.CanRenameSelectedFolder);
        Assert.True(viewModel.CanAddBookmark);
        Assert.True(viewModel.CanAddFolder);

        viewModel.SelectedBookmark = fixture.Nested;

        Assert.True(viewModel.CanRenameSelectedBookmark);
        Assert.True(viewModel.CanEditSelectedBookmarkUrl);
    }

    [Fact]
    public async Task RenameFolder_SuccessMarksDirtyAndRefreshesProjection()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SelectFolder(Assert.Single(viewModel.FolderRoots[0].Children));

        var changed = await viewModel.RenameSelectedFolderAsync("Renamed folder");

        Assert.True(changed);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.True(viewModel.IsDirty);
        Assert.Equal("Renamed folder", fixture.ChildFolder.Name);
        Assert.Equal("Renamed folder", Assert.Single(viewModel.FolderRoots[0].Children).Name);
        Assert.Same(fixture.ChildFolder, viewModel.SelectedFolder);
        Assert.Contains("Unsaved", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not saved", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenameBookmark_NoOpDoesNotDirtyOrRefreshIndex()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = CreateViewModel(fixture.Document, search);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SelectedBookmark = fixture.BarFirst;
        Assert.Equal(1, search.BuildIndexCalls);

        var changed = await viewModel.RenameSelectedBookmarkAsync(fixture.BarFirst.Name);

        Assert.False(changed);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(1, search.BuildIndexCalls);
    }

    [Fact]
    public async Task EditUrl_InvalidValueDoesNotDirtyDocument()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SelectedBookmark = fixture.BarFirst;

        var exception = await Assert.ThrowsAsync<BookmarkEditException>(
            () => viewModel.EditSelectedBookmarkUrlAsync("   "));

        Assert.Equal(BookmarkEditError.InvalidValue, exception.Error);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.IsDirty);
        Assert.Equal("https://example.com/first", fixture.BarFirst.Url);
    }

    [Fact]
    public async Task AddBookmark_SuccessMarksDirtyUpdatesCountsAndSelectsNewBookmark()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var added = await viewModel.AddBookmarkAsync(
            "Added bookmark",
            "https://added.example/path");

        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.True(viewModel.IsDirty);
        Assert.Same(fixture.BookmarkBar, added.Parent);
        Assert.Same(added, viewModel.SelectedBookmark);
        Assert.Contains(viewModel.CurrentBookmarks, bookmark => ReferenceEquals(bookmark, added));
        Assert.Equal("4 URLs | 4 folders", viewModel.DocumentSummaryText);
    }

    [Fact]
    public async Task AddFolder_SuccessMarksDirtyAndSelectsNewFolder()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        var added = await viewModel.AddFolderAsync("Added folder");

        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.True(viewModel.IsDirty);
        Assert.Same(fixture.BookmarkBar, added.Parent);
        Assert.Same(added, viewModel.SelectedFolder);
        Assert.Equal("Added folder | 0 bookmarks", viewModel.SelectionSummaryText);
        Assert.True(viewModel.CanRenameSelectedFolder);
        Assert.Equal("3 URLs | 5 folders", viewModel.DocumentSummaryText);
    }

    [Fact]
    public async Task SearchResultBookmarkEdit_PreservesSelectedBookmarkAcrossProjectionRefresh()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        viewModel.SearchText = "other target";
        await viewModel.WaitForPendingSearchAsync();
        var result = Assert.Single(viewModel.SearchResults);
        Assert.Same(fixture.OtherUrl, result);
        viewModel.SelectedBookmark = result;

        var changed = await viewModel.RenameSelectedBookmarkAsync("Other target renamed");

        Assert.True(changed);
        Assert.Same(fixture.OtherUrl, viewModel.SelectedBookmark);
        Assert.Same(fixture.OtherUrl, Assert.Single(viewModel.SearchResults));
        Assert.Equal("Other target renamed", fixture.OtherUrl.Name);
    }

    [Fact]
    public async Task SuccessfulReloadAfterDirtyDocumentResetsStateToLoadedClean()
    {
        var first = CreateFixture();
        var second = CreateFixture("Second bar");
        var call = 0;
        var reader = new DelegateReader((_, _) =>
        {
            call++;
            return Task.FromResult(call == 1 ? first.Document : second.Document);
        });
        var viewModel = new MainViewModel(
            reader,
            new BookmarkSearchService(),
            TimeSpan.Zero);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        await viewModel.AddFolderAsync("Temporary");
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);

        await viewModel.LoadBookmarksAsync(
            @"C:\Synthetic\Bookmarks-2",
            discardDirtyChanges: true);

        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.IsDirty);
        Assert.Same(second.Document, viewModel.Document);
        Assert.Same(second.BookmarkBar, viewModel.SelectedFolder);
    }

    [Fact]
    public async Task EditingAvailabilityRaisesPropertyChangedWithSelectionAndDirtyState()
    {
        var fixture = CreateFixture();
        var viewModel = CreateViewModel(fixture.Document);
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SelectedBookmark = fixture.BarFirst;
        await viewModel.RenameSelectedBookmarkAsync("First renamed");

        Assert.Contains(nameof(MainViewModel.CanAddBookmark), notifications);
        Assert.Contains(nameof(MainViewModel.CanRenameSelectedBookmark), notifications);
        Assert.Contains(nameof(MainViewModel.CanEditSelectedBookmarkUrl), notifications);
        Assert.Contains(nameof(MainViewModel.IsDirty), notifications);
    }

    private static MainViewModel CreateViewModel(
        BookmarkDocument document,
        IBookmarkSearchService? searchService = null) =>
        new(
            new DelegateReader((_, _) => Task.FromResult(document)),
            searchService ?? new BookmarkSearchService(),
            TimeSpan.Zero);

    private static Fixture CreateFixture(string bookmarkBarName = "Bookmarks bar")
    {
        var barFirst = Url("10", "First", "https://example.com/first");
        var nested = Url("11", "Nested", "https://example.com/nested");
        var childFolder = Folder("20", "Child folder", nested);
        var bookmarkBar = Folder(
            "1",
            bookmarkBarName,
            barFirst,
            childFolder);
        var otherUrl = Url(
            "12",
            "Other target",
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
            nested,
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

    private static BookmarkUrl Url(string id, string name, string url) =>
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
        BookmarkUrl Nested,
        BookmarkUrl OtherUrl);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
