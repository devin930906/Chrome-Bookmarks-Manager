using System.Xml.Linq;

namespace ChromeBookmarksManager.Tests.Ui;

public sealed class WorkspaceLayoutContractTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void Workspace_keeps_virtualized_views_and_adds_theme_details_and_bulk_controls()
    {
        var document = XDocument.Load(Source("MainWindow.xaml"));
        var nodes = document.Descendants().ToArray();
        foreach (var name in new[] { "ThemeToggleButton", "DetailsPanel", "DetailsColumn", "BulkActionsBar", "FolderTree", "BookmarksList", "SearchBox" })
            Assert.Contains(nodes, e => (string?)e.Attribute(X + "Name") == name);

        foreach (var name in new[] { "FolderTree", "BookmarksList" })
        {
            var control = Assert.Single(nodes, e => (string?)e.Attribute(X + "Name") == name);
            Assert.Equal("True", (string?)control.Attribute("VirtualizingPanel.IsVirtualizing"));
            Assert.Equal("Recycling", (string?)control.Attribute("VirtualizingPanel.VirtualizationMode"));
        }
        Assert.Single(nodes, e => e.Name.LocalName == "ListView");
        Assert.Contains(nodes, e => (string?)e.Attribute("Click") == "ToggleTheme_Click");
        Assert.Contains(nodes, e => (string?)e.Attribute("Click") == "ShowAppearance_Click");
    }

    [Fact]
    public void Workspace_keeps_all_existing_editing_and_navigation_handlers_reachable()
    {
        var nodes = XDocument.Load(Source("MainWindow.xaml")).Descendants().ToArray();
        foreach (var handler in new[] {
            "OpenBookmarks_Click", "ImportBookmarksHtml_Click", "ExportBookmarksHtml_Click", "Save_Click", "CancelLoad_Click",
            "Undo_Click", "Redo_Click", "CutSelectedContentItems_Click", "CopySelectedContentItems_Click", "PasteClipboard_Click",
            "AddBookmark_Click", "AddBookmarksBatch_Click", "AddFolder_Click", "SortSelectedFolder_Click", "RenameFolder_Click",
            "RenameBookmark_Click", "EditUrl_Click", "DeleteSelectedContentItems_Click", "MoveSelectedContentItems_Click",
            "OpenContentFolder_Click", "OpenContentBookmark_Click", "SwitchToSimplifiedChinese_Click", "SwitchToEnglish_Click" })
            Assert.Contains(nodes, e => (string?)e.Attribute("Click") == handler);

        Assert.Equal("1180", (string?)XDocument.Load(Source("MainWindow.xaml")).Root?.Attribute("Width"));
        Assert.Equal("900", (string?)XDocument.Load(Source("MainWindow.xaml")).Root?.Attribute("MinWidth"));
    }

    private static string Source(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "src", "ChromeBookmarksManager", file);
            if (File.Exists(path)) return path;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(file);
    }
}
