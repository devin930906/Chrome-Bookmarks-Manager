using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.ViewModels;
using Microsoft.Win32;

namespace ChromeBookmarksManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new ChromeBookmarksReader());
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.IsDirty)
        {
            return;
        }

        e.Cancel = !ConfirmDiscardChanges();
    }

    private bool ConfirmDiscardChanges()
    {
        var dialog = new DiscardChangesDialog
        {
            Owner = this
        };

        return dialog.ShowDialog() == true;
    }

    private async void OpenBookmarks_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            FileName = "Bookmarks",
            Filter = "Chrome Bookmarks file|Bookmarks|JSON files|*.json|All files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (ViewModel.IsDirty)
        {
            if (!ConfirmDiscardChanges())
            {
                return;
            }

            await ViewModel.LoadBookmarksAsync(
                dialog.FileName,
                discardDirtyChanges: true);
            return;
        }

        await ViewModel.LoadBookmarksAsync(dialog.FileName);
    }

    private void CancelLoad_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelLoad();
    }

    private void FolderTree_SelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        ViewModel.SelectFolder(e.NewValue as FolderTreeItemViewModel);
    }

    private void FolderTree_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var item = FindVisualParent<TreeViewItem>(
            e.OriginalSource as DependencyObject);

        if (item is not null)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void BookmarksList_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var item = FindVisualParent<ListViewItem>(
            e.OriginalSource as DependencyObject);

        if (item is not null)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void BookmarksList_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (ViewModel.IsSearchActive &&
            ViewModel.SelectedBookmark is { } bookmark)
        {
            ViewModel.NavigateToSearchResult(bookmark);
        }
    }

    private void BookmarksList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter &&
            ViewModel.IsSearchActive &&
            ViewModel.SelectedBookmark is { } bookmark)
        {
            ViewModel.NavigateToSearchResult(bookmark);
            e.Handled = true;
        }
    }

    private async void AddBookmark_Click(object sender, RoutedEventArgs e)
    {
        await AddBookmarkFromUiAsync();
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        await AddFolderFromUiAsync();
    }

    private async void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        await RenameFolderFromUiAsync();
    }

    private async void RenameBookmark_Click(object sender, RoutedEventArgs e)
    {
        await RenameBookmarkFromUiAsync();
    }

    private async void EditUrl_Click(object sender, RoutedEventArgs e)
    {
        await EditUrlFromUiAsync();
    }

    private async Task AddBookmarkFromUiAsync()
    {
        if (!ViewModel.CanAddBookmark)
        {
            return;
        }

        var dialog = CreateEditDialog(
            "Add Bookmark",
            name: string.Empty,
            url: string.Empty,
            showName: true,
            showUrl: true,
            requireUrl: true);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await ViewModel.AddBookmarkAsync(
                dialog.NameValue,
                dialog.UrlValue);
        }
        catch (BookmarkEditException exception)
        {
            ShowEditError(exception);
        }
    }

    private async Task AddFolderFromUiAsync()
    {
        if (!ViewModel.CanAddFolder)
        {
            return;
        }

        var dialog = CreateEditDialog(
            "Add Folder",
            name: string.Empty,
            url: null,
            showName: true,
            showUrl: false,
            requireUrl: false);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await ViewModel.AddFolderAsync(dialog.NameValue);
        }
        catch (BookmarkEditException exception)
        {
            ShowEditError(exception);
        }
    }

    private async Task RenameFolderFromUiAsync()
    {
        if (!ViewModel.CanRenameSelectedFolder ||
            ViewModel.SelectedFolder is not { } folder)
        {
            return;
        }

        var dialog = CreateEditDialog(
            "Rename Folder",
            name: folder.Name,
            url: null,
            showName: true,
            showUrl: false,
            requireUrl: false);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await ViewModel.RenameSelectedFolderAsync(dialog.NameValue);
        }
        catch (BookmarkEditException exception)
        {
            ShowEditError(exception);
        }
    }

    private async Task RenameBookmarkFromUiAsync()
    {
        if (!ViewModel.CanRenameSelectedBookmark ||
            ViewModel.SelectedBookmark is not { } bookmark)
        {
            return;
        }

        var dialog = CreateEditDialog(
            "Rename Bookmark",
            name: bookmark.Name,
            url: null,
            showName: true,
            showUrl: false,
            requireUrl: false);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await ViewModel.RenameSelectedBookmarkAsync(dialog.NameValue);
        }
        catch (BookmarkEditException exception)
        {
            ShowEditError(exception);
        }
    }

    private async Task EditUrlFromUiAsync()
    {
        if (!ViewModel.CanEditSelectedBookmarkUrl ||
            ViewModel.SelectedBookmark is not { } bookmark)
        {
            return;
        }

        var dialog = CreateEditDialog(
            "Edit Bookmark URL",
            name: bookmark.Name,
            url: bookmark.Url,
            showName: false,
            showUrl: true,
            requireUrl: true);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await ViewModel.EditSelectedBookmarkUrlAsync(dialog.UrlValue);
        }
        catch (BookmarkEditException exception)
        {
            ShowEditError(exception);
        }
    }

    private BookmarkEditDialog CreateEditDialog(
        string title,
        string name,
        string? url,
        bool showName,
        bool showUrl,
        bool requireUrl)
    {
        var dialog = new BookmarkEditDialog
        {
            Owner = this
        };

        dialog.Configure(
            title,
            name,
            url,
            showName,
            showUrl,
            requireUrl);

        return dialog;
    }

    private void ShowEditError(BookmarkEditException exception)
    {
        MessageBox.Show(
            this,
            exception.Message,
            "Bookmark editing",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;

        if (e.Key == Key.B &&
            modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (ViewModel.CanAddBookmark)
            {
                await AddBookmarkFromUiAsync();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.F &&
            modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (ViewModel.CanAddFolder)
            {
                await AddFolderFromUiAsync();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.E &&
            modifiers == ModifierKeys.Control)
        {
            if (ViewModel.CanEditSelectedBookmarkUrl)
            {
                await EditUrlFromUiAsync();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2 &&
            modifiers == ModifierKeys.None)
        {
            if (BookmarksList.IsKeyboardFocusWithin &&
                ViewModel.CanRenameSelectedBookmark)
            {
                await RenameBookmarkFromUiAsync();
                e.Handled = true;
                return;
            }

            if (FolderTree.IsKeyboardFocusWithin &&
                ViewModel.CanRenameSelectedFolder)
            {
                await RenameFolderFromUiAsync();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.F &&
            modifiers.HasFlag(ModifierKeys.Control))
        {
            if (ViewModel.CanSearchDocument)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && ViewModel.IsSearchActive)
        {
            ViewModel.SearchText = string.Empty;
            SearchBox.Focus();
            e.Handled = true;
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? source)
        where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
