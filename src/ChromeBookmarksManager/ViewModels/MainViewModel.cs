using System.Diagnostics;
using System.Globalization;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Application.History;
using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Application.Saving;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.Infrastructure.Persistence;

namespace ChromeBookmarksManager.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private static readonly TimeSpan DefaultSearchDebounce =
        TimeSpan.FromMilliseconds(250);

    private readonly IChromeBookmarksReader _reader;
    private readonly IBookmarkSearchService _searchService;
    private readonly IBookmarkEditingService _editingService;
    private readonly IBookmarkMoveService _moveService;
    private readonly IBookmarkDeleteService _deleteService;
    private readonly IBookmarkSourceBaselineService? _baselineService;
    private readonly IChromeBookmarksSaveService? _saveService;
    private readonly BookmarkHistoryManager _history = new();
    private readonly SemaphoreSlim _documentOperationGate = new(1, 1);
    private readonly TimeSpan _searchDebounce;
    private CancellationTokenSource? _loadCancellation;
    private int _loadRequestPending;
    private CancellationTokenSource? _searchCancellation;
    private Task _pendingSearchTask = Task.CompletedTask;
    private long _searchGeneration;
    private BookmarkDocument? _document;
    private BookmarkSearchIndex? _searchIndex;
    private string? _sourcePath;
    private BookmarkSourceBaseline? _sourceBaseline;
    private DocumentState _state = DocumentState.NoDocument;
    private string _statusText = "No Bookmarks file is open.";
    private IReadOnlyList<FolderTreeItemViewModel> _folderRoots =
        Array.Empty<FolderTreeItemViewModel>();
    private readonly Dictionary<BookmarkFolder, FolderTreeItemViewModel>
        _folderLookup = new();
    private FolderTreeItemViewModel? _selectedFolderItem;
    private BookmarkFolder? _selectedFolder;
    private IReadOnlyList<BookmarkUrl> _currentBookmarks =
        Array.Empty<BookmarkUrl>();
    private BookmarkUrl? _selectedBookmark;
    private IReadOnlyList<BookmarkUrl> _selectedBookmarks =
        Array.Empty<BookmarkUrl>();
    private string _documentSummaryText = string.Empty;
    private string _selectionSummaryText = string.Empty;
    private string _searchText = string.Empty;
    private BookmarkSearchScope _searchScope = BookmarkSearchScope.AllBookmarks;
    private IReadOnlyList<BookmarkUrl> _searchResults =
        Array.Empty<BookmarkUrl>();
    private bool _isSearchBusy;
    private string _searchSummaryText = string.Empty;
    private bool _suppressFolderSearchRefresh;

    public MainViewModel(IChromeBookmarksReader reader)
        : this(
            reader,
            new BookmarkSearchService(),
            new BookmarkEditingService(),
            new BookmarkMoveService(),
            new BookmarkDeleteService(),
            DefaultSearchDebounce)
    {
    }

    internal MainViewModel(
        IChromeBookmarksReader reader,
        IBookmarkSearchService searchService,
        TimeSpan searchDebounce)
        : this(
            reader,
            searchService,
            new BookmarkEditingService(),
            new BookmarkMoveService(),
            new BookmarkDeleteService(),
            searchDebounce)
    {
    }

    internal MainViewModel(
        IChromeBookmarksReader reader,
        IBookmarkSearchService searchService,
        IBookmarkEditingService editingService,
        TimeSpan searchDebounce)
        : this(
            reader,
            searchService,
            editingService,
            new BookmarkMoveService(),
            new BookmarkDeleteService(),
            searchDebounce)
    {
    }

    internal MainViewModel(
        IChromeBookmarksReader reader,
        IBookmarkSearchService searchService,
        IBookmarkEditingService editingService,
        IBookmarkMoveService moveService,
        TimeSpan searchDebounce)
        : this(
            reader,
            searchService,
            editingService,
            moveService,
            new BookmarkDeleteService(),
            searchDebounce)
    {
    }

    internal MainViewModel(
        IChromeBookmarksReader reader,
        IBookmarkSearchService searchService,
        IBookmarkEditingService editingService,
        IBookmarkMoveService moveService,
        IBookmarkDeleteService deleteService,
        TimeSpan searchDebounce)
        : this(
            reader,
            searchService,
            editingService,
            moveService,
            deleteService,
            baselineService: null,
            saveService: null,
            searchDebounce)
    {
    }

    internal MainViewModel(
        IChromeBookmarksReader reader,
        IBookmarkSearchService searchService,
        IBookmarkSourceBaselineService baselineService,
        IChromeBookmarksSaveService saveService,
        TimeSpan searchDebounce)
        : this(
            reader,
            searchService,
            new BookmarkEditingService(),
            new BookmarkMoveService(),
            new BookmarkDeleteService(),
            baselineService,
            saveService,
            searchDebounce)
    {
    }

    internal MainViewModel(
        IChromeBookmarksReader reader,
        IBookmarkSearchService searchService,
        IBookmarkEditingService editingService,
        IBookmarkMoveService moveService,
        IBookmarkDeleteService deleteService,
        IBookmarkSourceBaselineService? baselineService,
        IChromeBookmarksSaveService? saveService,
        TimeSpan searchDebounce)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _searchService = searchService
            ?? throw new ArgumentNullException(nameof(searchService));
        _editingService = editingService
            ?? throw new ArgumentNullException(nameof(editingService));
        _moveService = moveService
            ?? throw new ArgumentNullException(nameof(moveService));
        _deleteService = deleteService
            ?? throw new ArgumentNullException(nameof(deleteService));
        _baselineService = baselineService;
        _saveService = saveService;

        if ((baselineService is null) != (saveService is null))
        {
            throw new ArgumentException(
                "Persistence baseline and save services must be supplied together.");
        }

        if (searchDebounce < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(searchDebounce),
                searchDebounce,
                "Search debounce cannot be negative.");
        }

        _searchDebounce = searchDebounce;
    }

    private static readonly string ApplicationVersion =
        typeof(MainViewModel).Assembly
            .GetName()
            .Version?
            .ToString(3)
        ?? "0.0.0";

    public string ApplicationTitle =>
        $"Chrome Bookmarks Manager {ApplicationVersion}";

    public DocumentState State => _state;

    public BookmarkDocument? Document => _document;

    public string? SourcePath => _sourcePath;

    public BookmarkSourceBaseline? SourceBaseline => _sourceBaseline;

    public string StatusText => _statusText;

    public IReadOnlyList<FolderTreeItemViewModel> FolderRoots => _folderRoots;

    public BookmarkFolder? SelectedFolder => _selectedFolder;

    public IReadOnlyList<BookmarkUrl> CurrentBookmarks => _currentBookmarks;

    public BookmarkUrl? SelectedBookmark
    {
        get => _selectedBookmark;
        set => SetSelectedBookmark(value);
    }

    public IReadOnlyList<BookmarkUrl> SelectedBookmarks =>
        _selectedBookmarks;

    public int SelectedBookmarkCount =>
        _selectedBookmarks.Count;

    public bool HasMultipleSelectedBookmarks =>
        _selectedBookmarks.Count > 1;

    public string DocumentSummaryText => _documentSummaryText;

    public string SelectionSummaryText => _selectionSummaryText;

    public string SearchText
    {
        get => _searchText;
        set => SetSearchText(value ?? string.Empty);
    }

    public BookmarkSearchScope SearchScope
    {
        get => _searchScope;
        set => SetSearchScope(value);
    }

    public IReadOnlyList<BookmarkUrl> SearchResults => _searchResults;

    public IReadOnlyList<BookmarkUrl> DisplayedBookmarks =>
        IsSearchActive ? SearchResults : CurrentBookmarks;

    public bool IsSearchActive =>
        CanSearchDocument && !string.IsNullOrWhiteSpace(SearchText);

    public bool IsSearchBusy => _isSearchBusy;

    public bool CanSearchDocument =>
        CanBrowseDocument && _searchIndex is not null;

    public string SearchSummaryText => _searchSummaryText;

    public bool IsDirty =>
        State is DocumentState.LoadedDirty or DocumentState.SaveFailed;

    public bool CanSave =>
        _saveService is not null &&
        _sourceBaseline is not null &&
        _document is not null &&
        State is DocumentState.LoadedDirty or DocumentState.SaveFailed;

    public bool CanUndo =>
        CanBrowseDocument && _history.CanUndo;

    public bool CanRedo =>
        CanBrowseDocument && _history.CanRedo;

    public string? UndoDescription =>
        CanUndo ? _history.UndoDescription : null;

    public string? RedoDescription =>
        CanRedo ? _history.RedoDescription : null;

    public bool CanAddBookmark =>
        CanBrowseDocument && SelectedFolder is not null;

    public bool CanAddFolder =>
        CanBrowseDocument && SelectedFolder is not null;

    public bool CanRenameSelectedFolder =>
        CanBrowseDocument &&
        SelectedFolder is not null &&
        !IsPermanentRoot(SelectedFolder);

    public bool CanRenameSelectedBookmark =>
        CanBrowseDocument &&
        SelectedBookmark is not null &&
        !HasMultipleSelectedBookmarks;

    public bool CanEditSelectedBookmarkUrl =>
        CanBrowseDocument &&
        SelectedBookmark is not null &&
        !HasMultipleSelectedBookmarks;

    public bool CanMoveSelectedBookmark =>
        CanBrowseDocument &&
        SelectedBookmark is not null &&
        !HasMultipleSelectedBookmarks;

    public bool CanMoveSelectedFolder =>
        CanBrowseDocument &&
        SelectedFolder is not null &&
        !IsPermanentRoot(SelectedFolder);

    public bool CanDeleteSelectedBookmarks =>
        CanBrowseDocument &&
        (_selectedBookmarks.Count > 0 ||
         SelectedBookmark is not null);

    public bool CanDeleteSelectedFolder =>
        CanBrowseDocument &&
        SelectedFolder is not null &&
        !IsPermanentRoot(SelectedFolder);

    public bool CanMoveSelectedBookmarks =>
        CanBrowseDocument &&
        (_selectedBookmarks.Count > 0 ||
         SelectedBookmark is not null);

    public bool CanOpenBookmarks =>
        State is not DocumentState.Loading and not DocumentState.Saving;

    public bool CanCancelLoad => State == DocumentState.Loading;

    public bool CanBrowseDocument =>
        State is DocumentState.LoadedClean or
            DocumentState.LoadedDirty or
            DocumentState.SaveFailed;

    public Task LoadBookmarksAsync(
        string path,
        bool discardDirtyChanges = false)
    {
        if (Interlocked.CompareExchange(
                ref _loadRequestPending,
                1,
                0) != 0)
        {
            return Task.FromException(
                new InvalidOperationException(
                    "A Bookmarks file is already being loaded."));
        }

        return RunReservedLoadAsync(path, discardDirtyChanges);
    }

    private async Task RunReservedLoadAsync(
        string path,
        bool discardDirtyChanges)
    {
        try
        {
            await _documentOperationGate
                .WaitAsync()
                .ConfigureAwait(true);
            try
            {
                await LoadBookmarksCoreAsync(
                        path,
                        discardDirtyChanges)
                    .ConfigureAwait(true);
            }
            finally
            {
                _documentOperationGate.Release();
            }
        }
        finally
        {
            Volatile.Write(ref _loadRequestPending, 0);
        }
    }

    private async Task LoadBookmarksCoreAsync(
        string path,
        bool discardDirtyChanges)
    {
        if (_loadCancellation is not null ||
            State == DocumentState.Loading)
        {
            throw new InvalidOperationException(
                "A Bookmarks file is already being loaded.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (IsDirty &&
            !discardDirtyChanges)
        {
            throw new InvalidOperationException(
                "The current Bookmarks document has unsaved in-memory changes. " +
                "Confirm Discard before loading another file.");
        }

        var previousState = State;
        var hadExistingDocument =
            _document is not null &&
            CanBrowseDocument;

        SetState(DocumentState.Loading);
        SetStatusText("Reading Bookmarks...");

        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        var stopwatch = Stopwatch.StartNew();
        BookmarkSourceBaseline? sourceBaseline = null;

        try
        {
            if (_baselineService is not null)
            {
                SetStatusText("Verifying Bookmarks source...");
                sourceBaseline = await _baselineService
                    .CaptureAsync(path, cancellation.Token)
                    .ConfigureAwait(true);
            }

            SetStatusText("Reading Bookmarks...");
            var document = await _reader
                .ReadFileAsync(path, cancellation.Token)
                .ConfigureAwait(true);

            if (_baselineService is not null &&
                sourceBaseline is not null)
            {
                await _baselineService
                    .VerifyUnchangedAsync(
                        sourceBaseline,
                        cancellation.Token)
                    .ConfigureAwait(true);
            }

            SetStatusText("Building search index...");

            var searchIndex = await _searchService
                .BuildIndexAsync(document, cancellation.Token)
                .ConfigureAwait(true);

            cancellation.Token.ThrowIfCancellationRequested();

            stopwatch.Stop();

            ResetSearchState(
                clearIndex: true,
                resetScope: true);
            ClearHistory();
            SetSearchIndex(searchIndex);
            SetDocument(document);
            SetSourcePath(path);
            SetSourceBaseline(sourceBaseline);
            BuildBrowserState(document);
            SetState(DocumentState.LoadedClean);
            SetStatusText(
                $"Loaded {document.UrlCount:N0} URLs and " +
                $"{document.FolderCount:N0} folders in " +
                $"{stopwatch.Elapsed.TotalSeconds:F1}s.");
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            stopwatch.Stop();

            if (hadExistingDocument)
            {
                SetState(previousState);
                SetStatusText(
                    "Replacement loading was canceled. " +
                    "The current document was kept unchanged.");
            }
            else
            {
                ClearHistory();
                ResetSearchState(
                    clearIndex: true,
                    resetScope: true);
                SetDocument(null);
                SetSourcePath(null);
                SetSourceBaseline(null);
                ClearBrowserState();
                SetState(DocumentState.NoDocument);
                SetStatusText("Loading was canceled.");
            }
        }
        catch (ChromeBookmarksReadException exception)
        {
            stopwatch.Stop();

            if (hadExistingDocument)
            {
                SetState(previousState);
                SetStatusText(
                    exception.Message +
                    " The current document was kept unchanged.");
            }
            else
            {
                ClearHistory();
                ResetSearchState(
                    clearIndex: true,
                    resetScope: true);
                SetDocument(null);
                SetSourcePath(null);
                SetSourceBaseline(null);
                ClearBrowserState();
                SetState(DocumentState.LoadFailed);
                SetStatusText(exception.Message);
            }
        }
        catch (BookmarkSourceBaselineException exception)
        {
            stopwatch.Stop();

            if (hadExistingDocument)
            {
                SetState(previousState);
                SetStatusText(
                    exception.Message +
                    " The current document was kept unchanged.");
            }
            else
            {
                ClearHistory();
                ResetSearchState(
                    clearIndex: true,
                    resetScope: true);
                SetDocument(null);
                SetSourcePath(null);
                SetSourceBaseline(null);
                ClearBrowserState();
                SetState(DocumentState.LoadFailed);
                SetStatusText(exception.Message);
            }
        }
        catch (Exception)
        {
            stopwatch.Stop();

            if (hadExistingDocument)
            {
                SetState(previousState);
                SetStatusText(
                    "Replacement loading failed while preparing search. " +
                    "The current document was kept unchanged.");
            }
            else
            {
                ClearHistory();
                ResetSearchState(
                    clearIndex: true,
                    resetScope: true);
                SetDocument(null);
                SetSourcePath(null);
                SetSourceBaseline(null);
                ClearBrowserState();
                SetState(DocumentState.LoadFailed);
                SetStatusText(
                    "Loading failed while preparing search.");
            }
        }
        finally
        {
            if (ReferenceEquals(
                    _loadCancellation,
                    cancellation))
            {
                _loadCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    public async Task<ChromeBookmarksSaveResult> SaveAsync(
        CancellationToken cancellationToken = default)
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            if (_saveService is null ||
                _document is null ||
                _sourceBaseline is null)
            {
                throw new InvalidOperationException(
                    "A persistence-enabled loaded Bookmarks document is required before saving.");
            }

            if (!CanSave)
            {
                throw new InvalidOperationException(
                    "The active Bookmarks document has no unsaved changes that can be saved.");
            }

            var document = _document;
            var baseline = _sourceBaseline;

            SetState(DocumentState.Saving);
            SetStatusText("Saving Bookmarks safely...");

            try
            {
                var result = await _saveService
                    .SaveAsync(document, baseline, cancellationToken)
                    .ConfigureAwait(true);

                if (!ReferenceEquals(_document, document))
                {
                    throw new InvalidOperationException(
                        "The active Bookmarks document changed while saving.");
                }

                SetSourceBaseline(result.FinalBaseline);
                _history.MarkClean();
                SetState(DocumentState.LoadedClean);
                SetStatusText(
                    $"Saved and verified. Verified safety backup: {result.BackupPath}");
                NotifyHistoryAvailabilityChanged();

                return result;
            }
            catch (ChromeBookmarksSaveException exception)
            {
                SetState(DocumentState.SaveFailed);
                SetStatusText(exception.Message);
                throw;
            }
            catch (OperationCanceledException)
            {
                SetState(DocumentState.SaveFailed);
                SetStatusText("Saving was canceled before replacement.");
                throw;
            }
            catch (Exception)
            {
                SetState(DocumentState.SaveFailed);
                SetStatusText(
                    "Saving failed unexpectedly. The document remains unsaved and retryable.");
                throw;
            }
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public void CancelLoad()
    {
        _loadCancellation?.Cancel();
    }

    public async Task<BookmarkUrl> AddBookmarkAsync(
        string name,
        string url)
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();
            var parent = SelectedFolder
                ?? throw new InvalidOperationException(
                    "Select a folder before adding a bookmark.");

            var insertionIndex = parent.Children.Count;
            var bookmark = _editingService.AddBookmark(
                document,
                parent,
                name,
                url);

            RecordHistory(
                new BookmarkAddHistoryEntry(
                    bookmark,
                    parent,
                    insertionIndex));
            await RefreshProjectionsAfterEditAsync(
                    preferredFolder: parent,
                    preferredBookmark: bookmark)
                .ConfigureAwait(true);

            return bookmark;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<BookmarkFolder> AddFolderAsync(string name)
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();
            var parent = SelectedFolder
                ?? throw new InvalidOperationException(
                    "Select a folder before adding a folder.");

            var insertionIndex = parent.Children.Count;
            var folder = _editingService.AddFolder(
                document,
                parent,
                name);

            RecordHistory(
                new BookmarkAddHistoryEntry(
                    folder,
                    parent,
                    insertionIndex));
            await RefreshProjectionsAfterEditAsync(
                    preferredFolder: folder)
                .ConfigureAwait(true);

            return folder;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<bool> RenameSelectedFolderAsync(string newName)
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();
            var folder = SelectedFolder
                ?? throw new InvalidOperationException(
                    "Select a folder before renaming it.");

            if (!CanRenameSelectedFolder)
            {
                throw new InvalidOperationException(
                    "The selected Chrome root folder cannot be renamed.");
            }

            var oldName = folder.Name;
            var changed = _editingService.RenameNode(
                document,
                folder,
                newName);

            if (!changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkRenameHistoryEntry(
                    folder,
                    oldName,
                    folder.Name));
            await RefreshProjectionsAfterEditAsync(
                    preferredFolder: folder)
                .ConfigureAwait(true);

            return true;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<bool> RenameSelectedBookmarkAsync(string newName)
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();
            var bookmark = SelectedBookmark
                ?? throw new InvalidOperationException(
                    "Select a bookmark before renaming it.");

            var oldName = bookmark.Name;
            var changed = _editingService.RenameNode(
                document,
                bookmark,
                newName);

            if (!changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkRenameHistoryEntry(
                    bookmark,
                    oldName,
                    bookmark.Name));
            await RefreshProjectionsAfterEditAsync(
                    preferredBookmark: bookmark)
                .ConfigureAwait(true);

            return true;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<bool> EditSelectedBookmarkUrlAsync(string newUrl)
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();
            var bookmark = SelectedBookmark
                ?? throw new InvalidOperationException(
                    "Select a bookmark before editing its URL.");

            var oldUrl = bookmark.Url;
            var changed = _editingService.EditUrl(
                document,
                bookmark,
                newUrl);

            if (!changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkUrlEditHistoryEntry(
                    bookmark,
                    oldUrl,
                    bookmark.Url));
            await RefreshProjectionsAfterEditAsync(
                    preferredBookmark: bookmark)
                .ConfigureAwait(true);

            return true;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<bool> UndoAsync()
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();
            var preferredFolder = SelectedFolder;
            var preferredBookmark = SelectedBookmark;

            var result = _history.Undo(document);
            if (!result.Changed)
            {
                return false;
            }

            SyncDocumentStateFromHistory();

            await RefreshAfterHistoryAsync(
                    result,
                    preferredFolder,
                    preferredBookmark)
                .ConfigureAwait(true);

            return true;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<bool> RedoAsync()
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();
            var preferredFolder = SelectedFolder;
            var preferredBookmark = SelectedBookmark;

            var result = _history.Redo(document);
            if (!result.Changed)
            {
                return false;
            }

            SyncDocumentStateFromHistory();

            await RefreshAfterHistoryAsync(
                    result,
                    preferredFolder,
                    preferredBookmark)
                .ConfigureAwait(true);

            return true;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public void UpdateSelectedBookmarks(
        IEnumerable<BookmarkUrl> bookmarks)
    {
        ArgumentNullException.ThrowIfNull(bookmarks);

        var requested = new HashSet<BookmarkUrl>(
            ReferenceEqualityComparer.Instance);

        foreach (var bookmark in bookmarks)
        {
            ArgumentNullException.ThrowIfNull(bookmark);
            requested.Add(bookmark);
        }

        var ordered = DisplayedBookmarks
            .Where(requested.Contains)
            .ToArray();

        SetSelectedBookmarks(ordered);

        var primary =
            _selectedBookmark is not null &&
            ordered.Any(
                bookmark => ReferenceEquals(
                    bookmark,
                    _selectedBookmark))
                ? _selectedBookmark
                : ordered.FirstOrDefault();

        SetSelectedBookmark(primary);
    }

    public async Task<bool> DeleteSelectedBookmarksAsync()
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();
            var selected = GetEffectiveSelectedBookmarks();

            if (selected.Count == 0)
            {
                return false;
            }

            var preferredFolder =
                SelectedFolder ??
                document.Roots.BookmarkBar;

            var result = _deleteService.DeleteBookmarks(
                document,
                selected);

            if (!result.Changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkBatchDeleteHistoryEntry(result));
            ClearBookmarkSelection();

            await RefreshProjectionsAfterEditAsync(
                    preferredFolder: preferredFolder)
                .ConfigureAwait(true);

            return true;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<bool> DeleteSelectedFolderAsync()
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();
            var folder = SelectedFolder
                ?? throw new InvalidOperationException(
                    "Select a folder before deleting it.");

            if (!CanDeleteSelectedFolder)
            {
                throw new InvalidOperationException(
                    "Permanent Chrome root folders cannot be deleted.");
            }

            var result = _deleteService.DeleteNode(
                document,
                folder);

            RecordHistory(
                new BookmarkDeleteHistoryEntry(result));
            ClearBookmarkSelection();

            await RefreshProjectionsAfterEditAsync(
                    preferredFolder: result.SourceParent)
                .ConfigureAwait(true);

            return true;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<bool> MoveSelectedBookmarksToEndAsync(
        BookmarkFolder targetParent)
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            ArgumentNullException.ThrowIfNull(targetParent);

            var document = RequireEditableDocument();
            var selected = GetEffectiveSelectedBookmarks();

            if (selected.Count == 0)
            {
                return false;
            }

            var searchWasActive = IsSearchActive;
            var selectedFolderBeforeMove = SelectedFolder;

            var result = _moveService.MoveBookmarksToEnd(
                document,
                selected,
                targetParent);

            if (!result.Changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkBatchMoveHistoryEntry(
                    selected,
                    result));
            ClearBookmarkSelection();

            await RefreshProjectionsAfterMoveAsync(
                    preferredFolder: searchWasActive
                        ? selectedFolderBeforeMove
                        : targetParent)
                .ConfigureAwait(true);

            return true;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<bool> MoveBookmarkToEndAsync(
        BookmarkUrl bookmark,
        BookmarkFolder targetParent)
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            ArgumentNullException.ThrowIfNull(bookmark);
            ArgumentNullException.ThrowIfNull(targetParent);

            var document = RequireEditableDocument();
            var searchWasActive = IsSearchActive;
            var selectedFolderBeforeMove = SelectedFolder;
            var result = _moveService.MoveToEnd(
                document,
                bookmark,
                targetParent);

            if (!result.Changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkMoveHistoryEntry(
                    bookmark,
                    result));
            await RefreshProjectionsAfterMoveAsync(
                    preferredFolder: searchWasActive
                        ? selectedFolderBeforeMove
                        : result.TargetParent,
                    preferredBookmark: bookmark)
                .ConfigureAwait(true);

            return true;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<bool> MoveFolderToEndAsync(
        BookmarkFolder folder,
        BookmarkFolder targetParent)
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            ArgumentNullException.ThrowIfNull(folder);
            ArgumentNullException.ThrowIfNull(targetParent);

            var document = RequireEditableDocument();
            var result = _moveService.MoveToEnd(
                document,
                folder,
                targetParent);

            if (!result.Changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkMoveHistoryEntry(
                    folder,
                    result));
            await RefreshProjectionsAfterMoveAsync(
                    preferredFolder: folder)
                .ConfigureAwait(true);

            return true;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<bool> MoveBookmarkBeforeAsync(
        BookmarkUrl bookmark,
        BookmarkUrl target)
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            EnsurePositionalBookmarkMoveAllowed();

            var document = RequireEditableDocument();
            var result = _moveService.MoveBookmarkBefore(
                document,
                bookmark,
                target);

            if (!result.Changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkMoveHistoryEntry(
                    bookmark,
                    result));
            await RefreshProjectionsAfterMoveAsync(
                    preferredFolder: result.TargetParent,
                    preferredBookmark: bookmark)
                .ConfigureAwait(true);

            return true;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<bool> MoveBookmarkAfterAsync(
        BookmarkUrl bookmark,
        BookmarkUrl target)
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            EnsurePositionalBookmarkMoveAllowed();

            var document = RequireEditableDocument();
            var result = _moveService.MoveBookmarkAfter(
                document,
                bookmark,
                target);

            if (!result.Changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkMoveHistoryEntry(
                    bookmark,
                    result));
            await RefreshProjectionsAfterMoveAsync(
                    preferredFolder: result.TargetParent,
                    preferredBookmark: bookmark)
                .ConfigureAwait(true);

            return true;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<bool> MoveFolderBeforeAsync(
        BookmarkFolder folder,
        BookmarkFolder target)
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();
            var result = _moveService.MoveFolderBefore(
                document,
                folder,
                target);

            if (!result.Changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkMoveHistoryEntry(
                    folder,
                    result));
            await RefreshProjectionsAfterMoveAsync(
                    preferredFolder: folder)
                .ConfigureAwait(true);

            return true;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<bool> MoveFolderAfterAsync(
        BookmarkFolder folder,
        BookmarkFolder target)
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();
            var result = _moveService.MoveFolderAfter(
                document,
                folder,
                target);

            if (!result.Changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkMoveHistoryEntry(
                    folder,
                    result));
            await RefreshProjectionsAfterMoveAsync(
                    preferredFolder: folder)
                .ConfigureAwait(true);

            return true;
        
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public void NavigateToSearchResult(BookmarkUrl? bookmark)
    {
        if (!IsSearchActive ||
            bookmark is null ||
            !SearchResults.Any(result => ReferenceEquals(result, bookmark)) ||
            bookmark.Parent is null ||
            !_folderLookup.TryGetValue(bookmark.Parent, out var targetFolder))
        {
            return;
        }

        var ancestor = targetFolder.Parent;
        while (ancestor is not null)
        {
            ancestor.IsExpanded = true;
            ancestor = ancestor.Parent;
        }

        SelectFolder(targetFolder);
        SearchText = string.Empty;
        SetSelectedBookmark(bookmark);
    }

    public void SelectFolder(FolderTreeItemViewModel? item)
    {
        if (ReferenceEquals(_selectedFolderItem, item))
        {
            if (item is not null && !item.IsSelected)
            {
                item.IsSelected = true;
            }

            return;
        }

        if (_selectedFolderItem is not null)
        {
            _selectedFolderItem.IsSelected = false;
        }

        ClearBookmarkSelection();
        _selectedFolderItem = item;

        if (item is null)
        {
            SetSelectedFolder(null);
            SetSelectedBookmark(null);
            SetCurrentBookmarks(Array.Empty<BookmarkUrl>());
            SetSelectionSummaryText(string.Empty);
            RerunSearchForFolderChange();
            return;
        }

        item.IsSelected = true;
        SetSelectedFolder(item.Folder);
        SetSelectedBookmark(null);

        var bookmarks = item.Folder.Children
            .OfType<BookmarkUrl>()
            .ToArray();

        SetCurrentBookmarks(bookmarks);
        SetSelectionSummaryText(
            $"{item.Folder.Name} | " +
            $"{bookmarks.Length.ToString("N0", CultureInfo.InvariantCulture)} bookmarks");

        RerunSearchForFolderChange();
    }

    internal Task WaitForPendingSearchAsync() => _pendingSearchTask;

    internal async Task RefreshProjectionsAfterEditAsync(
        BookmarkFolder? preferredFolder = null,
        BookmarkUrl? preferredBookmark = null,
        CancellationToken cancellationToken = default)
    {
        if (_document is null || !CanBrowseDocument)
        {
            throw new InvalidOperationException(
                "An editable bookmark document must be loaded before projections can be refreshed.");
        }

        var document = _document;
        var selectedFolder =
            preferredFolder ??
            _selectedFolder ??
            document.Roots.BookmarkBar;
        var selectedBookmark =
            preferredBookmark ??
            _selectedBookmark;
        var expandedFolders = _folderLookup
            .Where(pair => pair.Value.IsExpanded)
            .Select(pair => pair.Key)
            .ToArray();
        var searchWasActive = IsSearchActive;

        ++_searchGeneration;
        CancelPendingSearch();
        _pendingSearchTask = Task.CompletedTask;
        SetSearchResults(Array.Empty<BookmarkUrl>());
        SetIsSearchBusy(false);
        SetSearchSummaryText(
            searchWasActive ? "Refreshing search index..." : string.Empty);

        var refreshedIndex = await _searchService
            .BuildIndexAsync(document, cancellationToken)
            .ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();

        if (!ReferenceEquals(_document, document) || !CanBrowseDocument)
        {
            throw new InvalidOperationException(
                "The active bookmark document changed while edit projections were refreshing.");
        }

        SetSearchIndex(refreshedIndex);

        _suppressFolderSearchRefresh = true;
        try
        {
            BuildBrowserState(document);

            foreach (var expandedFolder in expandedFolders)
            {
                if (_folderLookup.TryGetValue(expandedFolder, out var expandedItem))
                {
                    expandedItem.IsExpanded = true;
                }
            }

            if (!_folderLookup.TryGetValue(selectedFolder, out var selectedItem))
            {
                selectedItem = _folderLookup[document.Roots.BookmarkBar];
            }

            SelectFolder(selectedItem);

            if (selectedBookmark is not null &&
                ReferenceEquals(selectedBookmark.Parent, SelectedFolder) &&
                CurrentBookmarks.Any(
                    bookmark => ReferenceEquals(bookmark, selectedBookmark)))
            {
                SetSelectedBookmark(selectedBookmark);
            }
        }
        finally
        {
            _suppressFolderSearchRefresh = false;
        }

        if (searchWasActive &&
            !string.IsNullOrWhiteSpace(SearchText))
        {
            ScheduleSearch(useDebounce: false);
            var refreshSearch = _pendingSearchTask;
            await refreshSearch.ConfigureAwait(true);

            if (selectedBookmark is not null &&
                SearchResults.Any(
                    bookmark => ReferenceEquals(bookmark, selectedBookmark)))
            {
                SetSelectedBookmark(selectedBookmark);
            }
        }
        else
        {
            SetSearchSummaryText(string.Empty);
        }
    }

    internal async Task RefreshProjectionsAfterMoveAsync(
        BookmarkFolder? preferredFolder = null,
        BookmarkUrl? preferredBookmark = null,
        CancellationToken cancellationToken = default)
    {
        if (_document is null || !CanBrowseDocument)
        {
            throw new InvalidOperationException(
                "An editable bookmark document must be loaded before move projections can be refreshed.");
        }

        var document = _document;
        var selectedFolder =
            preferredFolder ??
            _selectedFolder ??
            document.Roots.BookmarkBar;
        var selectedBookmark =
            preferredBookmark ??
            _selectedBookmark;
        var expandedFolders = _folderLookup
            .Where(pair => pair.Value.IsExpanded)
            .Select(pair => pair.Key)
            .ToArray();
        var searchWasActive = IsSearchActive;

        ++_searchGeneration;
        CancelPendingSearch();
        _pendingSearchTask = Task.CompletedTask;
        SetSearchResults(Array.Empty<BookmarkUrl>());
        SetIsSearchBusy(false);
        SetSearchSummaryText(
            searchWasActive ? "Refreshing search..." : string.Empty);

        cancellationToken.ThrowIfCancellationRequested();

        if (!ReferenceEquals(_document, document) || !CanBrowseDocument)
        {
            throw new InvalidOperationException(
                "The active bookmark document changed while move projections were refreshing.");
        }

        _suppressFolderSearchRefresh = true;
        try
        {
            BuildBrowserState(document);

            foreach (var expandedFolder in expandedFolders)
            {
                if (_folderLookup.TryGetValue(expandedFolder, out var expandedItem))
                {
                    expandedItem.IsExpanded = true;
                }
            }

            if (!_folderLookup.TryGetValue(selectedFolder, out var selectedItem))
            {
                selectedItem = _folderLookup[document.Roots.BookmarkBar];
            }

            SelectFolder(selectedItem);

            if (selectedBookmark is not null &&
                ReferenceEquals(selectedBookmark.Parent, SelectedFolder) &&
                CurrentBookmarks.Any(
                    bookmark => ReferenceEquals(bookmark, selectedBookmark)))
            {
                SetSelectedBookmark(selectedBookmark);
            }
        }
        finally
        {
            _suppressFolderSearchRefresh = false;
        }

        if (searchWasActive &&
            !string.IsNullOrWhiteSpace(SearchText))
        {
            ScheduleSearch(useDebounce: false);
            var refreshSearch = _pendingSearchTask;
            await refreshSearch.ConfigureAwait(true);

            if (selectedBookmark is not null &&
                SearchResults.Any(
                    bookmark => ReferenceEquals(bookmark, selectedBookmark)))
            {
                SetSelectedBookmark(selectedBookmark);
            }
        }
        else
        {
            SetSearchSummaryText(string.Empty);
        }
    }

    private void EnsurePositionalBookmarkMoveAllowed()
    {
        if (IsSearchActive)
        {
            throw new BookmarkMoveException(
                BookmarkMoveError.InvalidDropTarget,
                "Bookmarks cannot be positionally reordered while search results are displayed.");
        }
    }

    private BookmarkDocument RequireEditableDocument()
    {
        if (_document is null || !CanBrowseDocument)
        {
            throw new InvalidOperationException(
                "An editable bookmark document is not loaded.");
        }

        return _document;
    }

    private bool IsPermanentRoot(BookmarkFolder folder) =>
        _document is not null &&
        (ReferenceEquals(folder, _document.Roots.BookmarkBar) ||
         ReferenceEquals(folder, _document.Roots.Other) ||
         ReferenceEquals(folder, _document.Roots.Synced));

    private void RecordHistory(IBookmarkHistoryEntry entry)
    {
        _history.Record(entry);
        SyncDocumentStateFromHistory();
    }

    private void ClearHistory()
    {
        _history.Clear();
        NotifyHistoryAvailabilityChanged();
    }

    private void SyncDocumentStateFromHistory()
    {
        if (State is not DocumentState.LoadedClean and
            not DocumentState.LoadedDirty and
            not DocumentState.SaveFailed)
        {
            throw new InvalidOperationException(
                "History state can only be synchronized for a loaded bookmark document.");
        }

        var isClean = _history.IsAtCleanState;

        SetState(
            isClean
                ? DocumentState.LoadedClean
                : DocumentState.LoadedDirty);

        SetStatusText(
            isClean
                ? "No unsaved in-memory changes. Source file has not been modified."
                : "Unsaved in-memory changes. Changes are not saved to disk.");

        NotifyHistoryAvailabilityChanged();
    }

    private async Task RefreshAfterHistoryAsync(
        BookmarkHistoryResult result,
        BookmarkFolder? preferredFolder,
        BookmarkUrl? preferredBookmark)
    {
        switch (result.Impact)
        {
            case BookmarkHistoryImpact.SearchRelevant:
                await RefreshProjectionsAfterEditAsync(
                        preferredFolder,
                        preferredBookmark)
                    .ConfigureAwait(true);
                break;

            case BookmarkHistoryImpact.StructureOnly:
                await RefreshProjectionsAfterMoveAsync(
                        preferredFolder,
                        preferredBookmark)
                    .ConfigureAwait(true);
                break;

            case BookmarkHistoryImpact.None:
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(result),
                    result.Impact,
                    "Unsupported bookmark history projection impact.");
        }
    }

    private void NotifyHistoryAvailabilityChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoDescription));
        OnPropertyChanged(nameof(RedoDescription));
    }

    private void MarkDirty()
    {
        if (State == DocumentState.LoadedClean)
        {
            SetState(DocumentState.LoadedDirty);
        }
        else if (State != DocumentState.LoadedDirty)
        {
            throw new InvalidOperationException(
                "Only a loaded bookmark document can become dirty.");
        }

        SetStatusText(
            "Unsaved in-memory changes. Changes are not saved to disk.");
    }

    private void NotifyEditingAvailabilityChanged()
    {
        OnPropertyChanged(nameof(CanAddBookmark));
        OnPropertyChanged(nameof(CanAddFolder));
        OnPropertyChanged(nameof(CanRenameSelectedFolder));
        OnPropertyChanged(nameof(CanRenameSelectedBookmark));
        OnPropertyChanged(nameof(CanEditSelectedBookmarkUrl));
        OnPropertyChanged(nameof(CanMoveSelectedBookmark));
        OnPropertyChanged(nameof(CanMoveSelectedFolder));
        OnPropertyChanged(nameof(CanDeleteSelectedBookmarks));
        OnPropertyChanged(nameof(CanDeleteSelectedFolder));
        OnPropertyChanged(nameof(CanMoveSelectedBookmarks));
    }

    private void BuildBrowserState(BookmarkDocument document)
    {
        var roots = new[]
        {
            new FolderTreeItemViewModel(document.Roots.BookmarkBar),
            new FolderTreeItemViewModel(document.Roots.Other),
            new FolderTreeItemViewModel(document.Roots.Synced)
        };

        BuildFolderLookup(roots);
        SetFolderRoots(roots);
        SetDocumentSummaryText(
            $"{document.UrlCount.ToString("N0", CultureInfo.InvariantCulture)} URLs | " +
            $"{document.FolderCount.ToString("N0", CultureInfo.InvariantCulture)} folders");
        SelectFolder(roots[0]);
    }

    private void BuildFolderLookup(
        IReadOnlyList<FolderTreeItemViewModel> roots)
    {
        _folderLookup.Clear();

        var stack = new Stack<FolderTreeItemViewModel>();
        for (var index = roots.Count - 1; index >= 0; index--)
        {
            stack.Push(roots[index]);
        }

        while (stack.TryPop(out var item))
        {
            _folderLookup.Add(item.Folder, item);

            for (var index = item.Children.Count - 1; index >= 0; index--)
            {
                stack.Push(item.Children[index]);
            }
        }
    }

    private void ClearBrowserState()
    {
        if (_selectedFolderItem is not null)
        {
            _selectedFolderItem.IsSelected = false;
        }

        _selectedFolderItem = null;
        _folderLookup.Clear();
        SetFolderRoots(Array.Empty<FolderTreeItemViewModel>());
        SetSelectedFolder(null);
        ClearBookmarkSelection();
        SetCurrentBookmarks(Array.Empty<BookmarkUrl>());
        SetDocumentSummaryText(string.Empty);
        SetSelectionSummaryText(string.Empty);
    }

    private void SetSearchText(string value)
    {
        if (string.Equals(_searchText, value, StringComparison.Ordinal))
        {
            return;
        }

        _searchText = value;
        ClearBookmarkSelection();
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(DisplayedBookmarks));

        if (string.IsNullOrWhiteSpace(value))
        {
            CancelPendingSearch();
            SetSearchResults(Array.Empty<BookmarkUrl>());
            SetIsSearchBusy(false);
            SetSearchSummaryText(string.Empty);
            _pendingSearchTask = Task.CompletedTask;
            return;
        }

        ScheduleSearch(useDebounce: true);
    }

    private void SetSearchScope(BookmarkSearchScope value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unsupported bookmark search scope.");
        }

        if (_searchScope == value)
        {
            return;
        }

        _searchScope = value;
        ClearBookmarkSelection();
        OnPropertyChanged(nameof(SearchScope));

        if (IsSearchActive)
        {
            ScheduleSearch(useDebounce: false);
        }
    }

    private void RerunSearchForFolderChange()
    {
        if (_suppressFolderSearchRefresh)
        {
            return;
        }

        if (IsSearchActive &&
            SearchScope == BookmarkSearchScope.CurrentFolder)
        {
            ScheduleSearch(useDebounce: false);
        }
    }

    private void ScheduleSearch(bool useDebounce)
    {
        var generation = ++_searchGeneration;
        CancelPendingSearch();

        if (!CanSearchDocument ||
            _searchIndex is null ||
            string.IsNullOrWhiteSpace(SearchText))
        {
            SetSearchResults(Array.Empty<BookmarkUrl>());
            SetIsSearchBusy(false);
            SetSearchSummaryText(string.Empty);
            _pendingSearchTask = Task.CompletedTask;
            return;
        }

        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;

        var debounce = useDebounce ? _searchDebounce : TimeSpan.Zero;
        _pendingSearchTask = RunSearchAsync(
            generation,
            _searchIndex,
            SearchText,
            SearchScope,
            SelectedFolder,
            debounce,
            cancellation);
    }

    private async Task RunSearchAsync(
        long generation,
        BookmarkSearchIndex index,
        string query,
        BookmarkSearchScope scope,
        BookmarkFolder? currentFolder,
        TimeSpan debounce,
        CancellationTokenSource cancellation)
    {
        try
        {
            if (debounce > TimeSpan.Zero)
            {
                await Task.Delay(debounce, cancellation.Token)
                    .ConfigureAwait(true);
            }

            cancellation.Token.ThrowIfCancellationRequested();

            if (!IsLatestSearch(generation, cancellation))
            {
                return;
            }

            SetIsSearchBusy(true);
            SetSearchSummaryText("Searching...");

            var results = await _searchService
                .SearchAsync(
                    index,
                    query,
                    scope,
                    currentFolder,
                    cancellation.Token)
                .ConfigureAwait(true);

            cancellation.Token.ThrowIfCancellationRequested();

            if (!IsLatestSearch(generation, cancellation))
            {
                return;
            }

            SetSearchResults(results);
            SetSearchSummaryText(
                results.Count == 0
                    ? "No matches"
                    : $"{results.Count.ToString("N0", CultureInfo.InvariantCulture)} matches");
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (IsLatestSearch(generation, cancellation))
            {
                SetSearchResults(Array.Empty<BookmarkUrl>());
                SetSearchSummaryText("Search failed.");
            }
        }
        finally
        {
            if (IsLatestSearch(generation, cancellation))
            {
                SetIsSearchBusy(false);
                _searchCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private bool IsLatestSearch(
        long generation,
        CancellationTokenSource cancellation) =>
        generation == _searchGeneration &&
        ReferenceEquals(_searchCancellation, cancellation);

    private void CancelPendingSearch()
    {
        var cancellation = _searchCancellation;
        _searchCancellation = null;
        cancellation?.Cancel();
    }

    private void ResetSearchState(bool clearIndex, bool resetScope)
    {
        ++_searchGeneration;
        CancelPendingSearch();
        _pendingSearchTask = Task.CompletedTask;

        if (_searchText.Length != 0)
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));
        }

        if (resetScope && _searchScope != BookmarkSearchScope.AllBookmarks)
        {
            _searchScope = BookmarkSearchScope.AllBookmarks;
            OnPropertyChanged(nameof(SearchScope));
        }

        if (clearIndex)
        {
            SetSearchIndex(null);
        }

        SetSearchResults(Array.Empty<BookmarkUrl>());
        SetIsSearchBusy(false);
        SetSearchSummaryText(string.Empty);
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(DisplayedBookmarks));
    }

    private void SetState(DocumentState value)
    {
        if (_state == value)
        {
            return;
        }

        _state = value;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(CanOpenBookmarks));
        OnPropertyChanged(nameof(CanCancelLoad));
        OnPropertyChanged(nameof(CanBrowseDocument));
        OnPropertyChanged(nameof(CanSearchDocument));
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(DisplayedBookmarks));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CanSave));
        NotifyEditingAvailabilityChanged();
        NotifyHistoryAvailabilityChanged();
    }

    private void SetDocument(BookmarkDocument? value)
    {
        if (ReferenceEquals(_document, value))
        {
            return;
        }

        _document = value;
        OnPropertyChanged(nameof(Document));
    }

    private void SetSearchIndex(BookmarkSearchIndex? value)
    {
        if (ReferenceEquals(_searchIndex, value))
        {
            return;
        }

        _searchIndex = value;
        OnPropertyChanged(nameof(CanSearchDocument));
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(DisplayedBookmarks));
    }

    private void SetSourcePath(string? value)
    {
        if (string.Equals(_sourcePath, value, StringComparison.Ordinal))
        {
            return;
        }

        _sourcePath = value;
        OnPropertyChanged(nameof(SourcePath));
    }

    private void SetSourceBaseline(BookmarkSourceBaseline? value)
    {
        if (Equals(_sourceBaseline, value))
        {
            return;
        }

        _sourceBaseline = value;
        OnPropertyChanged(nameof(SourceBaseline));
        OnPropertyChanged(nameof(CanSave));
    }

    private void SetStatusText(string value)
    {
        if (string.Equals(_statusText, value, StringComparison.Ordinal))
        {
            return;
        }

        _statusText = value;
        OnPropertyChanged(nameof(StatusText));
    }

    private void SetFolderRoots(
        IReadOnlyList<FolderTreeItemViewModel> value)
    {
        if (ReferenceEquals(_folderRoots, value))
        {
            return;
        }

        _folderRoots = value;
        OnPropertyChanged(nameof(FolderRoots));
    }

    private void SetSelectedFolder(BookmarkFolder? value)
    {
        if (ReferenceEquals(_selectedFolder, value))
        {
            return;
        }

        _selectedFolder = value;
        OnPropertyChanged(nameof(SelectedFolder));
        NotifyEditingAvailabilityChanged();
    }

    private void SetCurrentBookmarks(IReadOnlyList<BookmarkUrl> value)
    {
        if (ReferenceEquals(_currentBookmarks, value))
        {
            return;
        }

        _currentBookmarks = value;
        OnPropertyChanged(nameof(CurrentBookmarks));

        if (!IsSearchActive)
        {
            OnPropertyChanged(nameof(DisplayedBookmarks));
        }
    }

    private void SetSelectedBookmark(BookmarkUrl? value)
    {
        if (ReferenceEquals(_selectedBookmark, value))
        {
            return;
        }

        _selectedBookmark = value;
        OnPropertyChanged(nameof(SelectedBookmark));
        NotifyEditingAvailabilityChanged();
    }

    private void SetSelectedBookmarks(
        IReadOnlyList<BookmarkUrl> value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (ReferenceSequenceEqual(
                _selectedBookmarks,
                value))
        {
            return;
        }

        _selectedBookmarks = value;
        OnPropertyChanged(nameof(SelectedBookmarks));
        OnPropertyChanged(nameof(SelectedBookmarkCount));
        OnPropertyChanged(nameof(HasMultipleSelectedBookmarks));
        NotifyEditingAvailabilityChanged();
    }

    private IReadOnlyList<BookmarkUrl>
        GetEffectiveSelectedBookmarks()
    {
        if (_selectedBookmarks.Count > 0)
        {
            return _selectedBookmarks;
        }

        return _selectedBookmark is null
            ? Array.Empty<BookmarkUrl>()
            : new[] { _selectedBookmark };
    }

    private void ClearBookmarkSelection()
    {
        SetSelectedBookmarks(
            Array.Empty<BookmarkUrl>());
        SetSelectedBookmark(null);
    }

    private static bool ReferenceSequenceEqual(
        IReadOnlyList<BookmarkUrl> left,
        IReadOnlyList<BookmarkUrl> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!ReferenceEquals(
                    left[index],
                    right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void SetDocumentSummaryText(string value)
    {
        if (string.Equals(
                _documentSummaryText,
                value,
                StringComparison.Ordinal))
        {
            return;
        }

        _documentSummaryText = value;
        OnPropertyChanged(nameof(DocumentSummaryText));
    }

    private void SetSelectionSummaryText(string value)
    {
        if (string.Equals(
                _selectionSummaryText,
                value,
                StringComparison.Ordinal))
        {
            return;
        }

        _selectionSummaryText = value;
        OnPropertyChanged(nameof(SelectionSummaryText));
    }

    private void SetSearchResults(IReadOnlyList<BookmarkUrl> value)
    {
        if (ReferenceEquals(_searchResults, value))
        {
            return;
        }

        _searchResults = value;
        OnPropertyChanged(nameof(SearchResults));

        if (IsSearchActive)
        {
            OnPropertyChanged(nameof(DisplayedBookmarks));
        }
    }

    private void SetIsSearchBusy(bool value)
    {
        if (_isSearchBusy == value)
        {
            return;
        }

        _isSearchBusy = value;
        OnPropertyChanged(nameof(IsSearchBusy));
    }

    private void SetSearchSummaryText(string value)
    {
        if (string.Equals(
                _searchSummaryText,
                value,
                StringComparison.Ordinal))
        {
            return;
        }

        _searchSummaryText = value;
        OnPropertyChanged(nameof(SearchSummaryText));
    }
}
