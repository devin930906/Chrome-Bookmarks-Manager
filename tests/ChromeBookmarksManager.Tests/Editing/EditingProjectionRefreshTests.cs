using System.Text.Json;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Editing;

public sealed class EditingProjectionRefreshTests
{
    [Fact]
    public async Task RefreshAfterRename_RebuildsTreeAndPreservesSelectedFolderReference()
    {
        var fixture = CreateFixture();
        var reader = new CountingReader(fixture.Document);
        var viewModel = CreateViewModel(reader);
        var editor = new BookmarkEditingService();

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        var childItem = Assert.Single(viewModel.FolderRoots[0].Children);
        childItem.Parent!.IsExpanded = true;
        viewModel.SelectFolder(childItem);

        Assert.True(editor.RenameNode(
            fixture.Document,
            fixture.ChildFolder,
            "Renamed child"));

        await viewModel.RefreshProjectionsAfterEditAsync();

        Assert.Same(fixture.ChildFolder, viewModel.SelectedFolder);
        Assert.Equal("Renamed child", Assert.Single(viewModel.FolderRoots[0].Children).Name);
        Assert.True(viewModel.FolderRoots[0].IsExpanded);
        Assert.Equal("Renamed child | 1 bookmarks", viewModel.SelectionSummaryText);
        Assert.Equal(1, reader.ReadCalls);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
    }

    [Fact]
    public async Task RefreshAfterBookmarkRenameAndUrlEdit_PreservesSelectionAndSearchesCurrentValues()
    {
        var fixture = CreateFixture();
        var reader = new CountingReader(fixture.Document);
        var viewModel = CreateViewModel(reader);
        var editor = new BookmarkEditingService();

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SelectedBookmark = fixture.BarFirst;

        Assert.True(editor.RenameNode(
            fixture.Document,
            fixture.BarFirst,
            "Fresh needle name"));
        Assert.True(editor.EditUrl(
            fixture.Document,
            fixture.BarFirst,
            "https://updated.example/fresh-url"));

        await viewModel.RefreshProjectionsAfterEditAsync();

        Assert.Same(fixture.BarFirst, viewModel.SelectedBookmark);
        Assert.Same(
            fixture.BarFirst,
            Assert.Single(
                viewModel.CurrentBookmarks,
                bookmark => ReferenceEquals(bookmark, fixture.BarFirst)));

        viewModel.SearchText = "fresh-url";
        await viewModel.WaitForPendingSearchAsync();

        Assert.Same(fixture.BarFirst, Assert.Single(viewModel.SearchResults));
        Assert.Equal(1, reader.ReadCalls);
    }

    [Fact]
    public async Task RefreshAfterAddBookmark_UpdatesCurrentFolderSearchIndexAndCountsWithoutDiskReread()
    {
        var fixture = CreateFixture();
        var reader = new CountingReader(fixture.Document);
        var search = new CountingSearchService();
        var viewModel = new MainViewModel(reader, search, TimeSpan.Zero);
        var editor = new BookmarkEditingService();

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        var added = editor.AddBookmark(
            fixture.Document,
            fixture.BookmarkBar,
            "Projection needle",
            "https://projection.example/new");

        await viewModel.RefreshProjectionsAfterEditAsync(
            preferredBookmark: added);

        Assert.Equal(2, search.BuildIndexCalls);
        Assert.Equal(1, reader.ReadCalls);
        Assert.Contains(
            viewModel.CurrentBookmarks,
            bookmark => ReferenceEquals(bookmark, added));
        Assert.Same(added, viewModel.SelectedBookmark);
        Assert.Equal("4 URLs | 4 folders", viewModel.DocumentSummaryText);
        Assert.Equal("Bookmarks bar | 2 bookmarks", viewModel.SelectionSummaryText);

        viewModel.SearchText = "projection.example";
        await viewModel.WaitForPendingSearchAsync();

        Assert.Same(added, Assert.Single(viewModel.SearchResults));
    }

    [Fact]
    public async Task RefreshAfterAddFolder_UpdatesFolderTreeAndCanPreferNewFolder()
    {
        var fixture = CreateFixture();
        var reader = new CountingReader(fixture.Document);
        var viewModel = CreateViewModel(reader);
        var editor = new BookmarkEditingService();

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        var added = editor.AddFolder(
            fixture.Document,
            fixture.BookmarkBar,
            "Added folder");

        await viewModel.RefreshProjectionsAfterEditAsync(
            preferredFolder: added);

        var addedItem = Assert.Single(
            viewModel.FolderRoots[0].Children,
            item => ReferenceEquals(item.Folder, added));

        Assert.Same(added, viewModel.SelectedFolder);
        Assert.True(addedItem.IsSelected);
        Assert.Empty(viewModel.CurrentBookmarks);
        Assert.Equal("4 URLs | 5 folders", viewModel.DocumentSummaryText);
        Assert.Equal("Added folder | 0 bookmarks", viewModel.SelectionSummaryText);
        Assert.Equal(1, reader.ReadCalls);
    }

    [Fact]
    public async Task RefreshWhileSearchIsActive_RerunsSameQueryAgainstUpdatedInMemoryDocument()
    {
        var fixture = CreateFixture();
        var reader = new CountingReader(fixture.Document);
        var viewModel = CreateViewModel(reader);
        var editor = new BookmarkEditingService();

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SearchText = "live-needle";
        await viewModel.WaitForPendingSearchAsync();
        Assert.Empty(viewModel.SearchResults);

        var added = editor.AddBookmark(
            fixture.Document,
            fixture.Other,
            "Live needle",
            "https://example.com/live-needle");

        await viewModel.RefreshProjectionsAfterEditAsync();

        Assert.Equal("live-needle", viewModel.SearchText);
        Assert.True(viewModel.IsSearchActive);
        Assert.Same(added, Assert.Single(viewModel.SearchResults));
        Assert.Equal(1, reader.ReadCalls);
    }

    private static MainViewModel CreateViewModel(IChromeBookmarksReader reader) =>
        new(reader, new BookmarkSearchService(), TimeSpan.Zero);

    private static Fixture CreateFixture()
    {
        var barFirst = Url("10", "First", "https://example.com/first");
        var nested = Url("11", "Nested", "https://example.com/nested");
        var child = Folder("20", "Child folder", nested);
        var bookmarkBar = Folder("1", "Bookmarks bar", barFirst, child);
        var otherUrl = Url("12", "Other", "https://other.example/path");
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
            child,
            barFirst);
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

    private sealed class CountingReader(BookmarkDocument document)
        : IChromeBookmarksReader
    {
        public int ReadCalls { get; private set; }

        public Task<BookmarkDocument> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            return Task.FromResult(document);
        }
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
        BookmarkUrl BarFirst);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
