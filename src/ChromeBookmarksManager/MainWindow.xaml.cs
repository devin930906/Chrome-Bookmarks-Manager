using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Infrastructure.Processes;
using ChromeBookmarksManager.Infrastructure.Persistence;
using ChromeBookmarksManager.Application.Saving;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.DragDrop;
using ChromeBookmarksManager.ViewModels;
using Microsoft.Win32;

namespace ChromeBookmarksManager;

public partial class MainWindow : Window
{
    private Point? _bookmarkDragStartPoint;
    private BookmarkNode? _bookmarkDragCandidate;
    private Point? _folderDragStartPoint;
    private BookmarkFolder? _folderDragCandidate;
    private ListViewItem? _bookmarkDropIndicatorItem;
    private TreeViewItem? _folderDropIndicatorItem;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();

        var reader = new ChromeBookmarksReader();
        var baselineService = new BookmarkSourceBaselineService();
        var transaction = new BookmarkFileTransaction(
            new ChromeBookmarksWriter(),
            reader,
            baselineService,
            new BookmarkFileSystem(),
            TimeProvider.System);
        var saveService = new ChromeBookmarksSaveService(
            new ChromeProcessDetector(),
            baselineService,
            transaction);

        DataContext = new MainViewModel(
            reader,
            new BookmarkSearchService(),
            baselineService,
            saveService,
            TimeSpan.FromMilliseconds(250));
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private enum SaveDiscardCancel
    {
        Save,
        Discard,
        Cancel
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (ViewModel.State == DocumentState.Loading)
        {
            _allowClose = false;
            e.Cancel = true;
            return;
        }

        if (ViewModel.State == DocumentState.Saving)
        {
            _allowClose = false;
            e.Cancel = true;
            return;
        }

        if (_allowClose)
        {
            return;
        }

        if (!ViewModel.IsDirty)
        {
            return;
        }

        e.Cancel = true;

        var decision = await PromptDirtyDocumentAsync("closing the application");
        if (decision == SaveDiscardCancel.Cancel)
        {
            return;
        }

        if (decision == SaveDiscardCancel.Save &&
            !await SaveCurrentDocumentAsync())
        {
            return;
        }

        _allowClose = true;
        Close();
    }

