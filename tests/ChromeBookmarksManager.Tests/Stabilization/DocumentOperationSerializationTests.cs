using System.Text.Json;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Stabilization;

public sealed class DocumentOperationSerializationTests
{
    [Fact]
    public async Task ConcurrentDocumentMutations_AreSerializedBeforeSecondMutationStarts()
    {
        var document = CreateDocument();
        var searchService = new BlockingSearchService();
        var viewModel = new MainViewModel(
            new StubReader(document),
            searchService,
            TimeSpan.Zero);

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        searchService.BlockNextBuild();

        var firstMutation = viewModel.AddFolderAsync("First queued folder");
        await searchService.BlockedBuildEntered.Task;

        var secondMutation = viewModel.AddFolderAsync("Second queued folder");

        Assert.Contains(
            document.Roots.BookmarkBar.Children,
            node => node is BookmarkFolder folder &&
                    folder.Name == "First queued folder");
        Assert.DoesNotContain(
            document.Roots.BookmarkBar.Children,
            node => node is BookmarkFolder folder &&
                    folder.Name == "Second queued folder");

        searchService.ReleaseBlockedBuild(document);

        var completed = await Task.WhenAll(firstMutation, secondMutation);

        Assert.Equal("First queued folder", completed[0].Name);
        Assert.Equal("Second queued folder", completed[1].Name);
        Assert.Same(completed[0], completed[1].Parent);
    }

    private static BookmarkDocument CreateDocument()
    {
        var bookmarkBar = Folder("1", "Bookmarks bar");
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

    private static BookmarkFolder Folder(string id, string name) =>
        new(
            id,
            GuidFor(int.Parse(id)),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties,
            Array.Empty<BookmarkNode>());

    private static Guid GuidFor(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        bytes[7] = 0x40;
        bytes[8] = 0x80;
        return new Guid(bytes);
    }

    private sealed class StubReader(BookmarkDocument document)
        : IChromeBookmarksReader
    {
        public Task<BookmarkDocument> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(document);
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
            Task.FromResult<IReadOnlyList<BookmarkNode>>(
                Array.Empty<BookmarkNode>());
    }

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
