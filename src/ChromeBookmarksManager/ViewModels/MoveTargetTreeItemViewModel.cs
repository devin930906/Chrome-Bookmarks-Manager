using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.ViewModels;

public sealed class MoveTargetTreeItemViewModel : ViewModelBase
{
    private bool _isExpanded;
    private bool _isSelected;

    private MoveTargetTreeItemViewModel(
        BookmarkFolder folder,
        bool isValidTarget,
        IReadOnlyList<MoveTargetTreeItemViewModel> children)
    {
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
        IsValidTarget = isValidTarget;
        Children = children ?? throw new ArgumentNullException(nameof(children));
    }

    public BookmarkFolder Folder { get; }

    public string Name => Folder.Name;

    public IReadOnlyList<MoveTargetTreeItemViewModel> Children { get; }

    public bool IsValidTarget { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public static IReadOnlyList<MoveTargetTreeItemViewModel> BuildRoots(
        BookmarkDocument document,
        BookmarkNode movingNode)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(movingNode);

        return new[]
        {
            Build(document.Roots.BookmarkBar, movingNode),
            Build(document.Roots.Other, movingNode),
            Build(document.Roots.Synced, movingNode)
        };
    }

    private static MoveTargetTreeItemViewModel Build(
        BookmarkFolder folder,
        BookmarkNode movingNode)
    {
        var children = folder.Children
            .OfType<BookmarkFolder>()
            .Select(child => Build(child, movingNode))
            .ToArray();

        var isValidTarget =
            movingNode is not BookmarkFolder movingFolder ||
            !ReferenceEquals(folder, movingFolder) &&
            !IsDescendantOf(folder, movingFolder);

        return new MoveTargetTreeItemViewModel(
            folder,
            isValidTarget,
            children);
    }

    private static bool IsDescendantOf(
        BookmarkFolder candidate,
        BookmarkFolder ancestor)
    {
        BookmarkFolder? current = candidate.Parent;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }
}
