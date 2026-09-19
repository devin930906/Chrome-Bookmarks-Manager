using System.Windows;
using System.Windows.Input;
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

    private async void OpenBookmarks_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            FileName = "Bookmarks",
            Filter = "Chrome Bookmarks file|Bookmarks|JSON files|*.json|All files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await ViewModel.LoadBookmarksAsync(dialog.FileName);
        }
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

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F &&
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
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
}
