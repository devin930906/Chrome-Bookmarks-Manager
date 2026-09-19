using ChromeBookmarksManager.Application;

namespace ChromeBookmarksManager.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public string ApplicationTitle => "Chrome Bookmarks Manager";

    public DocumentState State { get; } = DocumentState.NoDocument;

    public string StatusText => "No Bookmarks file is open.";
}
