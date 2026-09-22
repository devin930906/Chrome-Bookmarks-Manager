using System.Text.Json;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Sorting;

public sealed class MainViewModelSortTests
{
    [Fact]
    public async Task SortSelectedFolderByName_UndoRedoRestoresExactOrderAndDirtyState()
    {
        var fixture = CreateFixture();
        var search = new CountingSearchService();
        var viewModel = new MainViewModel(
            new DelegateReader(
                (_, _) => Task.FromResult(fixture.Document)),
            search,
            TimeSpan.Zero);

        await viewModel.LoadBookmarksAsync(
            @"C:\Synthetic\Bookmarks");

        Assert.True(viewModel.CanSortSelectedFolder);
        Assert.Equal(1, search.BuildIndexCalls);

        Assert.True(
            await viewModel.SortSelectedFolderByNameAsync());

        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.FolderAlpha,
                fixture.FolderZulu,
                fixture.BookmarkAlpha,
                fixture.BookmarkZulu
            },
            fixture.BookmarkBar.Children);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.Equal("Sort by name", viewModel.UndoDescription);
        Assert.Equal(1, search.BuildIndexCalls);

        Assert.True(await viewModel.UndoAsync());

        Assert.Equal(
            fixture.OriginalOrder,
            fixture.BookmarkBar.Children);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.Equal(1, search.BuildIndexCalls);

        Assert.True(await viewModel.RedoAsync());

        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.FolderAlpha,
                fixture.FolderZulu,
                fixture.BookmarkAlpha,
                fixture.BookmarkZulu
            },
            fixture.BookmarkBar.Children);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.Equal(1, search.BuildIndexCalls);
    }

    [Fact]
    public async Task SortSelectedFolderByName_AlreadySortedIsNoOpAndDoesNotDirty()
    {
        var folderAlpha = Folder("20", "Alpha");
        var folderZulu = Folder("21", "Zulu");
        var bookmarkAlpha = Url("10", "Alpha");
        var bookmarkZulu = Url("11", "Zulu");
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            folderAlpha,
            folderZulu,
            bookmarkAlpha,
            bookmarkZulu);
        var document = CreateDocument(bookmarkBar);
        var viewModel = new MainViewModel(
            new DelegateReader((_, _) => Task.FromResult(document)),
            new BookmarkSearchService(),
            TimeSpan.Zero);

        await viewModel.LoadBookmarksAsync(
            @"C:\Synthetic\Bookmarks");

        Assert.False(
            await viewModel.SortSelectedFolderByNameAsync());
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.CanUndo);
    }

    private static Fixture CreateFixture()
    {
        var bookmarkZulu = Url("11", "Zulu");
        var folderZulu = Folder("21", "Zulu");
        var bookmarkAlpha = Url("10", "Alpha");
        var folderAlpha = Folder("20", "Alpha");
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            bookmarkZulu,
            folderZulu,
            bookmarkAlpha,
            folderAlpha);
        var originalOrder = bookmarkBar.Children.ToArray();
        var document = CreateDocument(bookmarkBar);

        return new Fixture(
            document,
            bookmarkBar,
            folderAlpha,
            folderZulu,
            bookmarkAlpha,
            bookmarkZulu,
            originalOrder);
    }

    private static BookmarkDocument CreateDocument(
        BookmarkFolder bookmarkBar) =>
        new(
            1,
            null,
            null,
            new BookmarkRoots(
                bookmarkBar,
                Folder("2", "Other bookmarks"),
                Folder("3", "Mobile bookmarks"),
                EmptyProperties),
            EmptyProperties);

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
        string name) =>
        new(
            id,
            GuidFor(int.Parse(id)),
            name,
            $"https://example.com/{id}",
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
            return _inner.BuildIndexAsync(
                document,
                cancellationToken);
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
        BookmarkFolder FolderAlpha,
        BookmarkFolder FolderZulu,
        BookmarkUrl BookmarkAlpha,
        BookmarkUrl BookmarkZulu,
        IReadOnlyList<BookmarkNode> OriginalOrder);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
