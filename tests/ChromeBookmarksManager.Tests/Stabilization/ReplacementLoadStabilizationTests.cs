using System.Text.Json;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Stabilization;

public sealed class ReplacementLoadStabilizationTests
{
    [Fact]
    public async Task ReplacementLoad_ReadFailure_PreservesExistingDirtyDocumentSearchAndHistory()
    {
        var first = CreateDocument("First bar");
        var calls = 0;
        var reader = new DelegateReader((_, _) =>
        {
            calls++;
            if (calls == 1)
            {
                return Task.FromResult(first);
            }

            throw new ChromeBookmarksReadException(
                ChromeBookmarksReadError.IoFailure,
                "Synthetic replacement read failure.");
        });
        var viewModel = new MainViewModel(
            reader,
            new BookmarkSearchService(),
            TimeSpan.FromSeconds(30));

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        await viewModel.AddFolderAsync("Unsaved folder");

        var originalDocument = viewModel.Document;
        var originalSourcePath = viewModel.SourcePath;
        var originalFolderRoots = viewModel.FolderRoots;
        var originalSelectedFolder = viewModel.SelectedFolder;

        viewModel.SearchText = "pending-query";
        Assert.True(viewModel.IsSearchActive);
        Assert.False(viewModel.IsSearchBusy);

        await viewModel.LoadBookmarksAsync(
            @"C:\Synthetic\Broken-Replacement",
            discardDirtyChanges: true);

        Assert.Equal(2, calls);
        Assert.Same(originalDocument, viewModel.Document);
        Assert.Equal(originalSourcePath, viewModel.SourcePath);
        Assert.Same(originalFolderRoots, viewModel.FolderRoots);
        Assert.Same(originalSelectedFolder, viewModel.SelectedFolder);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.CanUndo);
        Assert.Equal("pending-query", viewModel.SearchText);
        Assert.True(viewModel.IsSearchActive);

        viewModel.SearchText = string.Empty;
        await viewModel.WaitForPendingSearchAsync();
    }

    [Fact]
    public async Task QueuedReplacementLoad_RejectsSecondLoadBeforeItCanQueueBehindOperationGate()
    {
        var first = CreateDocument("First bar");
        var second = CreateDocument("Second bar");
        var third = CreateDocument("Third bar");
        var reader = new SequenceReader(first, second, third);
        var searchService = new BlockingSearchService();
        var viewModel = new MainViewModel(
            reader,
            searchService,
            TimeSpan.Zero);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        searchService.BlockNextBuild();

        var holdingMutation = viewModel.AddFolderAsync("Hold operation gate");
        await searchService.BlockedBuildEntered.Task;

        var firstReplacement = viewModel.LoadBookmarksAsync(
            @"C:\Synthetic\Bookmarks-2",
            discardDirtyChanges: true);
        var duplicateReplacement = viewModel.LoadBookmarksAsync(
            @"C:\Synthetic\Bookmarks-3",
            discardDirtyChanges: true);

        try
        {
            Assert.True(
                duplicateReplacement.IsCompleted,
                "A duplicate Load request must be rejected before waiting on the document operation gate.");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => duplicateReplacement);
            Assert.Contains(
                "already",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            searchService.ReleaseBlockedBuild(first);
            await holdingMutation;
            await firstReplacement;

            try
            {
                await duplicateReplacement;
            }
            catch (InvalidOperationException)
            {
            }
        }

        Assert.Equal(2, reader.ReadCalls);
        Assert.Same(second, viewModel.Document);
        Assert.Equal(
            @"C:\Synthetic\Bookmarks-2",
            viewModel.SourcePath);
    }

    private static BookmarkDocument CreateDocument(string bookmarkBarName)
    {
        var first = Url(
            "10",
            bookmarkBarName + " target",
            "https://example.com/" + bookmarkBarName.Replace(" ", "-", StringComparison.Ordinal));
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

    private sealed class SequenceReader(
        params BookmarkDocument[] documents)
        : IChromeBookmarksReader
    {
        public int ReadCalls { get; private set; }

        public Task<BookmarkDocument> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            var index = ReadCalls++;
            if (index >= documents.Length)
            {
                throw new InvalidOperationException(
                    "Synthetic reader received more calls than expected.");
            }

            return Task.FromResult(documents[index]);
        }
    }

    private sealed class BlockingSearchService : IBookmarkSearchService
    {
        private bool _blockNextBuild;
        private TaskCompletionSource<BookmarkSearchIndex>? _releaseBlockedBuild;

        public TaskCompletionSource BlockedBuildEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void BlockNextBuild()
        {
            _blockNextBuild = true;
            _releaseBlockedBuild =
                new TaskCompletionSource<BookmarkSearchIndex>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void ReleaseBlockedBuild(BookmarkDocument document)
        {
            _releaseBlockedBuild?.TrySetResult(
                new BookmarkSearchIndex(document));
        }

        public Task<BookmarkSearchIndex> BuildIndexAsync(
            BookmarkDocument document,
            CancellationToken cancellationToken)
        {
            if (_blockNextBuild)
            {
                _blockNextBuild = false;
                BlockedBuildEntered.TrySetResult();
                return _releaseBlockedBuild!.Task;
            }

            return Task.FromResult(
                new BookmarkSearchIndex(document, cancellationToken));
        }

        public Task<IReadOnlyList<BookmarkNode>> SearchAsync(
            BookmarkSearchIndex index,
            string query,
            BookmarkSearchScope scope,
            BookmarkFolder? currentFolder,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BookmarkUrl>>(
                Array.Empty<BookmarkUrl>());
    }

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
