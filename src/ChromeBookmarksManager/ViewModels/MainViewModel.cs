using System.Diagnostics;
using System.Globalization;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IChromeBookmarksReader _reader;
    private CancellationTokenSource? _loadCancellation;
    private BookmarkDocument? _document;
    private string? _sourcePath;
    private DocumentState _state = DocumentState.NoDocument;
    private string _statusText = "No Bookmarks file is open.";
    private IReadOnlyList<FolderTreeItemViewModel> _folderRoots =
        Array.Empty<FolderTreeItemViewModel>();
    private FolderTreeItemViewModel? _selectedFolderItem;
    private BookmarkFolder? _selectedFolder;
    private IReadOnlyList<BookmarkUrl> _currentBookmarks =
        Array.Empty<BookmarkUrl>();
    private BookmarkUrl? _selectedBookmark;
    private string _documentSummaryText = string.Empty;
    private string _selectionSummaryText = string.Empty;

    public MainViewModel(IChromeBookmarksReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
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

    public bool CanOpenBookmarks =>
        State is not DocumentState.Loading and not DocumentState.Saving;

    public bool CanCancelLoad => State == DocumentState.Loading;

    public bool CanBrowseDocument =>
        State is DocumentState.LoadedClean or DocumentState.LoadedDirty;

    public async Task LoadBookmarksAsync(string path)
    {
        if (_loadCancellation is not null || State == DocumentState.Loading)
        {
            throw new InvalidOperationException(
                "A Bookmarks file is already being loaded.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(path);

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

            stopwatch.Stop();
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
            SetDocument(null);
            SetSourcePath(null);
            ClearBrowserState();
            SetState(DocumentState.NoDocument);
            SetStatusText("Loading was canceled.");
        }
        catch (ChromeBookmarksReadException exception)
        {
            stopwatch.Stop();
            SetDocument(null);
            SetSourcePath(null);
            ClearBrowserState();
            SetState(DocumentState.LoadFailed);
            SetStatusText(exception.Message);
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
    }

    private void BuildBrowserState(BookmarkDocument document)
    {
        var roots = new[]
        {
            new FolderTreeItemViewModel(document.Roots.BookmarkBar),
            new FolderTreeItemViewModel(document.Roots.Other),
            new FolderTreeItemViewModel(document.Roots.Synced)
        };

        SetFolderRoots(roots);
        SetDocumentSummaryText(
            $"{document.UrlCount.ToString("N0", CultureInfo.InvariantCulture)} URLs | " +
            $"{document.FolderCount.ToString("N0", CultureInfo.InvariantCulture)} folders");
        SelectFolder(roots[0]);
    }

    private void ClearBrowserState()
    {
        if (_selectedFolderItem is not null)
        {
            _selectedFolderItem.IsSelected = false;
        }

        _selectedFolderItem = null;
        SetFolderRoots(Array.Empty<FolderTreeItemViewModel>());
        SetSelectedFolder(null);
        SetSelectedBookmark(null);
        SetCurrentBookmarks(Array.Empty<BookmarkUrl>());
        SetDocumentSummaryText(string.Empty);
        SetSelectionSummaryText(string.Empty);
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
    }

    private void SetCurrentBookmarks(IReadOnlyList<BookmarkUrl> value)
    {
        if (ReferenceEquals(_currentBookmarks, value))
        {
            return;
        }

        _currentBookmarks = value;
        OnPropertyChanged(nameof(CurrentBookmarks));
    }

    private void SetSelectedBookmark(BookmarkUrl? value)
    {
        if (ReferenceEquals(_selectedBookmark, value))
        {
            return;
        }

        _selectedBookmark = value;
        OnPropertyChanged(nameof(SelectedBookmark));
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
}
