import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = process.argv[2] ?? path.resolve(scriptDirectory, '..');
const mainWindowXamlPath = path.join(projectRoot, 'src/ChromeBookmarksManager/MainWindow.xaml');
const mainWindowCodePath = path.join(projectRoot, 'src/ChromeBookmarksManager/MainWindow.xaml.cs');
const mainViewModelPath = path.join(projectRoot, 'src/ChromeBookmarksManager/ViewModels/MainViewModel.cs');
const errors = [];

function readRequired(filePath) {
  if (!fs.existsSync(filePath)) {
    errors.push(`Missing required file: ${filePath}`);
    return '';
  }

  return fs.readFileSync(filePath, 'utf8');
}

function requireText(content, pattern, message) {
  if (!content.includes(pattern)) {
    errors.push(message);
  }
}

function requireBefore(content, first, second, message) {
  const firstIndex = content.indexOf(first);
  const secondIndex = content.indexOf(second);
  if (firstIndex < 0 || secondIndex < 0 || firstIndex >= secondIndex) {
    errors.push(message);
  }
}

const xaml = readRequired(mainWindowXamlPath);
const code = readRequired(mainWindowCodePath);
const viewModel = readRequired(mainViewModelPath);

requireText(xaml, 'SelectionMode="Extended"', 'Bookmark rows must support Ctrl/Shift multi-selection.');
requireText(xaml, 'SelectionChanged="BookmarksList_SelectionChanged"', 'List selection changes must be forwarded to the ViewModel.');
requireText(xaml, 'VirtualizingPanel.IsVirtualizing="True"', 'V0.7 must preserve list/tree virtualization.');
requireText(xaml, 'VirtualizingPanel.VirtualizationMode="Recycling"', 'V0.7 must preserve recycling virtualization.');
requireText(xaml, 'Header="Delete Bookmark(s)..."', 'Bookmark context menu must expose deletion.');
requireText(xaml, 'IsEnabled="{Binding CanDeleteSelectedBookmarks}"', 'Bookmark deletion must follow ViewModel capability.');
requireText(xaml, 'Click="DeleteSelectedBookmarks_Click"', 'Bookmark deletion must route through its guarded UI handler.');
requireText(xaml, 'Header="Delete Folder..."', 'Folder context menu must expose deletion.');
requireText(xaml, 'IsEnabled="{Binding CanDeleteSelectedFolder}"', 'Folder deletion must follow ordinary-folder capability.');
requireText(xaml, 'Click="DeleteSelectedFolder_Click"', 'Folder deletion must route through its guarded UI handler.');
requireText(xaml, 'IsEnabled="{Binding CanMoveSelectedBookmarks}"', 'Move to... must be enabled for a selected bookmark set.');
requireText(xaml, 'IsEnabled="{Binding CanRenameSelectedBookmark}"', 'Bookmark rename must remain available through its guarded capability.');
requireText(xaml, 'IsEnabled="{Binding CanEditSelectedBookmarkUrl}"', 'Bookmark URL editing must remain available through its guarded capability.');

requireText(code, 'BookmarksList.SelectedItems', 'The WPF list selection must be sent to the ViewModel.');
requireText(code, 'viewModel.UpdateSelectedContentItems(', 'The ViewModel must own the mixed right-pane selection and derive the selected bookmark set.');
requireText(code, 'if (!item.IsSelected)', 'Right-click on an already-selected bookmark must preserve multi-selection.');
requireText(code, 'BookmarksList.UnselectAll()', 'Right-click on an unselected bookmark must make it the only context-menu target.');
requireText(code, 'BookmarksList.SelectAll()', 'Ctrl+A must select the displayed list rows.');
requireText(code, 'e.Key == Key.A', 'Ctrl+A must be handled by the keyboard path.');
requireText(code, 'e.Key == Key.Delete', 'Delete must be handled by the keyboard path.');
requireText(code, 'BookmarksList.IsKeyboardFocusWithin', 'Bookmark Delete/Ctrl+A must require bookmark-list focus.');
requireText(code, 'FolderTree.IsKeyboardFocusWithin', 'Folder Delete must require folder-tree focus.');
requireText(code, 'await DeleteSelectedBookmarksFromUiAsync()', 'Bookmark Delete must confirm before calling the ViewModel.');
requireText(code, 'await DeleteSelectedFolderFromUiAsync()', 'Folder Delete must confirm before calling the ViewModel.');
requireText(code, 'MessageBoxButton.YesNoCancel', 'Destructive confirmation must offer an explicit cancel path.');
requireText(code, 'MessageBoxResult.Cancel', 'Destructive confirmation must default to Cancel.');
requireText(code, 'MessageBoxResult.Yes', 'Deletion must proceed only after explicit Yes confirmation.');
requireText(code, 'removed from the loaded document', 'Delete confirmation must explain that deletion first affects the loaded document.');
requireText(code, 'Use Save to write the change safely', 'Delete confirmation must explain that source write-back requires explicit Save.');
requireText(code, 'MoveSelectedBookmarksToEndAsync', 'Move to... must dispatch multi-selection through the batch ViewModel method.');
requireText(code, 'CountFolderDescendants(folder)', 'Folder confirmation must calculate descendant counts.');
requireText(code, 'BookmarkDragPayload.ForContentRow(', 'V1.0 right-pane drag-and-drop must preserve the ordered selected node set when the pointer-down row is selected.');
requireText(code, 'MoveContentNodesAsync', 'V1.0 mixed drag-and-drop must dispatch through the generic batch move path.');
requireText(viewModel, 'HasUnambiguousSelectedBookmark()', 'Single-item bookmark rename/edit/move must use the mixed-selection ambiguity guard.');

const bookmarkDeleteStart = code.indexOf('private async Task DeleteSelectedBookmarksFromUiAsync()');
const folderDeleteStart = code.indexOf('private async Task DeleteSelectedFolderFromUiAsync()');
const confirmationStart = code.indexOf('private bool ConfirmDestructiveOperation(');
if (bookmarkDeleteStart >= 0 && folderDeleteStart > bookmarkDeleteStart) {
  requireBefore(
    code.slice(bookmarkDeleteStart, folderDeleteStart),
    'ConfirmDestructiveOperation(',
    'await ViewModel.DeleteSelectedBookmarksAsync()',
    'Cancelling bookmark confirmation must return before any ViewModel mutation.');
}
if (folderDeleteStart >= 0 && confirmationStart > folderDeleteStart) {
  requireBefore(
    code.slice(folderDeleteStart, confirmationStart),
    'ConfirmFolderDeletion(',
    'await ViewModel.DeleteSelectedFolderAsync()',
    'Cancelling folder confirmation must return before any ViewModel mutation.');
}

for (const forbidden of ['.RemoveChildAt(', '.InsertChild(', '.AddChild(']) {
  if (code.includes(forbidden)) {
    errors.push(`MainWindow code-behind must not mutate bookmark hierarchy directly: ${forbidden}`);
  }
}

if (errors.length > 0) {
  for (const error of errors) {
    console.error(`FAIL: ${error}`);
  }

  process.exitCode = 1;
} else {
  console.log('V0.7 Delete/Batch UI contract verified.');
}
