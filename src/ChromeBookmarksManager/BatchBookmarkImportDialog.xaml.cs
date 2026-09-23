using System.Windows;
using ChromeBookmarksManager.Application.Importing;
using ChromeBookmarksManager.Localization;
using Microsoft.Win32;

namespace ChromeBookmarksManager;

public partial class BatchBookmarkImportDialog : Window
{
    public BatchBookmarkImportDialog()
    {
        InitializeComponent();
    }

    public IReadOnlyList<BookmarkTextImportItem> Items { get; private set; } =
        Array.Empty<BookmarkTextImportItem>();

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        UrlsBox.Focus();
    }

    private async void ImportTxt_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationService.GetString("DialogBatchAddBookmarks"),
            CheckFileExists = true,
            Multiselect = false,
            Filter = LocalizationService.GetString("BatchImportTxtFilter")
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            UrlsBox.Text = await File.ReadAllTextAsync(dialog.FileName);
            ValidationText.Text = string.Empty;
            UrlsBox.Focus();
            UrlsBox.CaretIndex = UrlsBox.Text.Length;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                LocalizationService.Format(
                    "BatchImportTxtFailed",
                    exception.Message),
                LocalizationService.GetString("DialogBatchAddBookmarks"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var result = BookmarkTextImportService.Parse(UrlsBox.Text);

        if (result.Errors.Count > 0)
        {
            var visibleLines = result.Errors
                .Take(8)
                .Select(error => error.LineNumber.ToString())
                .ToArray();
            var lineText = string.Join(", ", visibleLines);
            if (result.Errors.Count > visibleLines.Length)
            {
                lineText += ", ...";
            }

            ValidationText.Text = LocalizationService.Format(
                "BatchImportInvalidLines",
                lineText);
            UrlsBox.Focus();
            return;
        }

        if (result.Items.Count == 0)
        {
            ValidationText.Text =
                LocalizationService.GetString("BatchImportNoUrls");
            UrlsBox.Focus();
            return;
        }

        Items = result.Items.ToArray();
        ValidationText.Text = string.Empty;
        DialogResult = true;
    }
}
