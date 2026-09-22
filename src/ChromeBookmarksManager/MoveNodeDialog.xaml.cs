using System.Windows;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.Localization;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager;

public partial class MoveNodeDialog : Window
{
    public MoveNodeDialog()
    {
        InitializeComponent();
    }

    public IReadOnlyList<MoveTargetTreeItemViewModel> Roots { get; private set; } =
        Array.Empty<MoveTargetTreeItemViewModel>();

    public BookmarkFolder? SelectedTarget { get; private set; }

    public void Configure(
        BookmarkDocument document,
        BookmarkNode movingNode)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(movingNode);

        Title = movingNode is BookmarkFolder
            ? LocalizationService.GetString("DialogMoveFolder")
            : LocalizationService.GetString("DialogMoveBookmark");

        Roots = MoveTargetTreeItemViewModel.BuildRoots(
            document,
            movingNode);

        SelectedTarget = null;
        SelectInitialTarget(Roots, movingNode.Parent);

        DataContext = this;
        MoveButton.IsEnabled = SelectedTarget is not null;
    }

    private void TargetTree_SelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        var item = e.NewValue as MoveTargetTreeItemViewModel;
        SelectedTarget = item is { IsValidTarget: true }
            ? item.Folder
            : null;
        MoveButton.IsEnabled = SelectedTarget is not null;
    }

    private void Move_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTarget is null)
        {
            return;
        }

        DialogResult = true;
    }

    private void SelectInitialTarget(
        IReadOnlyList<MoveTargetTreeItemViewModel> roots,
        BookmarkFolder? currentParent)
    {
        if (currentParent is null)
        {
            return;
        }

        foreach (var root in roots)
        {
            if (SelectInitialTarget(root, currentParent))
            {
                return;
            }
        }
    }

    private bool SelectInitialTarget(
        MoveTargetTreeItemViewModel item,
        BookmarkFolder currentParent)
    {
        if (ReferenceEquals(item.Folder, currentParent) &&
            item.IsValidTarget)
        {
            item.IsSelected = true;
            SelectedTarget = item.Folder;
            return true;
        }

        foreach (var child in item.Children)
        {
            if (!SelectInitialTarget(child, currentParent))
            {
                continue;
            }

            item.IsExpanded = true;
            return true;
        }

        return false;
    }
}
