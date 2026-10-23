using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ChromeBookmarksManager.Theming;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager;

public partial class MainWindow
{
    public WorkspacePresentation Workspace { get; } = new();
    private bool _detailsRequested = true;
    private bool _allowNarrowDetails;
    private double _detailsWidth = 225;

    private void InitializeWorkspace()
    {
        Workspace.Attach(ViewModel);
        ThemeService.ThemeChanged += WorkspaceThemeChanged;
        SizeChanged += WorkspaceSizeChanged;
        BookmarksList.SizeChanged += WorkspaceSizeChanged;
        DataContextChanged += WorkspaceDataContextChanged;
        Closed += WorkspaceClosed;
        Loaded += (_, _) => UpdateWorkspaceLayout();
        UpdateThemeMenuChecks();
    }

    private void WorkspaceDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is MainViewModel viewModel) Workspace.Attach(viewModel);
    }

    private void WorkspaceClosed(object? sender, EventArgs e)
    {
        ThemeService.ThemeChanged -= WorkspaceThemeChanged;
        Workspace.Dispose();
    }

    private void WorkspaceThemeChanged(object? sender, EventArgs e)
    {
        Workspace.Refresh();
        UpdateThemeMenuChecks();
    }

    private void UpdateThemeMenuChecks()
    {
        LightModeMenuItem.IsChecked = ThemeService.CurrentTheme == "light";
        DarkModeMenuItem.IsChecked = !LightModeMenuItem.IsChecked;
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e) =>
        ApplyThemePreference(ThemeService.CurrentTheme == "dark" ? "light" : "dark");

    private void LightMode_Click(object sender, RoutedEventArgs e) => ApplyThemePreference("light");
    private void DarkMode_Click(object sender, RoutedEventArgs e) => ApplyThemePreference("dark");

    private void ApplyThemePreference(string theme)
    {
        ThemeService.SetTheme(theme);
        UpdateThemeMenuChecks();
        try { _userSettingsStore.SaveTheme(theme); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppMessageDialog.Show(this, L("ThemeSaveFailed"), L("MenuAppearance"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowAppearance_Click(object sender, RoutedEventArgs e) =>
        new AppearanceDialog(_userSettingsStore) { Owner = this }.ShowDialog();

    private void ShowAddMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.DataContext = DataContext;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void ToggleDetails_Click(object sender, RoutedEventArgs e)
    {
        var visible = DetailsPanel.Visibility == Visibility.Visible;
        _detailsRequested = !visible;
        _allowNarrowDetails = !visible;
        UpdateWorkspaceLayout();
    }

    private void WorkspaceSizeChanged(object sender, SizeChangedEventArgs e) => UpdateWorkspaceLayout();

    private void UpdateWorkspaceLayout()
    {
        if (DetailsColumn is null || BookmarksList is null) return;
        var show = _detailsRequested && (ActualWidth >= 1080 || _allowNarrowDetails);
        if (!show && DetailsColumn.ActualWidth > 0) _detailsWidth = DetailsColumn.ActualWidth;
        if (show && DetailsColumn.Width.Value == 0) DetailsColumn.Width = new GridLength(_detailsWidth);
        if (!show) DetailsColumn.Width = new GridLength(0);
        DetailsSplitterColumn.Width = new GridLength(show ? 5 : 0);
        DetailsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        DetailsSplitter.Visibility = DetailsPanel.Visibility;
        Workspace.SetDetailsVisible(show);
        var available = BookmarksList.ActualWidth - 58;
        if (available > 180)
        {
            var nameWidth = Math.Max(120, available * .46);
            var urlWidth = Math.Max(110, available - nameWidth);
            // Do not trigger another layout when sizes have not actually changed.
            if (Math.Abs(ContentNameColumn.Width - nameWidth) > 1) ContentNameColumn.Width = nameWidth;
            if (Math.Abs(ContentUrlColumn.Width - urlWidth) > 1) ContentUrlColumn.Width = urlWidth;
        }
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e) => BookmarksList.UnselectAll();

    private void RenameDetail_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedContentFolder is not null) RenameContentFolder_Click(sender, e);
        else RenameBookmark_Click(sender, e);
    }
}
