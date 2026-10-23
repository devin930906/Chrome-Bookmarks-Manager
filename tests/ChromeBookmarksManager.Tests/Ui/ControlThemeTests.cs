using System.Xml.Linq;

namespace ChromeBookmarksManager.Tests.Ui;

public sealed class ControlThemeTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly IReadOnlyDictionary<string, (string[] Names, string[] Handlers)> DialogContracts =
        new Dictionary<string, (string[] Names, string[] Handlers)>(StringComparer.Ordinal)
        {
            ["BookmarkEditDialog.xaml"] = (
                ["NamePanel", "NameBox", "UrlPanel", "UrlBox", "ValidationText"],
                ["Ok_Click"]),
            ["BatchBookmarkImportDialog.xaml"] = (
                ["UrlsBox", "ValidationText"],
                ["ImportTxt_Click", "Ok_Click"]),
            ["MoveNodeDialog.xaml"] = (
                ["TargetTree", "MoveButton"],
                ["TargetTree_SelectedItemChanged", "Move_Click"]),
            ["DiscardChangesDialog.xaml"] = (
                [],
                ["Discard_Click"])
        };

    [Fact]
    public void SharedControlDictionary_ConsumesThemeBrushesAndDeclaresRequiredStyles()
    {
        var path = Path.Combine(RepositoryRoot, "src", "ChromeBookmarksManager", "Themes", "Controls.xaml");
        Assert.True(File.Exists(path), $"Shared controls dictionary is missing: {path}");

        var document = XDocument.Load(path);
        var markup = File.ReadAllText(path);
        var requiredBrushes = new[]
        {
            "AppBackgroundBrush", "SidebarBackgroundBrush", "SurfaceBrush", "HoverBrush",
            "PrimaryTextBrush", "SecondaryTextBrush", "MutedTextBrush", "BorderBrush",
            "AccentBrush", "AccentForegroundBrush", "SelectionBrush", "FocusBrush",
            "SuccessBrush", "WarningBrush", "DangerBrush", "DisabledTextBrush"
        };
        foreach (var brush in requiredBrushes)
        {
            Assert.Contains($"{{DynamicResource {brush}}}", markup, StringComparison.Ordinal);
        }

        var requiredKeyedStyles = new[]
        {
            "PrimaryButtonStyle", "DangerButtonStyle", "QuietButtonStyle", "IconButtonStyle",
            "WorkspaceWindowStyle", "WorkspaceTreeItemStyle", "WorkspaceListItemStyle"
        };
        var keyedStyleNames = document.Descendants(PresentationNamespace + "Style")
            .Select(style => (string?)style.Attribute(XamlNamespace + "Key"))
            .Where(key => key is not null)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var style in requiredKeyedStyles)
        {
            Assert.Contains(style, keyedStyleNames);
        }

        var requiredImplicitTargets = new[]
        {
            "Button", "TextBox", "ComboBox", "ComboBoxItem", "Menu", "MenuItem", "ContextMenu",
            "Popup", "ToolTip", "TreeView", "TreeViewItem", "ListView", "ListViewItem", "GridViewColumnHeader",
            "ScrollBar", "Thumb", "Separator", "StatusBar", "StatusBarItem", "GridSplitter",
            "TextBlock", "CheckBox", "ProgressBar", "GroupBox"
        };
        var implicitTargets = document.Descendants(PresentationNamespace + "Style")
            .Where(style => style.Attribute(XamlNamespace + "Key") is null)
            .Select(style => (string?)style.Attribute("TargetType"))
            .Where(target => target is not null)
            .Select(target => target!.Replace("{x:Type ", string.Empty, StringComparison.Ordinal).TrimEnd('}'))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var target in requiredImplicitTargets)
        {
            Assert.Contains(target, implicitTargets);
        }

        Assert.Contains("PART_ContentHost", markup, StringComparison.Ordinal);
        Assert.Contains("PART_EditableTextBox", markup, StringComparison.Ordinal);
        Assert.Contains("PART_Popup", markup, StringComparison.Ordinal);
        Assert.Contains("<ItemsPresenter", markup, StringComparison.Ordinal);
        Assert.Contains("VirtualizingPanel.IsVirtualizing", markup, StringComparison.Ordinal);
        Assert.Contains("VirtualizingPanel.VirtualizationMode", markup, StringComparison.Ordinal);
        Assert.Contains("ScrollViewer.CanContentScroll", markup, StringComparison.Ordinal);
        Assert.Contains("IsMouseOver", markup, StringComparison.Ordinal);
        Assert.Contains("IsSelected", markup, StringComparison.Ordinal);
        Assert.Contains("IsKeyboardFocusWithin", markup, StringComparison.Ordinal);
        Assert.Contains("IsEnabled", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedTemplates_KeepEditableComboAndProgressPartsAndRespectGroupBoxChrome()
    {
        var path = Path.Combine(RepositoryRoot, "src", "ChromeBookmarksManager", "Themes", "Controls.xaml");
        Assert.True(File.Exists(path), $"Shared controls dictionary is missing: {path}");

        var document = XDocument.Load(path);
        var markup = File.ReadAllText(path);
        Assert.Contains("PART_ContentHost", markup, StringComparison.Ordinal);
        Assert.Contains("PART_EditableTextBox", markup, StringComparison.Ordinal);
        Assert.Contains("IsReadOnly=\"{TemplateBinding IsReadOnly}\"", markup, StringComparison.Ordinal);
        Assert.Contains("PART_Popup", markup, StringComparison.Ordinal);
        Assert.Contains("IsCheckable", markup, StringComparison.Ordinal);
        Assert.Contains("IsChecked", markup, StringComparison.Ordinal);
        Assert.Contains("IsPressed", markup, StringComparison.Ordinal);

        var progressStyle = Assert.Single(document.Descendants(PresentationNamespace + "Style"), style =>
            (string?)style.Attribute("TargetType") == "{x:Type ProgressBar}");
        var progressParts = progressStyle.Descendants()
            .Select(element => (string?)element.Attribute(XamlNamespace + "Name"))
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("PART_Track", progressParts);
        Assert.Contains("PART_Indicator", progressParts);
        Assert.Contains(progressStyle.Descendants(PresentationNamespace + "Trigger"), trigger =>
            (string?)trigger.Attribute("Property") == "IsIndeterminate");

        var groupBoxStyle = Assert.Single(document.Descendants(PresentationNamespace + "Style"), style =>
            (string?)style.Attribute("TargetType") == "{x:Type GroupBox}");
        Assert.Contains(groupBoxStyle.Descendants(PresentationNamespace + "Border"), border =>
            (string?)border.Attribute("BorderThickness") == "{TemplateBinding BorderThickness}");
    }

    [Fact]
    public void ExistingDialogs_PreserveNamesAndEventHandlers()
    {
        foreach (var (fileName, contract) in DialogContracts)
        {
            var document = LoadDialog(fileName);
            var names = document.Descendants()
                .Select(element => (string?)element.Attribute(XamlNamespace + "Name"))
                .Where(name => name is not null)
                .ToHashSet(StringComparer.Ordinal);
            var handlers = document.Descendants()
                .SelectMany(element => element.Attributes())
                .Where(attribute => attribute.Name.LocalName is "Click" or "SelectedItemChanged")
                .Select(attribute => attribute.Value)
                .ToHashSet(StringComparer.Ordinal);

            Assert.Equal(contract.Names.OrderBy(name => name), names.OrderBy(name => name));
            Assert.Equal(contract.Handlers.OrderBy(handler => handler), handlers.OrderBy(handler => handler));
        }
    }

    [Fact]
    public void ExistingDialogs_UseTheSharedWindowAndSemanticValidationResources()
    {
        foreach (var fileName in DialogContracts.Keys)
        {
            var document = LoadDialog(fileName);
            Assert.NotNull(document.Root);
            var window = document.Root!;
            Assert.Equal("{StaticResource WorkspaceWindowStyle}", (string?)window.Attribute("Style"));
            Assert.DoesNotContain("SystemColors.", File.ReadAllText(DialogPath(fileName)), StringComparison.Ordinal);
        }

        foreach (var fileName in new[] { "BookmarkEditDialog.xaml", "BatchBookmarkImportDialog.xaml" })
        {
            var document = LoadDialog(fileName);
            var validation = Assert.Single(document.Descendants(), element =>
                (string?)element.Attribute(XamlNamespace + "Name") == "ValidationText");
            Assert.Equal("{DynamicResource DangerBrush}", (string?)validation.Attribute("Foreground"));
        }

        var moveDocument = LoadDialog("MoveNodeDialog.xaml");
        var targetBorder = Assert.Single(moveDocument.Descendants(PresentationNamespace + "Border"));
        Assert.Equal("{DynamicResource BorderBrush}", (string?)targetBorder.Attribute("BorderBrush"));
    }

    [Fact]
    public void ExistingDialogs_KeepDefaultAndCancelButtonSemantics()
    {
        AssertDefaultAndCancel("BookmarkEditDialog.xaml", "Ok_Click");
        AssertDefaultAndCancel("BatchBookmarkImportDialog.xaml", "Ok_Click");

        var moveDocument = LoadDialog("MoveNodeDialog.xaml");
        var moveButton = Assert.Single(moveDocument.Descendants(PresentationNamespace + "Button"), button =>
            (string?)button.Attribute(XamlNamespace + "Name") == "MoveButton");
        Assert.Equal("True", (string?)moveButton.Attribute("IsDefault"));
        Assert.Equal("False", (string?)moveButton.Attribute("IsEnabled"));
        Assert.Equal("Move_Click", (string?)moveButton.Attribute("Click"));
        Assert.Contains(moveDocument.Descendants(PresentationNamespace + "Button"), button =>
            (string?)button.Attribute("IsCancel") == "True");

        var discardDocument = LoadDialog("DiscardChangesDialog.xaml");
        var discardButtons = discardDocument.Descendants(PresentationNamespace + "Button").ToArray();
        Assert.Equal(2, discardButtons.Length);
        Assert.Equal("True", (string?)discardButtons[0].Attribute("IsDefault"));
        Assert.Equal("True", (string?)discardButtons[0].Attribute("IsCancel"));
        Assert.Equal("Discard_Click", (string?)discardButtons[1].Attribute("Click"));
        Assert.Null(discardButtons[1].Attribute("IsDefault"));
    }

    [Fact]
    public void MoveDialog_KeepsVirtualizedTargetTreeAndBasedOnSharedItemStyle()
    {
        var document = LoadDialog("MoveNodeDialog.xaml");
        var tree = Assert.Single(document.Descendants(PresentationNamespace + "TreeView"));
        Assert.Equal("{Binding Roots}", (string?)tree.Attribute("ItemsSource"));
        Assert.Equal("True", (string?)tree.Attribute("VirtualizingPanel.IsVirtualizing"));
        Assert.Equal("Recycling", (string?)tree.Attribute("VirtualizingPanel.VirtualizationMode"));
        Assert.Equal("True", (string?)tree.Attribute("ScrollViewer.CanContentScroll"));

        var itemContainerStyle = Assert.Single(tree.Descendants(), element =>
            element.Name.LocalName.EndsWith(".ItemContainerStyle", StringComparison.Ordinal) ||
            element.Name.LocalName == "ItemContainerStyle");
        var style = Assert.Single(itemContainerStyle.Elements(PresentationNamespace + "Style"));
        Assert.Equal("{StaticResource WorkspaceTreeItemStyle}", (string?)style.Attribute("BasedOn"));
        Assert.Equal("{Binding IsValidTarget}", (string?)style.Descendants(PresentationNamespace + "Setter")
            .Single(setter => (string?)setter.Attribute("Property") == "IsEnabled")
            .Attribute("Value"));
        Assert.Contains(style.Descendants(PresentationNamespace + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "IsExpanded" &&
            (string?)setter.Attribute("Value") == "{Binding IsExpanded, Mode=TwoWay}");
        Assert.Contains(style.Descendants(PresentationNamespace + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "IsSelected" &&
            (string?)setter.Attribute("Value") == "{Binding IsSelected, Mode=TwoWay}");
    }

    private static void AssertDefaultAndCancel(string fileName, string defaultClick)
    {
        var document = LoadDialog(fileName);
        var buttons = document.Descendants(PresentationNamespace + "Button").ToArray();
        var defaultButton = Assert.Single(buttons, button =>
            (string?)button.Attribute("IsDefault") == "True");
        Assert.Equal(defaultClick, (string?)defaultButton.Attribute("Click"));
        Assert.Single(buttons, button => (string?)button.Attribute("IsCancel") == "True");
    }

    private static XDocument LoadDialog(string fileName)
    {
        var path = DialogPath(fileName);
        Assert.True(File.Exists(path), $"Dialog XAML is missing: {path}");
        return XDocument.Load(path);
    }

    private static string DialogPath(string fileName) =>
        Path.Combine(RepositoryRoot, "src", "ChromeBookmarksManager", fileName);

    private static string RepositoryRoot
    {
        get
        {
            var candidates = new[] { AppContext.BaseDirectory, Environment.CurrentDirectory };
            foreach (var candidate in candidates)
            {
                for (var directory = new DirectoryInfo(candidate); directory is not null; directory = directory.Parent)
                {
                    if (Directory.Exists(Path.Combine(directory.FullName, "src", "ChromeBookmarksManager")) &&
                        Directory.Exists(Path.Combine(directory.FullName, "tests", "ChromeBookmarksManager.Tests")))
                    {
                        return directory.FullName;
                    }
                }
            }

            throw new DirectoryNotFoundException("Could not locate the Chrome Bookmarks Manager repository root.");
        }
    }
}
