using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.DragDrop;
using ChromeBookmarksManager.ViewModels;
using Microsoft.Win32;

namespace ChromeBookmarksManager;

public partial class MainWindow : Window
{
    private Point? _bookmarkDragStartPoint;
    private BookmarkUrl? _bookmarkDragCandidate;
    private Point? _folderDragStartPoint;
    private BookmarkFolder? _folderDragCandidate;
    private ListViewItem? _bookmarkDropIndicatorItem;
    private TreeViewItem? _folderDropIndicatorItem;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new ChromeBookmarksReader());
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.IsDirty)
        {
            return;
        }

        e.Cancel = !ConfirmDiscardChanges();
    }

    private bool ConfirmDiscardChanges()
    {
        var dialog = new DiscardChangesDialog
        {
            Owner = this
        };

        return dialog.ShowDialog() == true;
    }

    private async void OpenBookmarks_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            FileName = "Bookmarks",
            Filter = "Chrome Bookmarks file|Bookmarks|JSON files|*.json|All files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (ViewModel.IsDirty)
        {
            if (!ConfirmDiscardChanges())
            {
                return;
            }

            await ViewModel.LoadBookmarksAsync(
                dialog.FileName,
                discardDirtyChanges: true);
            return;
        }

        await ViewModel.LoadBookmarksAsync(dialog.FileName);
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

    private void BookmarksList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.UpdateSelectedBookmarks(
            BookmarksList.SelectedItems.OfType<BookmarkUrl>());
    }

    private void FolderTree_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var item = FindVisualParent<TreeViewItem>(
            e.OriginalSource as DependencyObject);

        if (item?.DataContext is FolderTreeItemViewModel folderItem &&
            DragDropRules.CanStartFolderDrag(
                folderItem.Folder.Parent is null))
        {
            item.IsSelected = true;
            item.Focus();
            _folderDragCandidate = folderItem.Folder;
            _folderDragStartPoint = e.GetPosition(FolderTree);
            return;
        }

        ResetFolderDragSource();
    }

    private void FolderTree_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            _folderDragCandidate is not { } folder ||
            _folderDragStartPoint is not { } startPoint)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                ResetFolderDragSource();
            }

            return;
        }

        var currentPoint = e.GetPosition(FolderTree);
        var deltaX = currentPoint.X - startPoint.X;
        var deltaY = currentPoint.Y - startPoint.Y;

        if (!DragDropRules.HasExceededDragThreshold(
                deltaX,
                deltaY,
                SystemParameters.MinimumHorizontalDragDistance,
                SystemParameters.MinimumVerticalDragDistance))
        {
            return;
        }

        var payload = new BookmarkDragPayload(folder);
        ResetFolderDragSource();

        try
        {
            System.Windows.DragDrop.DoDragDrop(
                FolderTree,
                payload,
                DragDropEffects.Move);
        }
        finally
        {
            ClearBookmarkDropIndicator();
            ClearFolderDropIndicator();
        }
    }

    private void FolderTree_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var item = FindVisualParent<TreeViewItem>(
            e.OriginalSource as DependencyObject);

        if (item is not null)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void BookmarksList_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var item = FindVisualParent<ListViewItem>(
            e.OriginalSource as DependencyObject);

        if (item?.DataContext is BookmarkUrl bookmark)
        {
            _bookmarkDragCandidate = bookmark;
            _bookmarkDragStartPoint = e.GetPosition(BookmarksList);
            return;
        }

        ResetBookmarkDragSource();
    }

    private void BookmarksList_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            _bookmarkDragCandidate is not { } bookmark ||
            _bookmarkDragStartPoint is not { } startPoint)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                ResetBookmarkDragSource();
            }

            return;
        }

        var currentPoint = e.GetPosition(BookmarksList);
        var deltaX = currentPoint.X - startPoint.X;
        var deltaY = currentPoint.Y - startPoint.Y;

        if (!DragDropRules.HasExceededDragThreshold(
                deltaX,
                deltaY,
                SystemParameters.MinimumHorizontalDragDistance,
                SystemParameters.MinimumVerticalDragDistance))
        {
            return;
        }

        ViewModel.SelectedBookmark = bookmark;
        var payload = new BookmarkDragPayload(bookmark);
        ResetBookmarkDragSource();

        try
        {
            System.Windows.DragDrop.DoDragDrop(
                BookmarksList,
                payload,
                DragDropEffects.Move);
        }
        finally
        {
            ClearBookmarkDropIndicator();
            ClearFolderDropIndicator();
        }
    }

    private void BookmarksList_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var item = FindVisualParent<ListViewItem>(
            e.OriginalSource as DependencyObject);

        if (item is not null)
        {
            if (!item.IsSelected)
            {
                BookmarksList.UnselectAll();
                item.IsSelected = true;
            }

            item.Focus();
        }
    }

    private void BookmarksList_DragOver(
        object sender,
        DragEventArgs e)
    {
        ClearBookmarkDropIndicator();
        e.Effects = DragDropEffects.None;

        if (!DragDropRules.CanPositionallyReorderBookmarks(
                ViewModel.IsSearchActive) ||
            GetBookmarkDragPayload(e)?.Node is not BookmarkUrl bookmark)
        {
            e.Handled = true;
            return;
        }

        var targetItem = FindVisualParent<ListViewItem>(
            e.OriginalSource as DependencyObject);

        if (targetItem?.DataContext is not BookmarkUrl target ||
            ReferenceEquals(bookmark, target) ||
            targetItem.ActualHeight <= 0)
        {
            e.Handled = true;
            return;
        }

        var pointer = e.GetPosition(targetItem);
        var placement = DragDropRules.GetBookmarkRowPlacement(
            pointer.Y,
            targetItem.ActualHeight);

        SetBookmarkDropIndicator(targetItem, placement);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void BookmarksList_DragLeave(
        object sender,
        DragEventArgs e)
    {
        ClearBookmarkDropIndicator();
    }

    private async void BookmarksList_Drop(
        object sender,
        DragEventArgs e)
    {
        ClearBookmarkDropIndicator();
        e.Effects = DragDropEffects.None;

        if (!DragDropRules.CanPositionallyReorderBookmarks(
                ViewModel.IsSearchActive) ||
            GetBookmarkDragPayload(e)?.Node is not BookmarkUrl bookmark)
        {
            e.Handled = true;
            return;
        }

        var targetItem = FindVisualParent<ListViewItem>(
            e.OriginalSource as DependencyObject);

        if (targetItem?.DataContext is not BookmarkUrl target ||
            ReferenceEquals(bookmark, target) ||
            targetItem.ActualHeight <= 0)
        {
            e.Handled = true;
            return;
        }

        var pointer = e.GetPosition(targetItem);
        var placement = DragDropRules.GetBookmarkRowPlacement(
            pointer.Y,
            targetItem.ActualHeight);

        try
        {
            if (placement == DropPlacement.Before)
            {
                await ViewModel.MoveBookmarkBeforeAsync(bookmark, target);
            }
            else
            {
                await ViewModel.MoveBookmarkAfterAsync(bookmark, target);
            }

            e.Effects = DragDropEffects.Move;
        }
        catch (BookmarkMoveException exception)
        {
            ShowMoveError(exception);
        }

        e.Handled = true;
    }

    private void FolderTree_DragOver(
        object sender,
        DragEventArgs e)
    {
        ClearFolderDropIndicator();
        e.Effects = DragDropEffects.None;

        var draggedNode = GetBookmarkDragPayload(e)?.Node;
        var targetItem = FindVisualParent<TreeViewItem>(
            e.OriginalSource as DependencyObject);

        if (draggedNode is null ||
            targetItem?.DataContext is not FolderTreeItemViewModel target)
        {
            e.Handled = true;
            return;
        }

        if (draggedNode is BookmarkUrl)
        {
            SetFolderDropIndicator(targetItem, DropPlacement.Into);
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (draggedNode is not BookmarkFolder movingFolder ||
            targetItem.ActualHeight <= 0)
        {
            e.Handled = true;
            return;
        }

        var placement = DragDropRules.GetFolderRowPlacement(
            e.GetPosition(targetItem).Y,
            targetItem.ActualHeight,
            target.Folder.Parent is null);

        var valid = placement == DropPlacement.Into
            ? DragDropRules.CanMoveFolderInto(
                movingFolder,
                target.Folder)
            : DragDropRules.CanMoveFolderRelativeTo(
                movingFolder,
                target.Folder);

        if (!valid)
        {
            e.Handled = true;
            return;
        }

        SetFolderDropIndicator(targetItem, placement);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void FolderTree_DragLeave(
        object sender,
        DragEventArgs e)
    {
        ClearFolderDropIndicator();
    }

    private async void FolderTree_Drop(
        object sender,
        DragEventArgs e)
    {
        ClearFolderDropIndicator();
        e.Effects = DragDropEffects.None;

        var draggedNode = GetBookmarkDragPayload(e)?.Node;
        var targetItem = FindVisualParent<TreeViewItem>(
            e.OriginalSource as DependencyObject);

        if (draggedNode is null ||
            targetItem?.DataContext is not FolderTreeItemViewModel target)
        {
            e.Handled = true;
            return;
        }

        try
        {
            bool changed;

            if (draggedNode is BookmarkUrl bookmark)
            {
                changed = await ViewModel.MoveBookmarkToEndAsync(
                    bookmark,
                    target.Folder);
            }
            else if (draggedNode is BookmarkFolder movingFolder &&
                     targetItem.ActualHeight > 0)
            {
                var placement = DragDropRules.GetFolderRowPlacement(
                    e.GetPosition(targetItem).Y,
                    targetItem.ActualHeight,
                    target.Folder.Parent is null);

                var valid = placement == DropPlacement.Into
                    ? DragDropRules.CanMoveFolderInto(
                        movingFolder,
                        target.Folder)
                    : DragDropRules.CanMoveFolderRelativeTo(
                        movingFolder,
                        target.Folder);

                if (!valid)
                {
                    e.Handled = true;
                    return;
                }

                changed = placement switch
                {
                    DropPlacement.Before =>
                        await ViewModel.MoveFolderBeforeAsync(
                            movingFolder,
                            target.Folder),
                    DropPlacement.After =>
                        await ViewModel.MoveFolderAfterAsync(
                            movingFolder,
                            target.Folder),
                    _ =>
                        await ViewModel.MoveFolderToEndAsync(
                            movingFolder,
                            target.Folder)
                };
            }
            else
            {
                e.Handled = true;
                return;
            }

            e.Effects = changed
                ? DragDropEffects.Move
                : DragDropEffects.None;
        }
        catch (BookmarkMoveException exception)
        {
            ShowMoveError(exception);
        }

        e.Handled = true;
    }

    private void BookmarksList_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (ViewModel.IsSearchActive &&
            ViewModel.SelectedBookmark is { } bookmark)
        {
            ViewModel.NavigateToSearchResult(bookmark);
        }
    }

    private void BookmarksList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter &&
            ViewModel.IsSearchActive &&
            ViewModel.SelectedBookmark is { } bookmark)
        {
            ViewModel.NavigateToSearchResult(bookmark);
            e.Handled = true;
        }
    }

    private async void AddBookmark_Click(object sender, RoutedEventArgs e)
    {
        await AddBookmarkFromUiAsync();
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        await AddFolderFromUiAsync();
    }

    private async void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        await RenameFolderFromUiAsync();
    }

    private async void RenameBookmark_Click(object sender, RoutedEventArgs e)
    {
        await RenameBookmarkFromUiAsync();
    }

    private async void EditUrl_Click(object sender, RoutedEventArgs e)
    {
        await EditUrlFromUiAsync();
    }

    private async void MoveFolder_Click(object sender, RoutedEventArgs e)
    {
        await MoveFolderFromUiAsync();
    }

    private async void MoveBookmark_Click(object sender, RoutedEventArgs e)
    {
        await MoveBookmarkFromUiAsync();
    }

    private async void DeleteSelectedBookmarks_Click(
        object sender,
        RoutedEventArgs e)
    {
        await DeleteSelectedBookmarksFromUiAsync();
    }

    private async void DeleteSelectedFolder_Click(
        object sender,
        RoutedEventArgs e)
    {
        await DeleteSelectedFolderFromUiAsync();
    }

    private async Task AddBookmarkFromUiAsync()
    {
        if (!ViewModel.CanAddBookmark)
        {
            return;
        }

        var dialog = CreateEditDialog(
            "Add Bookmark",
            name: string.Empty,
            url: string.Empty,
            showName: true,
            showUrl: true,
            requireUrl: true);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await ViewModel.AddBookmarkAsync(
                dialog.NameValue,
                dialog.UrlValue);
        }
        catch (BookmarkEditException exception)
        {
            ShowEditError(exception);
        }
    }

    private async Task AddFolderFromUiAsync()
    {
        if (!ViewModel.CanAddFolder)
        {
            return;
        }

        var dialog = CreateEditDialog(
            "Add Folder",
            name: string.Empty,
            url: null,
            showName: true,
            showUrl: false,
            requireUrl: false);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await ViewModel.AddFolderAsync(dialog.NameValue);
        }
        catch (BookmarkEditException exception)
        {
            ShowEditError(exception);
        }
    }

    private async Task RenameFolderFromUiAsync()
    {
        if (!ViewModel.CanRenameSelectedFolder ||
            ViewModel.SelectedFolder is not { } folder)
        {
            return;
        }

        var dialog = CreateEditDialog(
            "Rename Folder",
            name: folder.Name,
            url: null,
            showName: true,
            showUrl: false,
            requireUrl: false);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await ViewModel.RenameSelectedFolderAsync(dialog.NameValue);
        }
        catch (BookmarkEditException exception)
        {
            ShowEditError(exception);
        }
    }

    private async Task RenameBookmarkFromUiAsync()
    {
        if (!ViewModel.CanRenameSelectedBookmark ||
            ViewModel.SelectedBookmark is not { } bookmark)
        {
            return;
        }

        var dialog = CreateEditDialog(
            "Rename Bookmark",
            name: bookmark.Name,
            url: null,
            showName: true,
            showUrl: false,
            requireUrl: false);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await ViewModel.RenameSelectedBookmarkAsync(dialog.NameValue);
        }
        catch (BookmarkEditException exception)
        {
            ShowEditError(exception);
        }
    }

    private async Task EditUrlFromUiAsync()
    {
        if (!ViewModel.CanEditSelectedBookmarkUrl ||
            ViewModel.SelectedBookmark is not { } bookmark)
        {
            return;
        }

        var dialog = CreateEditDialog(
            "Edit Bookmark URL",
            name: bookmark.Name,
            url: bookmark.Url,
            showName: false,
            showUrl: true,
            requireUrl: true);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await ViewModel.EditSelectedBookmarkUrlAsync(dialog.UrlValue);
        }
        catch (BookmarkEditException exception)
        {
            ShowEditError(exception);
        }
    }

    private async Task MoveFolderFromUiAsync()
    {
        if (!ViewModel.CanMoveSelectedFolder ||
            ViewModel.SelectedFolder is not { } folder ||
            ViewModel.Document is not { } document)
        {
            return;
        }

        var dialog = CreateMoveDialog(document, folder);
        if (dialog.ShowDialog() != true ||
            dialog.SelectedTarget is not { } target)
        {
            return;
        }

        try
        {
            await ViewModel.MoveFolderToEndAsync(folder, target);
        }
        catch (BookmarkMoveException exception)
        {
            ShowMoveError(exception);
        }
    }

    private async Task MoveBookmarkFromUiAsync()
    {
        var selectedBookmarks = GetSelectedBookmarks();

        if (!ViewModel.CanMoveSelectedBookmarks ||
            selectedBookmarks.Count == 0 ||
            ViewModel.Document is not { } document)
        {
            return;
        }

        var dialog = CreateMoveDialog(document, selectedBookmarks[0]);
        if (dialog.ShowDialog() != true ||
            dialog.SelectedTarget is not { } target)
        {
            return;
        }

        try
        {
            if (selectedBookmarks.Count == 1)
            {
                await ViewModel.MoveBookmarkToEndAsync(
                    selectedBookmarks[0],
                    target);
            }
            else
            {
                await ViewModel.MoveSelectedBookmarksToEndAsync(target);
            }
        }
        catch (BookmarkMoveException exception)
        {
            ShowMoveError(exception);
        }
    }

    private MoveNodeDialog CreateMoveDialog(
        BookmarkDocument document,
        BookmarkNode node)
    {
        var dialog = new MoveNodeDialog
        {
            Owner = this
        };

        dialog.Configure(document, node);
        return dialog;
    }

    private BookmarkEditDialog CreateEditDialog(
        string title,
        string name,
        string? url,
        bool showName,
        bool showUrl,
        bool requireUrl)
    {
        var dialog = new BookmarkEditDialog
        {
            Owner = this
        };

        dialog.Configure(
            title,
            name,
            url,
            showName,
            showUrl,
            requireUrl);

        return dialog;
    }

    private void ShowEditError(BookmarkEditException exception)
    {
        MessageBox.Show(
            this,
            exception.Message,
            "Bookmark editing",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void ShowMoveError(BookmarkMoveException exception)
    {
        MessageBox.Show(
            this,
            exception.Message,
            "Move bookmark item",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void ShowDeleteError(BookmarkDeleteException exception)
    {
        MessageBox.Show(
            this,
            exception.Message,
            "Delete bookmark item",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private async Task DeleteSelectedBookmarksFromUiAsync()
    {
        if (!ViewModel.CanDeleteSelectedBookmarks)
        {
            return;
        }

        var selectedBookmarks = GetSelectedBookmarks();
        if (selectedBookmarks.Count == 0)
        {
            return;
        }

        var targetDescription = selectedBookmarks.Count == 1
            ? $"Delete bookmark \"{selectedBookmarks[0].Name}\"?"
            : $"Delete {selectedBookmarks.Count:N0} selected bookmarks?";
        var removalSubject = selectedBookmarks.Count == 1
            ? "This bookmark"
            : "These bookmarks";
        var confirmation =
            $"{targetDescription}\n\n" +
            $"{removalSubject} will be removed from the currently loaded in-memory document only.\n" +
            "V0.7 has no Save or write-back path; the source Chrome Bookmarks file remains unchanged.";

        if (!ConfirmDestructiveOperation(
                "Confirm bookmark deletion",
                confirmation))
        {
            return;
        }

        try
        {
            await ViewModel.DeleteSelectedBookmarksAsync();
        }
        catch (BookmarkDeleteException exception)
        {
            ShowDeleteError(exception);
        }
    }

    private async Task DeleteSelectedFolderFromUiAsync()
    {
        if (!ViewModel.CanDeleteSelectedFolder ||
            ViewModel.SelectedFolder is not { } folder)
        {
            return;
        }

        var (bookmarkCount, folderCount) = CountFolderDescendants(folder);
        var bookmarkSummary = bookmarkCount == 1
            ? "1 bookmark"
            : $"{bookmarkCount:N0} bookmarks";
        var folderSummary = folderCount == 1
            ? "1 nested folder"
            : $"{folderCount:N0} nested folders";
        var confirmation =
            $"Delete folder \"{folder.Name}\" and its entire subtree?\n\n" +
            $"This includes {bookmarkSummary} and {folderSummary}.\n\n" +
            "The subtree will be removed from the currently loaded in-memory document only.\n" +
            "V0.7 has no Save or write-back path; the source Chrome Bookmarks file remains unchanged.";

        if (!ConfirmDestructiveOperation(
                "Confirm folder deletion",
                confirmation))
        {
            return;
        }

        try
        {
            await ViewModel.DeleteSelectedFolderAsync();
        }
        catch (BookmarkDeleteException exception)
        {
            ShowDeleteError(exception);
        }
    }

    private bool ConfirmDestructiveOperation(
        string title,
        string message)
    {
        return MessageBox.Show(
                this,
                message,
                title,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel) == MessageBoxResult.Yes;
    }

    private IReadOnlyList<BookmarkUrl> GetSelectedBookmarks()
    {
        if (ViewModel.SelectedBookmarks.Count > 0)
        {
            return ViewModel.SelectedBookmarks;
        }

        return ViewModel.SelectedBookmark is { } bookmark
            ? new[] { bookmark }
            : Array.Empty<BookmarkUrl>();
    }

    private static (int BookmarkCount, int FolderCount)
        CountFolderDescendants(BookmarkFolder folder)
    {
        var bookmarkCount = 0;
        var folderCount = 0;
        var pending = new Stack<BookmarkNode>(folder.Children);

        while (pending.TryPop(out var descendant))
        {
            switch (descendant)
            {
                case BookmarkUrl:
                    bookmarkCount++;
                    break;

                case BookmarkFolder childFolder:
                    folderCount++;
                    foreach (var child in childFolder.Children)
                    {
                        pending.Push(child);
                    }

                    break;
            }
        }

        return (bookmarkCount, folderCount);
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;

        if (e.Key == Key.A &&
            modifiers == ModifierKeys.Control &&
            BookmarksList.IsKeyboardFocusWithin)
        {
            BookmarksList.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete &&
            modifiers == ModifierKeys.None)
        {
            if (BookmarksList.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                if (ViewModel.CanDeleteSelectedBookmarks)
                {
                    await DeleteSelectedBookmarksFromUiAsync();
                }

                return;
            }

            if (FolderTree.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                if (ViewModel.CanDeleteSelectedFolder)
                {
                    await DeleteSelectedFolderFromUiAsync();
                }

                return;
            }
        }

        if (e.Key == Key.B &&
            modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (ViewModel.CanAddBookmark)
            {
                await AddBookmarkFromUiAsync();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.F &&
            modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (ViewModel.CanAddFolder)
            {
                await AddFolderFromUiAsync();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.E &&
            modifiers == ModifierKeys.Control)
        {
            if (ViewModel.CanEditSelectedBookmarkUrl)
            {
                await EditUrlFromUiAsync();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2 &&
            modifiers == ModifierKeys.None)
        {
            if (BookmarksList.IsKeyboardFocusWithin &&
                ViewModel.CanRenameSelectedBookmark)
            {
                await RenameBookmarkFromUiAsync();
                e.Handled = true;
                return;
            }

            if (FolderTree.IsKeyboardFocusWithin &&
                ViewModel.CanRenameSelectedFolder)
            {
                await RenameFolderFromUiAsync();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.F &&
            modifiers.HasFlag(ModifierKeys.Control))
        {
            if (ViewModel.CanSearchDocument)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && ViewModel.IsSearchActive)
        {
            ViewModel.SearchText = string.Empty;
            SearchBox.Focus();
            e.Handled = true;
        }
    }

    private static BookmarkDragPayload? GetBookmarkDragPayload(
        DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(BookmarkDragPayload)))
        {
            return null;
        }

        return e.Data.GetData(typeof(BookmarkDragPayload))
            as BookmarkDragPayload;
    }

    private void SetBookmarkDropIndicator(
        ListViewItem item,
        DropPlacement placement)
    {
        ClearBookmarkDropIndicator();

        item.BorderBrush = SystemColors.HighlightBrush;
        item.BorderThickness = placement == DropPlacement.Before
            ? new Thickness(0, 2, 0, 0)
            : new Thickness(0, 0, 0, 2);

        _bookmarkDropIndicatorItem = item;
    }

    private void ClearBookmarkDropIndicator()
    {
        if (_bookmarkDropIndicatorItem is null)
        {
            return;
        }

        _bookmarkDropIndicatorItem.ClearValue(Control.BorderBrushProperty);
        _bookmarkDropIndicatorItem.ClearValue(Control.BorderThicknessProperty);
        _bookmarkDropIndicatorItem = null;
    }

    private void SetFolderDropIndicator(
        TreeViewItem item,
        DropPlacement placement)
    {
        ClearFolderDropIndicator();

        item.BorderBrush = SystemColors.HighlightBrush;
        item.BorderThickness = placement switch
        {
            DropPlacement.Before => new Thickness(0, 2, 0, 0),
            DropPlacement.After => new Thickness(0, 0, 0, 2),
            _ => new Thickness(1)
        };
        _folderDropIndicatorItem = item;
    }

    private void ClearFolderDropIndicator()
    {
        if (_folderDropIndicatorItem is null)
        {
            return;
        }

        _folderDropIndicatorItem.ClearValue(Control.BorderBrushProperty);
        _folderDropIndicatorItem.ClearValue(Control.BorderThicknessProperty);
        _folderDropIndicatorItem = null;
    }

    private void ResetBookmarkDragSource()
    {
        _bookmarkDragStartPoint = null;
        _bookmarkDragCandidate = null;
    }

    private void ResetFolderDragSource()
    {
        _folderDragStartPoint = null;
        _folderDragCandidate = null;
    }

    private static T? FindVisualParent<T>(DependencyObject? source)
        where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
