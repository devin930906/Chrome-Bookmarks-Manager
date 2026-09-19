using System.Windows;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
