using System.Text.Json;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Clipboard;
using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Clipboard;

public sealed class MainViewModelClipboardTests
{
    [Fact]
    public async Task CopyPaste_UndoRedoRestoresExactClonesCountsAndCleanState()
    {
        var fixture = CreateFixture();
        var store = new MemoryClipboardStore();
        var viewModel = CreateViewModel(
            fixture.Document,
            store);
        await viewModel.LoadBookmarksAsync(
            @"C:\Synthetic\Bookmarks");

        viewModel.UpdateSelectedContentItems(
            new[]
            {
                viewModel.CurrentItems[0],
                viewModel.CurrentItems[1]
            });

        Assert.True(viewModel.CanCopySelectedContentItems);
        Assert.True(viewModel.CopySelectedContentItems());
        Assert.True(store.HasPayload);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.IsDirty);

        viewModel.SelectFolder(viewModel.FolderRoots[1]);
        Assert.Same(fixture.Other, viewModel.SelectedFolder);
        Assert.True(viewModel.CanPasteClipboard);

        Assert.True(
            await viewModel.PasteClipboardIntoSelectedFolderAsync());

        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.Equal(6, fixture.Document.UrlCount);
        Assert.Equal(5, fixture.Document.FolderCount);
        Assert.Equal(2, fixture.Other.Children.Count);
        Assert.Same(fixture.BookmarkBar, fixture.BarFirst.Parent);
        Assert.Same(fixture.BookmarkBar, fixture.Project.Parent);

        var firstClone = fixture.Other.Children[0];
        var projectClone = fixture.Other.Children[1];
        Assert.NotSame(fixture.BarFirst, firstClone);
        Assert.NotSame(fixture.Project, projectClone);
        Assert.Equal("Paste 2 items", viewModel.UndoDescription);

        Assert.True(await viewModel.UndoAsync());

        Assert.Empty(fixture.Other.Children);
        Assert.Equal(4, fixture.Document.UrlCount);
        Assert.Equal(4, fixture.Document.FolderCount);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.True(store.HasPayload);