    private Task<SaveDiscardCancel> PromptDirtyDocumentAsync(string action)
    {
        var result = MessageBox.Show(
            this,
            $"The current Bookmarks document has unsaved changes.\n\n" +
            $"Save before {action}?\n\n" +
            "Yes = Save\nNo = Discard\nCancel = Keep editing",
            "Unsaved changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);

        return Task.FromResult(result switch
        {
            MessageBoxResult.Yes => SaveDiscardCancel.Save,
            MessageBoxResult.No => SaveDiscardCancel.Discard,
            _ => SaveDiscardCancel.Cancel
        });
    }

    private async Task<bool> SaveCurrentDocumentAsync()
    {
        try
        {
            await ViewModel.SaveAsync();
            return true;
        }
        catch (ChromeBookmarksSaveException exception)
        {
            var recovery = exception.HasVerifiedRecoveryBackup
                ? $"\n\nVerified recovery backup: {exception.BackupPath}"
                : string.Empty;

            MessageBox.Show(
                this,
                exception.Message + recovery,
                "Save failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show(
                this,
                "Saving was canceled before the replacement critical section.",
                "Save canceled",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"Saving failed unexpectedly.\n\n{exception.Message}",
                "Save failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CanSave)
        {
            await SaveCurrentDocumentAsync();
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
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

        var discardDirtyChanges = false;
        if (ViewModel.IsDirty)
        {
            var decision =
                await PromptDirtyDocumentAsync("opening another Bookmarks file");

            if (decision == SaveDiscardCancel.Cancel)
            {
                return;
            }

            if (decision == SaveDiscardCancel.Save)
            {
                if (!await SaveCurrentDocumentAsync())
                {
                    return;
                }
            }
            else
            {
                discardDirtyChanges = true;
            }
        }

        await ViewModel.LoadBookmarksAsync(
            dialog.FileName,
            discardDirtyChanges);
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

        viewModel.UpdateSelectedContentItems(
            BookmarksList.SelectedItems
                .OfType<BookmarkListItemViewModel>());
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

        if (item?.DataContext is BookmarkListItemViewModel listItem &&
            listItem.Node.Parent is not null)
        {
            if (listItem.Node is BookmarkFolder folder &&
                !DragDropRules.CanStartFolderDrag(
                    folder.Parent is null))
            {
                ResetBookmarkDragSource();
                return;
            }

            _bookmarkDragCandidate = listItem.Node;
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
            _bookmarkDragCandidate is not { } node ||
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

        if (node is BookmarkUrl bookmark)
        {
            ViewModel.SelectedBookmark = bookmark;
        }
        else if (node is BookmarkFolder)
        {
            var selectedItem = ViewModel.DisplayedItems
                .FirstOrDefault(item =>
                    ReferenceEquals(item.Node, node));

            if (selectedItem is not null)
            {
                ViewModel.UpdateSelectedContentItems(
                    new[] { selectedItem });
            }
        }

        var payload = new BookmarkDragPayload(node);
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
            GetBookmarkDragPayload(e)?.Node is not { } draggedNode)
        {
            e.Handled = true;
            return;
        }

        var targetItem = FindVisualParent<ListViewItem>(
            e.OriginalSource as DependencyObject);

        if (targetItem is null)
        {
            if (ViewModel.SelectedFolder is { } currentFolder &&
                DragDropRules.CanMoveContentNodeInto(
                    draggedNode,
                    currentFolder))
            {
                e.Effects = DragDropEffects.Move;
            }

            e.Handled = true;
            return;
        }

        if (targetItem.DataContext is not BookmarkListItemViewModel targetItemViewModel ||
            ReferenceEquals(draggedNode, targetItemViewModel.Node) ||
            targetItem.ActualHeight <= 0)
        {
            e.Handled = true;
            return;
        }

        var targetNode = targetItemViewModel.Node;
        var placement = DragDropRules.GetContentRowPlacement(
            e.GetPosition(targetItem).Y,
            targetItem.ActualHeight,
            targetNode is BookmarkFolder);

        var valid = placement == DropPlacement.Into
            ? targetNode is BookmarkFolder targetFolder &&
              DragDropRules.CanMoveContentNodeInto(
                  draggedNode,
                  targetFolder)
            : DragDropRules.CanMoveContentNodeRelativeTo(
                draggedNode,
                targetNode);

        if (!valid)
        {
            e.Handled = true;
            return;
        }

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
            GetBookmarkDragPayload(e)?.Node is not { } draggedNode)
        {
            e.Handled = true;
            return;
        }

        var targetItem = FindVisualParent<ListViewItem>(
            e.OriginalSource as DependencyObject);

        try
        {
            bool changed;

            if (targetItem is null)
            {
                if (ViewModel.SelectedFolder is not { } currentFolder ||
                    !DragDropRules.CanMoveContentNodeInto(
                        draggedNode,
                        currentFolder))
                {
                    e.Handled = true;
                    return;
                }

                changed = draggedNode switch
                {
                    BookmarkUrl bookmark =>
                        await ViewModel.MoveBookmarkToEndAsync(
                            bookmark,
                            currentFolder),
                    BookmarkFolder folder =>
                        await ViewModel.MoveFolderToEndAsync(
                            folder,
                            currentFolder),
                    _ => false
                };
            }
            else
            {
                if (targetItem.DataContext is not BookmarkListItemViewModel targetItemViewModel ||
                    ReferenceEquals(draggedNode, targetItemViewModel.Node) ||
                    targetItem.ActualHeight <= 0)
                {
                    e.Handled = true;
                    return;
                }

                var targetNode = targetItemViewModel.Node;
                var placement = DragDropRules.GetContentRowPlacement(
                    e.GetPosition(targetItem).Y,
                    targetItem.ActualHeight,
                    targetNode is BookmarkFolder);

                var valid = placement == DropPlacement.Into
                    ? targetNode is BookmarkFolder targetFolder &&
                      DragDropRules.CanMoveContentNodeInto(
                          draggedNode,
                          targetFolder)
                    : DragDropRules.CanMoveContentNodeRelativeTo(
                        draggedNode,
                        targetNode);

                if (!valid)
                {
                    e.Handled = true;
                    return;
                }

                changed = placement switch
                {
                    DropPlacement.Before =>
                        await ViewModel.MoveNodeBeforeAsync(
                            draggedNode,
                            targetNode),
                    DropPlacement.After =>
                        await ViewModel.MoveNodeAfterAsync(
                            draggedNode,
                            targetNode),
                    DropPlacement.Into when
                        targetNode is BookmarkFolder bookmarkTargetFolder &&
                        draggedNode is BookmarkUrl bookmark =>
                            await ViewModel.MoveBookmarkToEndAsync(
                                bookmark,
                                bookmarkTargetFolder),
                    DropPlacement.Into when
                        targetNode is BookmarkFolder folderTargetFolder &&
                        draggedNode is BookmarkFolder folder =>
                            await ViewModel.MoveFolderToEndAsync(
                                folder,
                                folderTargetFolder),
                    _ => false
                };
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
            return;
        }

        if (ViewModel.SelectedContentFolder is { } folder)
        {
            ViewModel.NavigateToFolder(folder);
        }
    }

    private void BookmarksList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (ViewModel.IsSearchActive &&
            ViewModel.SelectedBookmark is { } bookmark)
        {
            ViewModel.NavigateToSearchResult(bookmark);
            e.Handled = true;
            return;
        }

        if (ViewModel.SelectedContentFolder is { } folder)
        {
            ViewModel.NavigateToFolder(folder);
            e.Handled = true;
        }
    }

    private void BookmarksContextMenu_Opened(
        object sender,
        RoutedEventArgs e)
    {
        var folderSelected =
            ViewModel.SelectedContentFolder is not null;

        ContentOpenFolderMenuItem.Visibility =
            folderSelected ? Visibility.Visible : Visibility.Collapsed;
        ContentRenameFolderMenuItem.Visibility =
            folderSelected ? Visibility.Visible : Visibility.Collapsed;
        ContentMoveFolderMenuItem.Visibility =
            folderSelected && !multipleSelected
                ? Visibility.Visible
                : Visibility.Collapsed;
        var multipleSelected =
            ViewModel.SelectedContentItems.Count > 1;

        ContentDeleteFolderMenuItem.Visibility =
            folderSelected && !multipleSelected
                ? Visibility.Visible
                : Visibility.Collapsed;
        ContentDeleteSelectionMenuItem.Visibility =
            multipleSelected
                ? Visibility.Visible
                : Visibility.Collapsed;
        ContentFolderSeparator.Visibility =
            folderSelected ? Visibility.Visible : Visibility.Collapsed;

        ContentRenameBookmarkMenuItem.Visibility =
            folderSelected ? Visibility.Collapsed : Visibility.Visible;
        ContentEditUrlMenuItem.Visibility =
            folderSelected ? Visibility.Collapsed : Visibility.Visible;
        ContentMoveBookmarkMenuItem.Visibility =
            !folderSelected && !multipleSelected
                ? Visibility.Visible
                : Visibility.Collapsed;
        ContentMoveSelectionMenuItem.Visibility =
            multipleSelected
                ? Visibility.Visible
                : Visibility.Collapsed;
        ContentBookmarkSeparator.Visibility =
            folderSelected ? Visibility.Collapsed : Visibility.Visible;
        ContentDeleteBookmarkMenuItem.Visibility =
            !folderSelected && !multipleSelected
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void OpenContentFolder_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (ViewModel.SelectedContentFolder is { } folder)
        {
            ViewModel.NavigateToFolder(folder);
        }
    }

    private async void RenameContentFolder_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RenameContentFolderFromUiAsync();
    }

    private async void MoveContentFolder_Click(
        object sender,
        RoutedEventArgs e)
    {
        await MoveContentFolderFromUiAsync();
    }

    private async void DeleteContentFolder_Click(
        object sender,
        RoutedEventArgs e)
    {
        await DeleteContentFolderFromUiAsync();
    }

    private async void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CanUndo)
        {
            await ViewModel.UndoAsync();
        }
    }

