using System.Text.Json;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void Constructor_StartsWithoutDocument()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(DocumentState.NoDocument, viewModel.State);
    }

    [Fact]
    public void Constructor_ExposesStableApplicationTitle()
    {
        var viewModel = CreateViewModel();

        Assert.Equal("Chrome Bookmarks Manager", viewModel.ApplicationTitle);
    }

    [Fact]
    public void Constructor_ExplainsThatNoFileIsOpen()
    {
        var viewModel = CreateViewModel();

        Assert.Equal("No Bookmarks file is open.", viewModel.StatusText);
    }

    [Fact]
    public async Task LoadBookmarksAsync_Success_TransitionsToLoadedClean()
    {
        var document = TestDocuments.Empty();
        var reader = new StubReader((_, _) => Task.FromResult(document));
        var viewModel = new MainViewModel(reader);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.Same(document, viewModel.Document);
        Assert.Equal(@"C:\Synthetic\Bookmarks", viewModel.SourcePath);
        Assert.Contains("0 URLs", viewModel.StatusText);
        Assert.True(viewModel.CanOpenBookmarks);
        Assert.False(viewModel.CanCancelLoad);
    }

    [Fact]
    public async Task LoadBookmarksAsync_ReadFailure_TransitionsToLoadFailedWithoutDocument()
    {
        var reader = new StubReader((_, _) =>
            Task.FromException<BookmarkDocument>(
                new ChromeBookmarksReadException(
                    ChromeBookmarksReadError.MalformedJson,
                    "Invalid file.")));
        var viewModel = new MainViewModel(reader);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        Assert.Equal(DocumentState.LoadFailed, viewModel.State);
        Assert.Null(viewModel.Document);
        Assert.Equal("Invalid file.", viewModel.StatusText);
        Assert.True(viewModel.CanOpenBookmarks);
        Assert.False(viewModel.CanCancelLoad);
    }

    [Fact]
    public async Task CancelLoad_CancelsReaderAndReturnsToNoDocument()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new StubReader(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return TestDocuments.Empty();
        });
        var viewModel = new MainViewModel(reader);

        var load = viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        await entered.Task;
        viewModel.CancelLoad();
        await load;

        Assert.Equal(DocumentState.NoDocument, viewModel.State);
        Assert.Null(viewModel.Document);
        Assert.Equal("Loading was canceled.", viewModel.StatusText);
        Assert.True(viewModel.CanOpenBookmarks);
        Assert.False(viewModel.CanCancelLoad);
    }

    [Fact]
    public async Task LoadBookmarksAsync_SecondConcurrentLoad_IsRejected()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<BookmarkDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new StubReader(async (_, token) =>
        {
            entered.TrySetResult();
            return await release.Task.WaitAsync(token);
        });
        var viewModel = new MainViewModel(reader);

        var firstLoad = viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        await entered.Task;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks-2"));

        release.SetResult(TestDocuments.Empty());
        await firstLoad;
    }

    private static MainViewModel CreateViewModel() =>
        new(new StubReader((_, _) => Task.FromResult(TestDocuments.Empty())));

    private sealed class StubReader(
        Func<string, CancellationToken, Task<BookmarkDocument>> read) : IChromeBookmarksReader
    {
        public Task<BookmarkDocument> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            read(path, cancellationToken);
    }

    private static class TestDocuments
    {
        public static BookmarkDocument Empty()
        {
            var extensionData = new Dictionary<string, JsonElement>();
            var bookmarkBar = Folder("1", "11111111-1111-4111-8111-111111111111", "Bookmarks bar", extensionData);
            var other = Folder("2", "22222222-2222-4222-8222-222222222222", "Other bookmarks", extensionData);
            var synced = Folder("3", "33333333-3333-4333-8333-333333333333", "Mobile bookmarks", extensionData);

            return new BookmarkDocument(
                1,
                null,
                null,
                new BookmarkRoots(bookmarkBar, other, synced, extensionData),
                extensionData);
        }

        private static BookmarkFolder Folder(
            string id,
            string guid,
            string name,
            IReadOnlyDictionary<string, JsonElement> extensionData) =>
            new(
                id,
                Guid.Parse(guid),
                name,
                null,
                null,
                null,
                null,
                extensionData,
                Array.Empty<BookmarkNode>());
    }
}
