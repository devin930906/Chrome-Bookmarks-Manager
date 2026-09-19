using System.Windows;
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
}
