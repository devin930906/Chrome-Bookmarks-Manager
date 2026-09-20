using System.Diagnostics;
using System.Globalization;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private static readonly TimeSpan DefaultSearchDebounce =
        TimeSpan.FromMilliseconds(250);

    private readonly IChromeBookmarksReader _reader;
    private readonly IBookmarkSearchService _searchService;
    private readonly IBookmarkEditingService _editingService;
    private readonly TimeSpan _searchDebounce;
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _searchCancellation;
    private Task _pendingSearchTask = Task.CompletedTask;
    private long _searchGeneration;
    private BookmarkDocument? _document;
    private BookmarkSearchIndex? _searchIndex;
    private string? _sourcePath;
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
            searchDebounce)
    {
    }

    internal MainViewModel(
        IChromeBookmarksReader reader,
        IBookmarkSearchService searchService,
        IBookmarkEditingService editingService,
        TimeSpan searchDebounce)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _searchService = searchService
            ?? throw new ArgumentNullException(nameof(searchService));
        _editingService = editingService
            ?? throw new ArgumentNullException(nameof(editingService));

        if (searchDebounce < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(searchDebounce),
                searchDebounce,
                "Search debounce cannot be negative.");
        }

        _searchDebounce = searchDebounce;
    }

    public string ApplicationTitle => "Chrome Bookmarks Manager";

    public DocumentState State => _state;

    public BookmarkDocument? Document => _document;

    public string? SourcePath => _sourcePath;

    public string StatusText => _statusText;

    public IReadOnlyList<FolderTreeItemViewModel> FolderRoots => _folderRoots;

    public BookmarkFolder? SelectedFolder => _selectedFolder;

    public IReadOnlyList<BookmarkUrl> CurrentBookmarks => _currentBookmarks;

    public BookmarkUrl? SelectedBookmark
    {
        get => _selectedBookmark;
        set => SetSelectedBookmark(value);
    }

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

    public bool IsDirty => State == DocumentState.LoadedDirty;

    public bool CanAddBookmark =>
        CanBrowseDocument && SelectedFolder is not null;

    public bool CanAddFolder =>
        CanBrowseDocument && SelectedFolder is not null;

    public bool CanRenameSelectedFolder =>
        CanBrowseDocument &&
        SelectedFolder is not null &&
        !IsPermanentRoot(SelectedFolder);

    public bool CanRenameSelectedBookmark =>
        CanBrowseDocument && SelectedBookmark is not null;

    public bool CanEditSelectedBookmarkUrl =>
        CanBrowseDocument && SelectedBookmark is not null;

    public bool CanOpenBookmarks =>
        State is not DocumentState.Loading and not DocumentState.Saving;

    public bool CanCancelLoad => State == DocumentState.Loading;

    public bool CanBrowseDocument =>
        State is DocumentState.LoadedClean or DocumentState.LoadedDirty;

    public async Task LoadBookmarksAsync(
        string path,
        bool discardDirtyChanges = false)
    {
        if (_loadCancellation is not null || State == DocumentState.Loading)
        {
            throw new InvalidOperationException(
                "A Bookmarks file is already being loaded.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (State == DocumentState.LoadedDirty &&
            !discardDirtyChanges)
        {
            throw new InvalidOperationException(
                "The current Bookmarks document has unsaved in-memory changes. " +
                "Confirm Discard before loading another file.");
        }

        ResetSearchState(clearIndex: true, resetScope: true);
        SetDocument(null);
        SetSourcePath(null);
        ClearBrowserState();
        SetState(DocumentState.Loading);
        SetStatusText("Reading Bookmarks...");

        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var document = await _reader
                .ReadFileAsync(path, cancellation.Token)
                .ConfigureAwait(true);

            SetStatusText("Building search index...");

            var searchIndex = await _searchService
                .BuildIndexAsync(document, cancellation.Token)
                .ConfigureAwait(true);

            cancellation.Token.ThrowIfCancellationRequested();

            stopwatch.Stop();
            SetSearchIndex(searchIndex);
            SetDocument(document);
            SetSourcePath(path);
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
            ResetSearchState(clearIndex: true, resetScope: true);
            SetDocument(null);
            SetSourcePath(null);
            ClearBrowserState();
            SetState(DocumentState.NoDocument);
            SetStatusText("Loading was canceled.");
        }
        catch (ChromeBookmarksReadException exception)
        {
            stopwatch.Stop();
            ResetSearchState(clearIndex: true, resetScope: true);
            SetDocument(null);
            SetSourcePath(null);
            ClearBrowserState();
            SetState(DocumentState.LoadFailed);
            SetStatusText(exception.Message);
        }
        catch (Exception)
        {
            stopwatch.Stop();
            ResetSearchState(clearIndex: true, resetScope: true);
            SetDocument(null);
            SetSourcePath(null);
            ClearBrowserState();
            SetState(DocumentState.LoadFailed);
            SetStatusText("Loading failed while preparing search.");
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
            }

            cancellation.Dispose();
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
        var document = RequireEditableDocument();
        var parent = SelectedFolder
            ?? throw new InvalidOperationException(
                "Select a folder before adding a bookmark.");

        var bookmark = _editingService.AddBookmark(
            document,
            parent,
            name,
            url);

        MarkDirty();
        await RefreshProjectionsAfterEditAsync(
                preferredFolder: parent,
                preferredBookmark: bookmark)
            .ConfigureAwait(true);

        return bookmark;
    }

    public async Task<BookmarkFolder> AddFolderAsync(string name)
    {
        var document = RequireEditableDocument();
        var parent = SelectedFolder
            ?? throw new InvalidOperationException(
                "Select a folder before adding a folder.");

        var folder = _editingService.AddFolder(
            document,
            parent,
            name);

        MarkDirty();
        await RefreshProjectionsAfterEditAsync(
                preferredFolder: folder)
            .ConfigureAwait(true);

        return folder;
    }

    public async Task<bool> RenameSelectedFolderAsync(string newName)
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

        var changed = _editingService.RenameNode(
            document,
            folder,
            newName);

        if (!changed)
        {
            return false;
        }

        MarkDirty();
        await RefreshProjectionsAfterEditAsync(
                preferredFolder: folder)
            .ConfigureAwait(true);

        return true;
    }

    public async Task<bool> RenameSelectedBookmarkAsync(string newName)
    {
        var document = RequireEditableDocument();
        var bookmark = SelectedBookmark
            ?? throw new InvalidOperationException(
                "Select a bookmark before renaming it.");

        var changed = _editingService.RenameNode(
            document,
            bookmark,
            newName);

        if (!changed)
        {
            return false;
        }

        MarkDirty();
        await RefreshProjectionsAfterEditAsync(
                preferredBookmark: bookmark)
            .ConfigureAwait(true);

        return true;
    }

    public async Task<bool> EditSelectedBookmarkUrlAsync(string newUrl)
    {
        var document = RequireEditableDocument();
        var bookmark = SelectedBookmark
            ?? throw new InvalidOperationException(
                "Select a bookmark before editing its URL.");

        var changed = _editingService.EditUrl(
            document,
            bookmark,
            newUrl);

        if (!changed)
        {
            return false;
        }

        MarkDirty();
        await RefreshProjectionsAfterEditAsync(
                preferredBookmark: bookmark)
            .ConfigureAwait(true);

        return true;
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
        SetSelectedBookmark(null);
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
        NotifyEditingAvailabilityChanged();
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
