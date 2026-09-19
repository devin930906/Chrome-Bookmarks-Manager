using System.Windows;

namespace ChromeBookmarksManager;

public partial class BookmarkEditDialog : Window
{
    private bool _requireUrl;
    private bool _showName;
    private bool _showUrl;

    public BookmarkEditDialog()
    {
        InitializeComponent();
    }

    public string NameValue => NameBox.Text;

    public string UrlValue => UrlBox.Text;

    public void Configure(
        string title,
        string name,
        string? url,
        bool showName,
        bool showUrl,
        bool requireUrl)
    {
        Title = title;
        NameBox.Text = name;
        UrlBox.Text = url ?? string.Empty;
        _showName = showName;
        _showUrl = showUrl;
        _requireUrl = requireUrl;

        NamePanel.Visibility =
            showName ? Visibility.Visible : Visibility.Collapsed;
        UrlPanel.Visibility =
            showUrl ? Visibility.Visible : Visibility.Collapsed;
        ValidationText.Text = string.Empty;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (_showName)
        {
            NameBox.Focus();
            NameBox.SelectAll();
        }
        else if (_showUrl)
        {
            UrlBox.Focus();
            UrlBox.SelectAll();
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (_requireUrl &&
            string.IsNullOrWhiteSpace(UrlBox.Text))
        {
            ValidationText.Text = "URL cannot be empty.";
            UrlBox.Focus();
            UrlBox.SelectAll();
            return;
        }

        ValidationText.Text = string.Empty;
        DialogResult = true;
    }
}
