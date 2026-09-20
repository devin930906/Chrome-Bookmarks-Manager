using System.Windows;

namespace ChromeBookmarksManager;

public partial class DiscardChangesDialog : Window
{
    public DiscardChangesDialog()
    {
        InitializeComponent();
    }

    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
