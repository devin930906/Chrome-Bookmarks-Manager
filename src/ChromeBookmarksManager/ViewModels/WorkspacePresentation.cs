using System.ComponentModel;
using System.IO;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.Localization;
using ChromeBookmarksManager.Theming;

namespace ChromeBookmarksManager.ViewModels;

/// <summary>
/// Presentation-only state for the workspace. It observes the existing document
/// view model; changing appearance never replaces the document or its history.
/// Paths walk only the selected node's parents, not the document's subtrees.
/// </summary>
public sealed class WorkspacePresentation : ViewModelBase, IDisposable
{
    private MainViewModel? _viewModel;
    private bool _detailsVisible = true;

    public string FolderTitle => _viewModel?.SelectedFolder?.Name
        ?? L("WorkspaceBrand");
    public string FolderPath => PathOf(_viewModel?.SelectedFolder?.Parent);
    public string SourceFileName => string.IsNullOrWhiteSpace(_viewModel?.SourcePath)
        ? L("WorkspaceNoFile")
        : Path.GetFileName(_viewModel.SourcePath);
    public string DisplayedSummary => LocalizationService.Format(
        "WorkspaceDisplayedCount", _viewModel?.DisplayedItems.Count ?? 0);
    public string SelectedSummary => LocalizationService.Format(
        "WorkspaceSelectedCount", _viewModel?.SelectedContentItems.Count ?? 0);
    public bool HasMultipleSelection => _viewModel?.SelectedContentItems.Count > 1;
    public bool IsLoading => _viewModel?.State == DocumentState.Loading;
    public bool IsEmptyDocument => _viewModel?.Document is null && !IsLoading;
    public bool HasEmptyList => _viewModel?.CanBrowseDocument == true
        && _viewModel.DisplayedItems.Count == 0 && !_viewModel.IsSearchBusy;
    public string EmptyListMessage => L(_viewModel?.IsSearchActive == true
        ? "WorkspaceNoResults" : "WorkspaceEmptyFolder");
    public bool IsDetailsClosed => !_detailsVisible;
    public string ThemeDescription => L(ThemeService.CurrentTheme == "light"
        ? "ThemeLight" : "ThemeDark") + " · " + L("ThemeToggleHint");

    private BookmarkListItemViewModel? SingleSelection =>
        _viewModel?.SelectedContentItems.Count == 1
            ? _viewModel.SelectedContentItems[0] : null;

    public string DetailTitle => HasMultipleSelection ? SelectedSummary
        : SingleSelection?.Name ?? L("WorkspaceChooseItem");
    public string DetailUrl => HasMultipleSelection ? L("WorkspaceBatchHint")
        : SingleSelection?.IsFolder == true ? L("PanelFolders")
        : SingleSelection?.Url ?? string.Empty;
    public string DetailPath => HasMultipleSelection ? L("WorkspaceBatchLocation")
        : PathOf(SingleSelection?.Node.Parent);
    public string DetailGlyph => HasMultipleSelection ? "≡"
        : SingleSelection?.IsFolder == true ? "▱" : "↗";
    public bool CanRenameDetail => SingleSelection?.IsFolder == true
        ? _viewModel?.CanRenameSelectedContentFolder == true
        : _viewModel?.CanRenameSelectedBookmark == true;

    public void Attach(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (ReferenceEquals(_viewModel, viewModel)) return;
        if (_viewModel is not null) _viewModel.PropertyChanged -= ViewModelChanged;
        _viewModel = viewModel;
        _viewModel.PropertyChanged += ViewModelChanged;
        Refresh();
    }

    public void SetDetailsVisible(bool visible)
    {
        if (_detailsVisible == visible) return;
        _detailsVisible = visible;
        OnPropertyChanged(nameof(IsDetailsClosed));
    }

    public void Refresh() => OnPropertyChanged(null);

    private void ViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(MainViewModel.State)
            or nameof(MainViewModel.Document) or nameof(MainViewModel.SourcePath)
            or nameof(MainViewModel.SelectedFolder) or nameof(MainViewModel.DisplayedItems)
            or nameof(MainViewModel.SelectedContentItem) or nameof(MainViewModel.SelectedContentItems)
            or nameof(MainViewModel.IsSearchActive) or nameof(MainViewModel.IsSearchBusy)
            or nameof(MainViewModel.CanRenameSelectedContentFolder)
            or nameof(MainViewModel.CanRenameSelectedBookmark)
            or nameof(MainViewModel.SelectionSummaryText)) Refresh();
    }

    private static string PathOf(BookmarkNode? node)
    {
        if (node is null) return string.Empty;
        var names = new Stack<string>();
        for (var current = node; current is not null; current = current.Parent)
            names.Push(current.Name);
        return string.Join(" / ", names);
    }

    private static string L(string key) => LocalizationService.GetString(key);

    public void Dispose()
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= ViewModelChanged;
        _viewModel = null;
    }
}
