using System.IO;
using System.Diagnostics;
using System.Globalization;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Clipboard;
using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Application.Exporting;
using ChromeBookmarksManager.Application.History;
using ChromeBookmarksManager.Application.Launching;
using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Application.Saving;
using ChromeBookmarksManager.Application.Sorting;
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
    private readonly BookmarkSortService _sortService = new();
    private readonly BookmarkHtmlExportService _htmlExportService = new();
    private readonly IBookmarkClipboardService? _clipboardService;
    private readonly IBookmarkClipboardStore? _clipboardStore;
    private readonly IExternalUrlLauncher? _urlLauncher;
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
    private IReadOnlyList<BookmarkListItemViewModel> _currentItems =
        Array.Empty<BookmarkListItemViewModel>();
    private BookmarkListItemViewModel? _selectedContentItem;
    private IReadOnlyList<BookmarkListItemViewModel> _selectedContentItems =
        Array.Empty<BookmarkListItemViewModel>();
    private BookmarkUrl? _selectedBookmark;
    private IReadOnlyList<BookmarkUrl> _selectedBookmarks =
        Array.Empty<BookmarkUrl>();
    private string _documentSummaryText = string.Empty;
    private string _selectionSummaryText = string.Empty;
    private string _searchText = string.Empty;
    private BookmarkSearchScope _searchScope = BookmarkSearchScope.AllBookmarks;
    private IReadOnlyList<BookmarkNode> _searchResults =
        Array.Empty<BookmarkNode>();
    private IReadOnlyList<BookmarkUrl> _searchBookmarks =
        Array.Empty<BookmarkUrl>();
    private IReadOnlyList<BookmarkListItemViewModel> _searchItems =
        Array.Empty<BookmarkListItemViewModel>();
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
        IExternalUrlLauncher urlLauncher,
        TimeSpan searchDebounce)
        : this(
            reader,
            searchService,
            new BookmarkEditingService(),
            new BookmarkMoveService(),
            new BookmarkDeleteService(),
            baselineService: null,
            saveService: null,
            urlLauncher,
            searchDebounce)
    {
    }

    internal MainViewModel(
        IChromeBookmarksReader reader,
        IBookmarkSearchService searchService,
        IBookmarkClipboardService clipboardService,
        IBookmarkClipboardStore clipboardStore,
        TimeSpan searchDebounce)
        : this(
            reader,
            searchService,
            new BookmarkEditingService(),
            new BookmarkMoveService(),
            new BookmarkDeleteService(),
            searchDebounce)
    {
        _clipboardService = clipboardService
            ?? throw new ArgumentNullException(nameof(clipboardService));
        _clipboardStore = clipboardStore
            ?? throw new ArgumentNullException(nameof(clipboardStore));
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
        IBookmarkSourceBaselineService baselineService,
        IChromeBookmarksSaveService saveService,
        IExternalUrlLauncher urlLauncher,
        TimeSpan searchDebounce)
        : this(
            reader,
            searchService,
            new BookmarkEditingService(),
            new BookmarkMoveService(),
            new BookmarkDeleteService(),
            baselineService,
            saveService,
            urlLauncher,
            searchDebounce)
    {
    }

    internal MainViewModel(
        IChromeBookmarksReader reader,
        IBookmarkSearchService searchService,
        IBookmarkSourceBaselineService baselineService,
        IChromeBookmarksSaveService saveService,
        IExternalUrlLauncher urlLauncher,
        IBookmarkClipboardService clipboardService,
        IBookmarkClipboardStore clipboardStore,
        TimeSpan searchDebounce)
        : this(
            reader,
            searchService,
            new BookmarkEditingService(),
            new BookmarkMoveService(),
            new BookmarkDeleteService(),
            baselineService,
            saveService,
            urlLauncher,
            searchDebounce)
    {
        _clipboardService = clipboardService
            ?? throw new ArgumentNullException(nameof(clipboardService));
        _clipboardStore = clipboardStore
            ?? throw new ArgumentNullException(nameof(clipboardStore));
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
        : this(
            reader,
            searchService,
            editingService,
            moveService,
            deleteService,
            baselineService,
            saveService,
            urlLauncher: null,
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
        IExternalUrlLauncher? urlLauncher,
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
        _clipboardService = null;
        _clipboardStore = null;
        _urlLauncher = urlLauncher;
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

    public IReadOnlyList<BookmarkListItemViewModel> CurrentItems =>
        _currentItems;

    public BookmarkListItemViewModel? SelectedContentItem =>
        _selectedContentItem;

    public IReadOnlyList<BookmarkListItemViewModel> SelectedContentItems =>
        _selectedContentItems;

    public BookmarkFolder? SelectedContentFolder =>
        _selectedContentItems.Count == 1
            ? _selectedContentItems[0].Node as BookmarkFolder
            : null;

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

    public IReadOnlyList<BookmarkNode> SearchResults => _searchResults;

    public IReadOnlyList<BookmarkUrl> DisplayedBookmarks =>
        IsSearchActive ? _searchBookmarks : CurrentBookmarks;

    public IReadOnlyList<BookmarkListItemViewModel> DisplayedItems =>
        IsSearchActive ? _searchItems : CurrentItems;

    public bool IsSearchActive =>
        CanSearchDocument && !string.IsNullOrWhiteSpace(SearchText);

    public bool IsSearchBusy => _isSearchBusy;

    public bool CanSearchDocument =>
        CanBrowseDocument && _searchIndex is not null;

    public string SearchSummaryText => _searchSummaryText;

    public bool IsDirty =>
        State is DocumentState.LoadedDirty or
            DocumentState.SaveFailed or
            DocumentState.RecoveryRequired;

    public bool CanSave =>
        _saveService is not null &&
        _sourceBaseline is not null &&
        _document is not null &&
        State is DocumentState.LoadedDirty or DocumentState.SaveFailed;

    private bool CanEditDocument =>
        State is DocumentState.LoadedClean or
            DocumentState.LoadedDirty or
            DocumentState.SaveFailed;

    public bool CanUndo =>
        CanEditDocument && _history.CanUndo;

    public bool CanRedo =>
        CanEditDocument && _history.CanRedo;

    public string? UndoDescription =>
        CanUndo ? _history.UndoDescription : null;

    public string? RedoDescription =>
        CanRedo ? _history.RedoDescription : null;

    public bool CanAddBookmark =>
        CanEditDocument && SelectedFolder is not null;

    public bool CanAddFolder =>
        CanEditDocument && SelectedFolder is not null;

    public bool CanSortSelectedFolder =>
        CanEditDocument && SelectedFolder is not null;

    public bool CanRenameSelectedFolder =>
        CanEditDocument &&
        SelectedFolder is not null &&
        !IsPermanentRoot(SelectedFolder);

    public bool CanRenameSelectedContentFolder =>
        CanEditDocument &&
        SelectedContentFolder is not null &&
        !IsPermanentRoot(SelectedContentFolder);

    public bool CanMoveSelectedContentFolder =>
        CanEditDocument &&
        SelectedContentFolder is not null &&
        !IsPermanentRoot(SelectedContentFolder);

    public bool CanDeleteSelectedContentFolder =>
        CanEditDocument &&
        SelectedContentFolder is not null &&
        !IsPermanentRoot(SelectedContentFolder);

    public bool CanRenameSelectedBookmark =>
        CanEditDocument &&
        HasUnambiguousSelectedBookmark();

    public bool CanEditSelectedBookmarkUrl =>
        CanEditDocument &&
        HasUnambiguousSelectedBookmark();

    public bool CanMoveSelectedBookmark =>
        CanEditDocument &&
        HasUnambiguousSelectedBookmark();

    public bool CanMoveSelectedFolder =>
        CanEditDocument &&
        SelectedFolder is not null &&
        !IsPermanentRoot(SelectedFolder);

    public bool CanDeleteSelectedBookmarks =>
        CanEditDocument &&
        (_selectedBookmarks.Count > 0 ||
         SelectedBookmark is not null);

    public bool CanDeleteSelectedFolder =>
        CanEditDocument &&
        SelectedFolder is not null &&
        !IsPermanentRoot(SelectedFolder);

    public bool CanMoveSelectedBookmarks =>
        CanEditDocument &&
        (_selectedBookmarks.Count > 0 ||
         SelectedBookmark is not null);

    public bool CanRenameSelectedContentBookmark =>
        CanRenameSelectedBookmark;

    public bool CanEditSelectedContentBookmarkUrl =>
        CanEditSelectedBookmarkUrl;

    public bool CanMoveSelectedContentBookmark =>
        CanMoveSelectedBookmark;

    public bool CanDeleteSelectedContentBookmarks =>
        CanDeleteSelectedBookmarks;

    public bool CanDeleteSelectedContentItems =>
        CanEditDocument &&
        _selectedContentItems.Count > 0 &&
        _selectedContentItems.All(
            item =>
                item.Node is not BookmarkFolder folder ||
                !IsPermanentRoot(folder));

    public bool CanMoveSelectedContentBookmarks =>
        CanMoveSelectedBookmarks;

    public bool CanMoveSelectedContentItems =>
        CanEditDocument &&
        _selectedContentItems.Count > 0 &&
        _selectedContentItems.All(
            item =>
                item.Node is not BookmarkFolder folder ||
                !IsPermanentRoot(folder));

    public bool CanCopySelectedContentItems =>
        CanBrowseDocument &&
        _clipboardService is not null &&
        _clipboardStore is not null &&
        _selectedContentItems.Count > 0;

    public bool CanCutSelectedContentItems =>
        CanEditDocument &&
        _clipboardService is not null &&
        _clipboardStore is not null &&
        _selectedContentItems.Count > 0 &&
        _selectedContentItems.All(
            item =>
                item.Node is not BookmarkFolder folder ||
                !IsPermanentRoot(folder));

    public bool CanPasteClipboard =>
        CanEditDocument &&
        SelectedFolder is not null &&
        _clipboardService is not null &&
        _clipboardStore?.HasPayload == true;

    public bool CanOpenSelectedContentItem =>
        CanBrowseDocument &&
        _selectedContentItems.Count == 1 &&
        (_selectedContentItems[0].Node is BookmarkFolder ||
         (_selectedContentItems[0].Node is BookmarkUrl &&
          _urlLauncher is not null));

    public bool CanExportBookmarksHtml =>
        CanBrowseDocument && _document is not null;

    public bool CanOpenBookmarks =>
        State is not DocumentState.Loading and not DocumentState.Saving;

    public bool CanCancelLoad => State == DocumentState.Loading;

    public bool CanBrowseDocument =>
        State is DocumentState.LoadedClean or
            DocumentState.LoadedDirty or
            DocumentState.SaveFailed or
            DocumentState.RecoveryRequired;

    public async Task ExportBookmarksHtmlAsync(
        TextWriter writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);

        await _documentOperationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(true);
        try
        {
            var document = _document;
            if (document is null || !CanBrowseDocument)
            {
                throw new InvalidOperationException(
                    "A loaded bookmark document is required before export.");
            }

            await _htmlExportService
                .ExportAsync(
                    document,
                    writer,
                    cancellationToken)
                .ConfigureAwait(true);
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

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
                if (exception.Error ==
                    ChromeBookmarksSaveError.RecoveryRequired)
                {
                    SetState(DocumentState.RecoveryRequired);

                    var recoveryBackup =
                        exception.HasVerifiedRecoveryBackup
                            ? $" Verified recovery backup: {exception.BackupPath}"
                            : string.Empty;

                    SetStatusText(
                        exception.Message +
                        recoveryBackup +
                        " Reload or recover the Bookmarks source before saving again.");
                }
                else
                {
                    SetState(DocumentState.SaveFailed);
                    SetStatusText(exception.Message);
                }

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

    public async Task<bool> SortSelectedFolderByNameAsync()
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();
            var folder = SelectedFolder
                ?? throw new InvalidOperationException(
                    "Select a folder before sorting it.");

            var result = _sortService.SortByName(
                document,
                folder,
                CultureInfo.CurrentUICulture);

            if (!result.Changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkSortHistoryEntry(result));

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

    public Task<bool> RenameSelectedFolderAsync(string newName)
    {
        var folder = SelectedFolder
            ?? throw new InvalidOperationException(
                "Select a folder before renaming it.");

        return RenameFolderAsync(folder, newName);
    }

    public async Task<bool> RenameFolderAsync(
        BookmarkFolder folder,
        string newName)
    {
        ArgumentNullException.ThrowIfNull(folder);

        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();

            if (IsPermanentRoot(folder))
            {
                throw new InvalidOperationException(
                    "Permanent Chrome root folders cannot be renamed.");
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
                    preferredFolder: SelectedFolder ?? folder)
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

    public bool CopySelectedContentItems() =>
        CaptureSelectedContentItemsToClipboard(
            BookmarkClipboardMode.Copy);

    public bool CutSelectedContentItems() =>
        CaptureSelectedContentItemsToClipboard(
            BookmarkClipboardMode.Cut);

    public async Task<bool> PasteClipboardIntoSelectedFolderAsync()
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            if (_clipboardService is null ||
                _clipboardStore is null ||
                SelectedFolder is null)
            {
                return false;
            }

            var document = RequireEditableDocument();
            var payload = _clipboardStore.GetPayload();

            if (payload is null)
            {
                return false;
            }

            var targetParent = SelectedFolder;
            var result = _clipboardService.Paste(
                document,
                payload,
                targetParent,
                targetParent.Children.Count);

            if (!result.Changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkClipboardPasteHistoryEntry(
                    result));

            if (payload.Mode == BookmarkClipboardMode.Cut)
            {
                _clipboardStore.Clear();
            }

            ClearContentSelection();
            NotifyEditingAvailabilityChanged();

            if (result.MovedOriginalNodes)
            {
                await RefreshProjectionsAfterMoveAsync(
                        preferredFolder: targetParent)
                    .ConfigureAwait(true);
            }
            else
            {
                await RefreshProjectionsAfterEditAsync(
                        preferredFolder: targetParent)
                    .ConfigureAwait(true);
            }

            return true;
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    private bool CaptureSelectedContentItemsToClipboard(
        BookmarkClipboardMode mode)
    {
        if (_document is null ||
            _clipboardService is null ||
            _clipboardStore is null)
        {
            return false;
        }

        var allowed = mode switch
        {
            BookmarkClipboardMode.Copy =>
                CanCopySelectedContentItems,
            BookmarkClipboardMode.Cut =>
                CanCutSelectedContentItems,
            _ => false
        };

        if (!allowed)
        {
            return false;
        }

        var nodes = _selectedContentItems
            .Select(item => item.Node)
            .ToArray();

        if (nodes.Length == 0)
        {
            return false;
        }

        var payload = _clipboardService.Capture(
            _document,
            nodes,
            mode);
        _clipboardStore.SetPayload(payload);
        NotifyEditingAvailabilityChanged();
        return true;
    }

    public async Task<bool> OpenSelectedContentItemAsync(
        CancellationToken cancellationToken = default)
    {
        if (!CanOpenSelectedContentItem ||
            _selectedContentItems.Count != 1)
        {
            return false;
        }

        var node = _selectedContentItems[0].Node;

        if (node is BookmarkFolder folder)
        {
            if (IsSearchActive &&
                SearchResults.Any(
                    result => ReferenceEquals(result, folder)))
            {
                NavigateToSearchResult(folder);
                return ReferenceEquals(SelectedFolder, folder);
            }

            return NavigateToFolder(folder);
        }

        if (node is not BookmarkUrl bookmark ||
            _urlLauncher is null)
        {
            return false;
        }

        await _urlLauncher
            .LaunchAsync(
                bookmark.Url,
                cancellationToken)
            .ConfigureAwait(true);

        return true;
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

        var contentItems = DisplayedItems
            .Where(
                item =>
                    item.Node is BookmarkUrl bookmark &&
                    requested.Contains(bookmark))
            .ToArray();

        UpdateSelectedContentItems(contentItems);
    }

    public void UpdateSelectedContentItems(
        IEnumerable<BookmarkListItemViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var requested = new HashSet<BookmarkListItemViewModel>();
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            requested.Add(item);
        }

        var ordered = DisplayedItems
            .Where(requested.Contains)
            .ToArray();

        var bookmarkItems = ordered
            .Where(item => item.Node is BookmarkUrl)
            .ToArray();
        var bookmarks = bookmarkItems
            .Select(item => (BookmarkUrl)item.Node)
            .ToArray();

        var primaryItem =
            _selectedContentItem is not null &&
            ordered.Any(
                item => ReferenceEquals(
                    item,
                    _selectedContentItem))
                ? _selectedContentItem
                : ordered.FirstOrDefault();

        var primaryBookmark =
            _selectedBookmark is not null &&
            bookmarks.Any(
                bookmark => ReferenceEquals(
                    bookmark,
                    _selectedBookmark))
                ? _selectedBookmark
                : bookmarks.FirstOrDefault();

        SetSelectedContentItems(ordered);
        SetSelectedContentItem(primaryItem);
        SetSelectedBookmarks(bookmarks);
        SetSelectedBookmark(primaryBookmark);
    }

    public async Task<bool> DeleteSelectedContentItemsAsync()
    {
        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();
            var selected = _selectedContentItems
                .Select(item => item.Node)
                .ToArray();

            if (selected.Length == 0)
            {
                return false;
            }

            var preferredFolder =
                SelectedFolder ??
                document.Roots.BookmarkBar;

            var result = _deleteService.DeleteNodes(
                document,
                selected);

            if (!result.Changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkBatchDeleteHistoryEntry(result));
            ClearContentSelection();

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
            ClearContentSelection();

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

    public Task<bool> DeleteSelectedFolderAsync()
    {
        var folder = SelectedFolder
            ?? throw new InvalidOperationException(
                "Select a folder before deleting it.");

        return DeleteFolderAsync(folder);
    }

    public async Task<bool> DeleteFolderAsync(BookmarkFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();

            if (IsPermanentRoot(folder))
            {
                throw new InvalidOperationException(
                    "Permanent Chrome root folders cannot be deleted.");
            }

            var selectedFolderBeforeDelete = SelectedFolder;
            var deletingNavigationFolder =
                ReferenceEquals(selectedFolderBeforeDelete, folder);

            var result = _deleteService.DeleteNode(
                document,
                folder);

            RecordHistory(
                new BookmarkDeleteHistoryEntry(result));
            ClearContentSelection();

            await RefreshProjectionsAfterEditAsync(
                    preferredFolder:
                        deletingNavigationFolder
                            ? result.SourceParent
                            : selectedFolderBeforeDelete ??
                              result.SourceParent)
                .ConfigureAwait(true);

            return true;
        }
        finally
        {
            _documentOperationGate.Release();
        }
    }

    public async Task<bool> MoveContentNodesAsync(
        IReadOnlyList<BookmarkNode> nodes,
        BookmarkFolder targetParent,
        int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(targetParent);

        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();

            if (nodes.Count == 0)
            {
                return false;
            }

            var searchWasActive = IsSearchActive;
            var selectedFolderBeforeMove = SelectedFolder;

            var result = _moveService.MoveNodes(
                document,
                nodes,
                targetParent,
                targetIndex);

            if (!result.Changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkBatchMoveHistoryEntry(
                    nodes,
                    result));
            ClearContentSelection();

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

    public async Task<bool> MoveSelectedContentItemsToEndAsync(
        BookmarkFolder targetParent)
    {
        ArgumentNullException.ThrowIfNull(targetParent);

        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            var document = RequireEditableDocument();
            var selected = _selectedContentItems
                .Select(item => item.Node)
                .ToArray();

            if (selected.Length == 0)
            {
                return false;
            }

            var searchWasActive = IsSearchActive;
            var selectedFolderBeforeMove = SelectedFolder;

            var result = _moveService.MoveNodes(
                document,
                selected,
                targetParent,
                targetParent.Children.Count);

            if (!result.Changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkBatchMoveHistoryEntry(
                    selected,
                    result));
            ClearContentSelection();

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
            ClearContentSelection();

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

    public Task<bool> MoveNodeBeforeAsync(
        BookmarkNode node,
        BookmarkNode target) =>
        MoveNodeRelativeAsync(
            node,
            target,
            insertAfter: false);

    public Task<bool> MoveNodeAfterAsync(
        BookmarkNode node,
        BookmarkNode target) =>
        MoveNodeRelativeAsync(
            node,
            target,
            insertAfter: true);

    private async Task<bool> MoveNodeRelativeAsync(
        BookmarkNode node,
        BookmarkNode target,
        bool insertAfter)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(target);

        await _documentOperationGate
            .WaitAsync()
            .ConfigureAwait(true);
        try
        {
            EnsurePositionalBookmarkMoveAllowed();

            var document = RequireEditableDocument();
            var result = insertAfter
                ? _moveService.MoveNodeAfter(
                    document,
                    node,
                    target)
                : _moveService.MoveNodeBefore(
                    document,
                    node,
                    target);

            if (!result.Changed)
            {
                return false;
            }

            RecordHistory(
                new BookmarkMoveHistoryEntry(
                    node,
                    result));

            await RefreshProjectionsAfterMoveAsync(
                    preferredFolder: result.TargetParent,
                    preferredBookmark: node as BookmarkUrl)
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

    public void NavigateToSearchResult(BookmarkNode? node)
    {
        if (!IsSearchActive ||
            node is null ||
            !SearchResults.Any(result => ReferenceEquals(result, node)))
        {
            return;
        }

        if (node is BookmarkFolder folder)
        {
            if (!NavigateToFolder(folder))
            {
                return;
            }

            SearchText = string.Empty;
            return;
        }

        if (node is not BookmarkUrl bookmark ||
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

    public bool NavigateToFolder(BookmarkFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        if (!_folderLookup.TryGetValue(folder, out var targetFolder))
        {
            return false;
        }

        var ancestor = targetFolder.Parent;
        while (ancestor is not null)
        {
            ancestor.IsExpanded = true;
            ancestor = ancestor.Parent;
        }

        SelectFolder(targetFolder);
        return true;
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

        ClearContentSelection();
        _selectedFolderItem = item;

        if (item is null)
        {
            SetSelectedFolder(null);
            SetSelectedBookmark(null);
            SetCurrentItems(Array.Empty<BookmarkListItemViewModel>());
            SetCurrentBookmarks(Array.Empty<BookmarkUrl>());
            SetSelectionSummaryText(string.Empty);
            RerunSearchForFolderChange();
            return;
        }

        item.IsSelected = true;
        SetSelectedFolder(item.Folder);
        SetSelectedBookmark(null);

        var items = item.Folder.Children
            .Select(node => new BookmarkListItemViewModel(node))
            .ToArray();
        var bookmarks = item.Folder.Children
            .OfType<BookmarkUrl>()
            .ToArray();
        var folderCount = item.Folder.Children
            .Count(node => node is BookmarkFolder);

        SetCurrentItems(items);
        SetCurrentBookmarks(bookmarks);
        SetSelectionSummaryText(
            $"{item.Folder.Name} | " +
            $"{FormatCount(folderCount, "folder")} | " +
            $"{FormatCount(bookmarks.Length, "bookmark")}");

        RerunSearchForFolderChange();
    }

    internal Task WaitForPendingSearchAsync() => _pendingSearchTask;

    internal async Task RefreshProjectionsAfterEditAsync(
        BookmarkFolder? preferredFolder = null,
        BookmarkUrl? preferredBookmark = null,
        CancellationToken cancellationToken = default)
    {
        if (_document is null || !CanEditDocument)
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
        SetSearchResults(Array.Empty<BookmarkNode>());
        SetIsSearchBusy(false);
        SetSearchSummaryText(
            searchWasActive ? "Refreshing search index..." : string.Empty);

        var refreshedIndex = await _searchService
            .BuildIndexAsync(document, cancellationToken)
            .ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();

        if (!ReferenceEquals(_document, document) || !CanEditDocument)
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
        if (_document is null || !CanEditDocument)
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
        SetSearchResults(Array.Empty<BookmarkNode>());
        SetIsSearchBusy(false);
        SetSearchSummaryText(
            searchWasActive ? "Refreshing search..." : string.Empty);

        cancellationToken.ThrowIfCancellationRequested();

        if (!ReferenceEquals(_document, document) || !CanEditDocument)
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
        if (_document is null || !CanEditDocument)
        {
            throw new InvalidOperationException(
                State == DocumentState.RecoveryRequired
                    ? "The Bookmarks source requires recovery or reload before editing or saving again."
                    : "An editable bookmark document is not loaded.");
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
        OnPropertyChanged(nameof(CanSortSelectedFolder));
        OnPropertyChanged(nameof(CanRenameSelectedFolder));
        OnPropertyChanged(nameof(CanRenameSelectedContentFolder));
        OnPropertyChanged(nameof(CanMoveSelectedContentFolder));
        OnPropertyChanged(nameof(CanDeleteSelectedContentFolder));
        OnPropertyChanged(nameof(CanRenameSelectedBookmark));
        OnPropertyChanged(nameof(CanEditSelectedBookmarkUrl));
        OnPropertyChanged(nameof(CanMoveSelectedBookmark));
        OnPropertyChanged(nameof(CanMoveSelectedFolder));
        OnPropertyChanged(nameof(CanDeleteSelectedBookmarks));
        OnPropertyChanged(nameof(CanDeleteSelectedFolder));
        OnPropertyChanged(nameof(CanMoveSelectedBookmarks));
        OnPropertyChanged(nameof(CanRenameSelectedContentBookmark));
        OnPropertyChanged(nameof(CanEditSelectedContentBookmarkUrl));
        OnPropertyChanged(nameof(CanMoveSelectedContentBookmark));
        OnPropertyChanged(nameof(CanDeleteSelectedContentBookmarks));
        OnPropertyChanged(nameof(CanDeleteSelectedContentItems));
        OnPropertyChanged(nameof(CanMoveSelectedContentBookmarks));
        OnPropertyChanged(nameof(CanMoveSelectedContentItems));
        OnPropertyChanged(nameof(CanCopySelectedContentItems));
        OnPropertyChanged(nameof(CanCutSelectedContentItems));
        OnPropertyChanged(nameof(CanPasteClipboard));
        OnPropertyChanged(nameof(CanOpenSelectedContentItem));
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
        ClearContentSelection();
        SetCurrentItems(Array.Empty<BookmarkListItemViewModel>());
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
        ClearContentSelection();
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(DisplayedBookmarks));
        OnPropertyChanged(nameof(DisplayedItems));

        if (string.IsNullOrWhiteSpace(value))
        {
            CancelPendingSearch();
            SetSearchResults(Array.Empty<BookmarkNode>());
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
        ClearContentSelection();
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
            SetSearchResults(Array.Empty<BookmarkNode>());
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
                SetSearchResults(Array.Empty<BookmarkNode>());
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

        SetSearchResults(Array.Empty<BookmarkNode>());
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
        OnPropertyChanged(nameof(CanExportBookmarksHtml));
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

    private void SetCurrentItems(
        IReadOnlyList<BookmarkListItemViewModel> value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (ReferenceEquals(_currentItems, value))
        {
            return;
        }

        _currentItems = value;
        OnPropertyChanged(nameof(CurrentItems));

        if (!IsSearchActive)
        {
            OnPropertyChanged(nameof(DisplayedItems));
        }
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

    private void SetSelectedContentItem(
        BookmarkListItemViewModel? value)
    {
        if (ReferenceEquals(_selectedContentItem, value))
        {
            return;
        }

        _selectedContentItem = value;
        OnPropertyChanged(nameof(SelectedContentItem));
        OnPropertyChanged(nameof(SelectedContentFolder));
        NotifyEditingAvailabilityChanged();
    }

    private void SetSelectedContentItems(
        IReadOnlyList<BookmarkListItemViewModel> value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (ReferenceSequenceEqualContentItems(
                _selectedContentItems,
                value))
        {
            return;
        }

        _selectedContentItems = value;
        OnPropertyChanged(nameof(SelectedContentItems));
        OnPropertyChanged(nameof(SelectedContentFolder));
        NotifyEditingAvailabilityChanged();
    }

    private bool HasUnambiguousSelectedBookmark()
    {
        if (SelectedBookmark is null ||
            HasMultipleSelectedBookmarks)
        {
            return false;
        }

        return _selectedContentItems.Count switch
        {
            0 => true,
            1 => _selectedContentItems[0].Node is BookmarkUrl,
            _ => false
        };
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

    private void ClearContentSelection()
    {
        SetSelectedContentItems(
            Array.Empty<BookmarkListItemViewModel>());
        SetSelectedContentItem(null);
        ClearBookmarkSelection();
    }

    private static bool ReferenceSequenceEqualContentItems(
        IReadOnlyList<BookmarkListItemViewModel> left,
        IReadOnlyList<BookmarkListItemViewModel> right)
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

    private static string FormatCount(int count, string singularNoun)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(singularNoun);

        return
            $"{count.ToString("N0", CultureInfo.InvariantCulture)} " +
            $"{singularNoun}{(count == 1 ? string.Empty : "s")}";
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

    private void SetSearchResults(IReadOnlyList<BookmarkNode> value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (ReferenceEquals(_searchResults, value))
        {
            return;
        }

        _searchResults = value;
        _searchBookmarks = value
            .OfType<BookmarkUrl>()
            .ToArray();
        _searchItems = value
            .Select(node => new BookmarkListItemViewModel(node))
            .ToArray();

        OnPropertyChanged(nameof(SearchResults));

        if (IsSearchActive)
        {
            OnPropertyChanged(nameof(DisplayedBookmarks));
            OnPropertyChanged(nameof(DisplayedItems));
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
