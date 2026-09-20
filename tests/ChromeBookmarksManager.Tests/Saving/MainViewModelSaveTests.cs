using System.Text.Json;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Saving;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.Infrastructure.Persistence;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Saving;

public sealed class MainViewModelSaveTests
{
    [Fact]
    public async Task Load_WithPersistence_CapturesAndVerifiesAcceptedSourceBaseline()
    {
        var fixture = CreateFixture();
        var baseline = CreateBaseline();
        var baselineService = new RecordingBaselineService(baseline);
        var saveService = new ControlledSaveService();
        var viewModel = CreateViewModel(
            fixture.Document,
            baselineService,
            saveService);

        await viewModel.LoadBookmarksAsync(baseline.FullPath);

        Assert.Equal(1, baselineService.CaptureCount);
        Assert.Equal(1, baselineService.VerifyCount);
        Assert.Equal(baseline, viewModel.SourceBaseline);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.CanSave);
    }

    [Fact]
    public async Task SaveAsync_TransitionsSavingThenMarksCurrentHistoryPositionClean()
    {
        var fixture = CreateFixture();
        var baseline = CreateBaseline();
        var finalBaseline = baseline with
        {
            Sha256 = new string('b', 64),
            Length = baseline.Length + 10,
            LastWriteTimeUtc = baseline.LastWriteTimeUtc.AddSeconds(1)
        };
        var baselineService = new RecordingBaselineService(baseline);
        var saveService = new ControlledSaveService();
        var viewModel = CreateViewModel(
            fixture.Document,
            baselineService,
            saveService);

        await viewModel.LoadBookmarksAsync(baseline.FullPath);
        await viewModel.AddBookmarkAsync(
            "Saved",
            "https://example.com/saved");

        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.True(viewModel.CanSave);
        Assert.True(viewModel.CanUndo);

        var saveTask = viewModel.SaveAsync();

        Assert.Equal(DocumentState.Saving, viewModel.State);
        Assert.False(viewModel.CanSave);
        Assert.False(viewModel.CanBrowseDocument);

        saveService.Complete(
            new ChromeBookmarksSaveResult(
                baseline.FullPath + ".ChromeBookmarksManager.test.bak",
                finalBaseline));

        var result = await saveTask;

        Assert.Equal(finalBaseline, viewModel.SourceBaseline);
        Assert.Equal(finalBaseline, result.FinalBaseline);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.IsDirty);
        Assert.True(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.False(viewModel.CanSave);

        Assert.True(await viewModel.UndoAsync());
        Assert.Equal(DocumentState.LoadedDirty, viewModel.State);
        Assert.True(viewModel.CanSave);
        Assert.True(viewModel.CanRedo);

        Assert.True(await viewModel.RedoAsync());
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.CanSave);
    }

    [Fact]
    public async Task SaveAsync_Success_PreservesSelectionSearchAndBrowserProjections()
    {
        var fixture = CreateFixture();
        var baseline = CreateBaseline();
        var saveService = new ControlledSaveService
        {
            ImmediateResult = new ChromeBookmarksSaveResult(
                baseline.FullPath + ".bak",
                baseline with { Sha256 = new string('c', 64) })
        };
        var viewModel = CreateViewModel(
            fixture.Document,
            new RecordingBaselineService(baseline),
            saveService);

        await viewModel.LoadBookmarksAsync(baseline.FullPath);
        viewModel.SelectedBookmark = fixture.First;
        await viewModel.RenameSelectedBookmarkAsync("First saved");
        viewModel.SearchText = "first.example";
        await viewModel.WaitForPendingSearchAsync();
        var selectedFolder = viewModel.SelectedFolder;
        var selectedBookmark = viewModel.SelectedBookmark;
        var roots = viewModel.FolderRoots;

        await viewModel.SaveAsync();

        Assert.Same(selectedFolder, viewModel.SelectedFolder);
        Assert.Same(selectedBookmark, viewModel.SelectedBookmark);
        Assert.Same(roots, viewModel.FolderRoots);
        Assert.True(viewModel.IsSearchActive);
        Assert.Contains(
            viewModel.SearchResults,
            bookmark => ReferenceEquals(bookmark, fixture.First));
    }

    [Fact]
    public async Task SaveAsync_Failure_LeavesDocumentDirtyBrowseableAndRetryable()
    {
        var fixture = CreateFixture();
        var baseline = CreateBaseline();
        var saveService = new ControlledSaveService
        {
            ImmediateException = new ChromeBookmarksSaveException(
                ChromeBookmarksSaveError.SourceChangedExternally,
                "Synthetic external change.")
        };
        var viewModel = CreateViewModel(
            fixture.Document,
            new RecordingBaselineService(baseline),
            saveService);

        await viewModel.LoadBookmarksAsync(baseline.FullPath);
        await viewModel.AddFolderAsync("Unsaved");
        var document = viewModel.Document;

        var error = await Assert.ThrowsAsync<ChromeBookmarksSaveException>(
            () => viewModel.SaveAsync());

        Assert.Equal(
            ChromeBookmarksSaveError.SourceChangedExternally,
            error.Error);
        Assert.Same(document, viewModel.Document);
        Assert.Equal(DocumentState.SaveFailed, viewModel.State);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.CanBrowseDocument);
        Assert.True(viewModel.CanSave);
        Assert.True(viewModel.CanUndo);

        saveService.ImmediateException = null;
        saveService.ImmediateResult = new ChromeBookmarksSaveResult(
            baseline.FullPath + ".bak",
            baseline with { Sha256 = new string('d', 64) });

        await viewModel.SaveAsync();

        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task ReloadAfterFailedSave_ClearsOldBaselineAndHistory()
    {
        var first = CreateFixture();
        var second = CreateFixture("Second bar");
        var firstBaseline = CreateBaseline();
        var secondBaseline = firstBaseline with
        {
            FullPath = Path.GetFullPath(
                Path.Combine(
                    Path.GetTempPath(),
                    "synthetic-2",
                    "Bookmarks")),
            Sha256 = new string('e', 64)
        };
        var call = 0;
        var reader = new DelegateReader((_, _) =>
        {
            call++;
            return Task.FromResult(
                call == 1 ? first.Document : second.Document);
        });
        var baselineService = new SequencedBaselineService(
            firstBaseline,
            secondBaseline);
        var saveService = new ControlledSaveService
        {
            ImmediateException = new ChromeBookmarksSaveException(
                ChromeBookmarksSaveError.ProcessCheckFailed,
                "Synthetic safety failure.")
        };
        var viewModel = new MainViewModel(
            reader,
            new BookmarkSearchService(),
            baselineService,
            saveService,
            TimeSpan.Zero);

        await viewModel.LoadBookmarksAsync(firstBaseline.FullPath);
        await viewModel.AddFolderAsync("Dirty");
        await Assert.ThrowsAsync<ChromeBookmarksSaveException>(
            () => viewModel.SaveAsync());

        await viewModel.LoadBookmarksAsync(
            secondBaseline.FullPath,
            discardDirtyChanges: true);

        Assert.Same(second.Document, viewModel.Document);
        Assert.Equal(secondBaseline, viewModel.SourceBaseline);
        Assert.Equal(DocumentState.LoadedClean, viewModel.State);
        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.False(viewModel.CanSave);
    }

    private static MainViewModel CreateViewModel(
        BookmarkDocument document,
        IBookmarkSourceBaselineService baselineService,
        IChromeBookmarksSaveService saveService) =>
        new(
            new DelegateReader((_, _) => Task.FromResult(document)),
            new BookmarkSearchService(),
            baselineService,
            saveService,
            TimeSpan.Zero);

    private static BookmarkSourceBaseline CreateBaseline() =>
        new(
            Path.GetFullPath(
                Path.Combine(
                    Path.GetTempPath(),
                    "synthetic",
                    "Bookmarks")),
            new string('a', 64),
            100,
            new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc));

    private static Fixture CreateFixture(
        string bookmarkBarName = "Bookmarks bar")
    {
        var first = Url(
            "10",
            "First",
            "https://first.example/path");
        var bookmarkBar = Folder(
            "1",
            bookmarkBarName,
            first);
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

        return new Fixture(document, bookmarkBar, first);
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

    private sealed class RecordingBaselineService(
        BookmarkSourceBaseline baseline) :
        IBookmarkSourceBaselineService
    {
        public int CaptureCount { get; private set; }

        public int VerifyCount { get; private set; }

        public Task<BookmarkSourceBaseline> CaptureAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            CaptureCount++;
            return Task.FromResult(baseline);
        }

        public Task<BookmarkSourceBaseline> VerifyUnchangedAsync(
            BookmarkSourceBaseline expected,
            CancellationToken cancellationToken = default)
        {
            VerifyCount++;
            return Task.FromResult(expected);
        }
    }

    private sealed class SequencedBaselineService(
        params BookmarkSourceBaseline[] baselines) :
        IBookmarkSourceBaselineService
    {
        private readonly Queue<BookmarkSourceBaseline> _baselines =
            new(baselines);
        private BookmarkSourceBaseline? _active;

        public Task<BookmarkSourceBaseline> CaptureAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            _active = _baselines.Dequeue();
            return Task.FromResult(_active);
        }

        public Task<BookmarkSourceBaseline> VerifyUnchangedAsync(
            BookmarkSourceBaseline expected,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_active ?? expected);
    }

    private sealed class ControlledSaveService :
        IChromeBookmarksSaveService
    {
        private TaskCompletionSource<ChromeBookmarksSaveResult>? _pending;

        public ChromeBookmarksSaveResult? ImmediateResult { get; set; }

        public ChromeBookmarksSaveException? ImmediateException { get; set; }

        public Task<ChromeBookmarksSaveResult> SaveAsync(
            BookmarkDocument? document,
            BookmarkSourceBaseline? sourceBaseline,
            CancellationToken cancellationToken = default)
        {
            if (ImmediateException is not null)
            {
                return Task.FromException<ChromeBookmarksSaveResult>(
                    ImmediateException);
            }

            if (ImmediateResult is not null)
            {
                return Task.FromResult(ImmediateResult);
            }

            _pending = new TaskCompletionSource<ChromeBookmarksSaveResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            return _pending.Task;
        }

        public void Complete(ChromeBookmarksSaveResult result) =>
            (_pending ?? throw new InvalidOperationException(
                "No pending save exists."))
            .SetResult(result);
    }

    private sealed record Fixture(
        BookmarkDocument Document,
        BookmarkFolder BookmarkBar,
        BookmarkUrl First);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties =
            new Dictionary<string, JsonElement>();
}
