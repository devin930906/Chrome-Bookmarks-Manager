using System.Text.Json;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Editing;

public sealed class DirtyDocumentProtectionTests
{
    [Fact]
    public async Task ReloadWhileDirtyWithoutDiscardAuthorization_IsRejectedAndKeepsCurrentDocument()
    {
        var first = CreateDocument("First bar");
        var second = CreateDocument("Second bar");
        var reader = new SequenceReader(first, second);
        var viewModel = CreateViewModel(reader);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        await viewModel.AddFolderAsync("Unsaved folder");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks-2"));

        Assert.Contains("unsaved", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, reader.ReadCalls);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.True(viewModel.IsDirty);
        Assert.Same(first, viewModel.Document);
        Assert.Equal(@"C:\Synthetic\Bookmarks", viewModel.SourcePath);
        Assert.Contains(
            first.Roots.BookmarkBar.Children,
            node => node is BookmarkFolder folder &&
                    folder.Name == "Unsaved folder");
    }

    [Fact]
    public async Task ReloadWhileDirtyWithDiscardAuthorization_ReplacesDocumentAndReturnsClean()
    {
        var first = CreateDocument("First bar");
        var second = CreateDocument("Second bar");
        var reader = new SequenceReader(first, second);
        var viewModel = CreateViewModel(reader);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        await viewModel.AddFolderAsync("Unsaved folder");

        await viewModel.LoadBookmarksAsync(
            @"C:\Synthetic\Bookmarks-2",
            discardDirtyChanges: true);

        Assert.Equal(2, reader.ReadCalls);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.IsDirty);
        Assert.Same(second, viewModel.Document);
        Assert.Equal(@"C:\Synthetic\Bookmarks-2", viewModel.SourcePath);
        Assert.Equal("Second bar", viewModel.SelectedFolder?.Name);
    }

    [Fact]
    public async Task CleanDocumentReload_DoesNotRequireDiscardAuthorization()
    {
        var first = CreateDocument("First bar");
        var second = CreateDocument("Second bar");
        var reader = new SequenceReader(first, second);
        var viewModel = CreateViewModel(reader);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks-2");

        Assert.Equal(2, reader.ReadCalls);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.Same(second, viewModel.Document);
    }

    [Fact]
    public async Task DirtyReloadRejection_HappensBeforeSearchOrBrowserStateIsCleared()
    {
        var first = CreateDocument("First bar");
        var second = CreateDocument("Second bar");
        var reader = new SequenceReader(first, second);
        var viewModel = CreateViewModel(reader);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SearchText = "first";
        await viewModel.WaitForPendingSearchAsync();
        await viewModel.AddFolderAsync("Unsaved folder");

        var selectedFolder = viewModel.SelectedFolder;
        var folderRoots = viewModel.FolderRoots;
        var searchText = viewModel.SearchText;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks-2"));

        Assert.Same(selectedFolder, viewModel.SelectedFolder);
        Assert.Same(folderRoots, viewModel.FolderRoots);
        Assert.Equal(searchText, viewModel.SearchText);
        Assert.True(viewModel.CanBrowseDocument);
    }

    private static MainViewModel CreateViewModel(IChromeBookmarksReader reader) =>
        new(reader, new BookmarkSearchService(), TimeSpan.Zero);

    private static BookmarkDocument CreateDocument(string bookmarkBarName)
    {
        var first = Url("10", "First target", "https://example.com/first");
        var bookmarkBar = Folder("1", bookmarkBarName, first);
        var other = Folder("2", "Other bookmarks");
        var synced = Folder("3", "Mobile bookmarks");

        return new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                bookmarkBar,
                other,
                synced,
                EmptyProperties),
            EmptyProperties);
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

    private sealed class SequenceReader(
        BookmarkDocument first,
        BookmarkDocument second)
        : IChromeBookmarksReader
    {
        public int ReadCalls { get; private set; }

        public Task<BookmarkDocument> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            return Task.FromResult(ReadCalls == 1 ? first : second);
        }
    }

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