        Assert.True(await viewModel.RedoAsync());

        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.Same(firstClone, fixture.Other.Children[0]);
        Assert.Same(projectClone, fixture.Other.Children[1]);
        Assert.Equal(6, fixture.Document.UrlCount);
        Assert.Equal(5, fixture.Document.FolderCount);
    }

    [Fact]
    public async Task CutPaste_MovesOriginalsOnlyOnPasteAndUndoRedoPreservesIdentity()
    {
        var fixture = CreateFixture();
        var store = new MemoryClipboardStore();
        var viewModel = CreateViewModel(
            fixture.Document,
            store);
        await viewModel.LoadBookmarksAsync(
            @"C:\Synthetic\Bookmarks");

        viewModel.UpdateSelectedContentItems(
            new[]
            {
                viewModel.CurrentItems[1],
                viewModel.CurrentItems[2]
            });

        Assert.True(viewModel.CanCutSelectedContentItems);
        Assert.True(viewModel.CutSelectedContentItems());

        Assert.True(store.HasPayload);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.Project,
                fixture.BarLast
            },
            fixture.BookmarkBar.Children);
        Assert.Empty(fixture.Other.Children);

        viewModel.SelectFolder(viewModel.FolderRoots[1]);

        Assert.True(
            await viewModel.PasteClipboardIntoSelectedFolderAsync());

        Assert.False(store.HasPayload);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.Equal(
            new BookmarkNode[] { fixture.BarFirst },
            fixture.BookmarkBar.Children);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.Project,
                fixture.BarLast
            },
            fixture.Other.Children);
        Assert.Same(fixture.Project, fixture.Other.Children[0]);
        Assert.Same(fixture.BarLast, fixture.Other.Children[1]);
        Assert.Equal("Paste 2 items", viewModel.UndoDescription);

        Assert.True(await viewModel.UndoAsync());

        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.Project,
                fixture.BarLast
            },
            fixture.BookmarkBar.Children);
        Assert.Empty(fixture.Other.Children);
        Assert.Same(fixture.Project, fixture.BookmarkBar.Children[1]);
        Assert.Same(fixture.BarLast, fixture.BookmarkBar.Children[2]);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);

        Assert.True(await viewModel.RedoAsync());

        Assert.Equal(
            new BookmarkNode[] { fixture.BarFirst },
            fixture.BookmarkBar.Children);
        Assert.Same(fixture.Project, fixture.Other.Children[0]);
        Assert.Same(fixture.BarLast, fixture.Other.Children[1]);
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
    }

    [Fact]
    public async Task CutPaste_DescendantFailureKeepsClipboardGraphAndCleanState()
    {
        var fixture = CreateFixture();
        var store = new MemoryClipboardStore();
        var viewModel = CreateViewModel(
            fixture.Document,
            store);
        await viewModel.LoadBookmarksAsync(
            @"C:\Synthetic\Bookmarks");

        viewModel.UpdateSelectedContentItems(
            new[] { viewModel.CurrentItems[1] });
        Assert.True(viewModel.CutSelectedContentItems());

        Assert.True(
            viewModel.NavigateToFolder(
                fixture.Deep));
        Assert.Same(fixture.Deep, viewModel.SelectedFolder);

        var beforeBar =
            fixture.BookmarkBar.Children.ToArray();
        var beforeProject =
            fixture.Project.Children.ToArray();

        var exception =
            await Assert.ThrowsAsync<BookmarkMoveException>(
                () => viewModel
                    .PasteClipboardIntoSelectedFolderAsync());

        Assert.Equal(
            BookmarkMoveError.DescendantTarget,
            exception.Error);
        Assert.True(store.HasPayload);
        Assert.Equal(beforeBar, fixture.BookmarkBar.Children);
        Assert.Equal(beforeProject, fixture.Project.Children);
        Assert.Same(fixture.BookmarkBar, fixture.Project.Parent);
        Assert.Same(fixture.Project, fixture.Deep.Parent);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.CanUndo);
    }

    private static MainViewModel CreateViewModel(
        BookmarkDocument document,
        IBookmarkClipboardStore store) =>
        new(
            new DelegateReader(
                (_, _) => Task.FromResult(document)),
            new BookmarkSearchService(),
            new BookmarkClipboardService(),
            store,
            TimeSpan.Zero);

    private static Fixture CreateFixture()
    {
        var barFirst = Url(
            "10",
            "First",
            "https://example.com/first");
        var nested = Url(
            "12",
            "Nested",
            "https://example.com/nested");
        var deepUrl = Url(
            "14",
            "Deep URL",
            "https://example.com/deep");
        var deep = Folder(
            "13",
            "Deep",
            deepUrl);
        var project = Folder(
            "20",
            "Project",
            nested,
            deep);
        var barLast = Url(
            "11",
            "Last",
            "https://example.com/last");
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            barFirst,
            project,
            barLast);
        var other = Folder(
            "2",
            "Other bookmarks");
        var synced = Folder(
            "3",
            "Mobile bookmarks");

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
            project,
            deep,
            barFirst,
            barLast);
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

    private sealed class MemoryClipboardStore
        : IBookmarkClipboardStore
    {
        private BookmarkClipboardPayload? _payload;

        public bool HasPayload => _payload is not null;

        public BookmarkClipboardPayload? GetPayload() =>
            _payload;

        public void SetPayload(
            BookmarkClipboardPayload payload)
        {
            _payload = payload
                ?? throw new ArgumentNullException(
                    nameof(payload));
        }

        public void Clear() =>
            _payload = null;
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

    private sealed record Fixture(
        BookmarkDocument Document,
        BookmarkFolder BookmarkBar,
        BookmarkFolder Other,
        BookmarkFolder Project,
        BookmarkFolder Deep,
        BookmarkUrl BarFirst,
        BookmarkUrl BarLast);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