    private async void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CanRedo)
        {
            await ViewModel.RedoAsync();
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

    private async void MoveSelectedContentItems_Click(
        object sender,
        RoutedEventArgs e)
    {
        await MoveSelectedContentItemsFromUiAsync();
    }

    private async void DeleteSelectedBookmarks_Click(
        object sender,
        RoutedEventArgs e)
    {
        await DeleteSelectedBookmarksFromUiAsync();
    }

    private async void DeleteSelectedContentItems_Click(
        object sender,
        RoutedEventArgs e)
    {
        await DeleteSelectedContentItemsFromUiAsync();
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

    private async Task RenameContentFolderFromUiAsync()
    {
        if (!ViewModel.CanRenameSelectedContentFolder ||
            ViewModel.SelectedContentFolder is not { } folder)
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
            await ViewModel.RenameFolderAsync(
                folder,
                dialog.NameValue);
        }
        catch (BookmarkEditException exception)
        {
            ShowEditError(exception);
        }
    }

    private async Task MoveContentFolderFromUiAsync()
    {
        if (!ViewModel.CanMoveSelectedContentFolder ||
            ViewModel.SelectedContentFolder is not { } folder ||
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
            await ViewModel.MoveFolderToEndAsync(
                folder,
                target);
        }
        catch (BookmarkMoveException exception)
        {
            ShowMoveError(exception);
        }
    }

    private async Task DeleteContentFolderFromUiAsync()
    {
        if (!ViewModel.CanDeleteSelectedContentFolder ||
            ViewModel.SelectedContentFolder is not { } folder)
        {
            return;
        }

        if (!ConfirmFolderDeletion(folder))
        {
            return;
        }

        try
        {
            await ViewModel.DeleteFolderAsync(folder);
        }
        catch (BookmarkDeleteException exception)
        {
            ShowDeleteError(exception);
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

    private async Task MoveSelectedContentItemsFromUiAsync()
    {
        var selectedItems = ViewModel.SelectedContentItems;

        if (!ViewModel.CanMoveSelectedContentItems ||
            selectedItems.Count == 0 ||
            ViewModel.Document is not { } document)
        {
            return;
        }

        var dialog = CreateMoveDialog(
            document,
            selectedItems[0].Node);
        if (dialog.ShowDialog() != true ||
            dialog.SelectedTarget is not { } target)
        {
            return;
        }

        try
        {
            await ViewModel.MoveSelectedContentItemsToEndAsync(
                target);
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

    private async Task DeleteSelectedContentItemsFromUiAsync()
    {
        if (!ViewModel.CanDeleteSelectedContentItems)
        {
            return;
        }

        var selectedItems = ViewModel.SelectedContentItems;
        if (selectedItems.Count == 0)
        {
            return;
        }

        var (bookmarkCount, folderCount) =
            CountSelectedContentRemoval(selectedItems);
        var itemSummary = selectedItems.Count == 1
            ? "1 selected item"
            : $"{selectedItems.Count:N0} selected items";
        var bookmarkSummary = bookmarkCount == 1
            ? "1 bookmark"
            : $"{bookmarkCount:N0} bookmarks";
        var folderSummary = folderCount == 1
            ? "1 folder"
            : $"{folderCount:N0} folders";
        var confirmation =
            $"Delete {itemSummary}?\n\n" +
            $"This will remove {bookmarkSummary} and {folderSummary}, " +
            "including the contents of selected folders.\n\n" +
            "The selected items will be removed from the loaded document.\n" +
            "Use Save to write the change safely to the source Chrome Bookmarks file.";

        if (!ConfirmDestructiveOperation(
                "Confirm selected item deletion",
                confirmation))
        {
            return;
        }

        try
        {
            await ViewModel.DeleteSelectedContentItemsAsync();
        }
        catch (BookmarkDeleteException exception)
        {
            ShowDeleteError(exception);
        }
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
            $"{removalSubject} will be removed from the loaded document.\n" +
            "Use Save to write the change safely to the source Chrome Bookmarks file.";

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

        if (!ConfirmFolderDeletion(folder))
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

    private bool ConfirmFolderDeletion(
        BookmarkFolder folder)
    {
        var (bookmarkCount, folderCount) =
            CountFolderDescendants(folder);
        var bookmarkSummary = bookmarkCount == 1
            ? "1 bookmark"
            : $"{bookmarkCount:N0} bookmarks";
        var folderSummary = folderCount == 1
            ? "1 nested folder"
            : $"{folderCount:N0} nested folders";
        var confirmation =
            $"Delete folder \"{folder.Name}\" and its entire subtree?\n\n" +
            $"This includes {bookmarkSummary} and {folderSummary}.\n\n" +
            "The subtree will be removed from the loaded document.\n" +
            "Use Save to write the change safely to the source Chrome Bookmarks file.";

        return ConfirmDestructiveOperation(
            "Confirm folder deletion",
            confirmation);
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
        CountSelectedContentRemoval(
            IReadOnlyList<BookmarkListItemViewModel> items)
    {
        var selected = new HashSet<BookmarkNode>(
            items.Select(item => item.Node),
            ReferenceEqualityComparer.Instance);
        var roots = selected
            .Where(node => !HasSelectedAncestor(node, selected))
            .ToArray();

        var bookmarkCount = 0;
        var folderCount = 0;
        var pending = new Stack<BookmarkNode>(roots);

        while (pending.TryPop(out var node))
        {
            switch (node)
            {
                case BookmarkUrl:
                    bookmarkCount++;
                    break;

                case BookmarkFolder folder:
                    folderCount++;
                    foreach (var child in folder.Children)
                    {
                        pending.Push(child);
                    }

                    break;
            }
        }

        return (bookmarkCount, folderCount);
    }

    private static bool HasSelectedAncestor(
        BookmarkNode node,
        IReadOnlySet<BookmarkNode> selected)
    {
        var current = node.Parent;

        while (current is not null)
        {
            if (selected.Contains(current))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
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
        var textInputOwnsFocus = Keyboard.FocusedElement is TextBox;

        if (e.Key == Key.S &&
            modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            if (ViewModel.CanSave)
            {
                await SaveCurrentDocumentAsync();
            }

            return;
        }

        if (!textInputOwnsFocus &&
            e.Key == Key.Z &&
            modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            if (ViewModel.CanUndo)
            {
                await ViewModel.UndoAsync();
            }

            return;
        }

        if (!textInputOwnsFocus &&
            ((e.Key == Key.Y &&
              modifiers == ModifierKeys.Control) ||
             (e.Key == Key.Z &&
              modifiers == (ModifierKeys.Control | ModifierKeys.Shift))))
        {
            e.Handled = true;
            if (ViewModel.CanRedo)
            {
                await ViewModel.RedoAsync();
            }

            return;
        }

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

                if (ViewModel.CanDeleteSelectedContentItems)
                {
                    await DeleteSelectedContentItemsFromUiAsync();
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
                ViewModel.CanRenameSelectedContentFolder)
            {
                await RenameContentFolderFromUiAsync();
                e.Handled = true;
                return;
            }

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
        item.BorderThickness = placement switch
        {
            DropPlacement.Before => new Thickness(0, 2, 0, 0),
            DropPlacement.After => new Thickness(0, 0, 0, 2),
            _ => new Thickness(1)
        };

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
