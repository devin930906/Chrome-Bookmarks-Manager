using System.Diagnostics;
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

    public MainViewModel(IChromeBookmarksReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public string ApplicationTitle => "Chrome Bookmarks Manager";

    public DocumentState State => _state;

    public BookmarkDocument? Document => _document;

    public string? SourcePath => _sourcePath;

    public string StatusText => _statusText;

    public bool CanOpenBookmarks =>
        State is not DocumentState.Loading and not DocumentState.Saving;

    public bool CanCancelLoad => State == DocumentState.Loading;

    public async Task LoadBookmarksAsync(string path)
    {
        if (_loadCancellation is not null || State == DocumentState.Loading)
        {
            throw new InvalidOperationException("A Bookmarks file is already being loaded.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        SetDocument(null);
        SetSourcePath(null);
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
            SetState(DocumentState.LoadedClean);
            SetStatusText(
                $"Loaded {document.UrlCount:N0} URLs and {document.FolderCount:N0} folders in {stopwatch.Elapsed.TotalSeconds:F1}s.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            stopwatch.Stop();
            SetDocument(null);
            SetSourcePath(null);
            SetState(DocumentState.NoDocument);
            SetStatusText("Loading was canceled.");
        }
        catch (ChromeBookmarksReadException exception)
        {
            stopwatch.Stop();
            SetDocument(null);
            SetSourcePath(null);
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
}
