using System.Collections.ObjectModel;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.ViewModels;

public sealed class FolderTreeItemViewModel : ViewModelBase
{
    private bool _isExpanded;
    private bool _isSelected;

    public FolderTreeItemViewModel(
        BookmarkFolder folder,
        FolderTreeItemViewModel? parent = null)
    {
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
        Parent = parent;

        Children = new ReadOnlyCollection<FolderTreeItemViewModel>(
            folder.Children
                .OfType<BookmarkFolder>()
                .Select(child => new FolderTreeItemViewModel(child, this))
                .ToList());
    }

    public BookmarkFolder Folder { get; }

    public FolderTreeItemViewModel? Parent { get; }

    public string Name => Folder.Name;

    public IReadOnlyList<FolderTreeItemViewModel> Children { get; }

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
}
