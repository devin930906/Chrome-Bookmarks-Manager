import fs from "node:fs";

const xamlPath = "src/ChromeBookmarksManager/MainWindow.xaml";
const codePath = "src/ChromeBookmarksManager/MainWindow.xaml.cs";
const xaml = fs.readFileSync(xamlPath, "utf8");
const code = fs.readFileSync(codePath, "utf8");

const violations = [];

const requiredXaml = [
  'ItemsSource="{Binding DisplayedItems}"',
  'SelectionMode="Extended"',
  'Header="Open Folder"',
  'Header="Open Bookmark"',
  'IsEnabled="{Binding CanOpenSelectedContentItem}"',
  'Click="OpenContentBookmark_Click"',
  'Header="_Import Bookmarks HTML..."',
  'IsEnabled="{Binding CanImportBookmarksHtml}"',
  'Click="ImportBookmarksHtml_Click"',
  'Header="_Export Bookmarks HTML..."',
  'IsEnabled="{Binding CanExportBookmarksHtml}"',
  'Click="ExportBookmarksHtml_Click"',
  'Header="_Cut"',
  'InputGestureText="Ctrl+X"',
  'IsEnabled="{Binding CanCutSelectedContentItems}"',
  'Header="_Copy"',
  'InputGestureText="Ctrl+C"',
  'IsEnabled="{Binding CanCopySelectedContentItems}"',
  'Header="_Paste"',
  'InputGestureText="Ctrl+V"',
  'IsEnabled="{Binding CanPasteClipboard}"',
  'Header="Sort by _Name"',
  'IsEnabled="{Binding CanSortSelectedFolder}"',
  'Click="SortSelectedFolder_Click"',
  'Header="Rename Folder..."',
  'Header="Move Folder to..."',
  'Header="Delete Folder..."',
  'Header="_Delete Selected Item(s)..."',
  'Header="Move Selected Item(s) to..."',
  'IsEnabled="{Binding CanMoveSelectedContentItems}"',
  'Click="MoveSelectedContentItems_Click"',
  'IsEnabled="{Binding CanDeleteSelectedContentItems}"',
  'Click="DeleteSelectedContentItems_Click"',
  'IsFolder',
  'IsBookmark'
];

const requiredCode = [
  "BookmarkListItemViewModel",
  "NavigateToFolder",
  "OpenSelectedContentItemAsync",
  "OpenSelectedContentItemFromUiAsync",
  "CanOpenSelectedContentItem",
  "WindowsExternalUrlLauncher",
  "RenameFolderAsync",
  "DeleteFolderAsync",
  "DeleteSelectedContentItemsFromUiAsync",
  "DeleteSelectedContentItemsAsync",
  "MoveSelectedContentItemsFromUiAsync",
  "MoveSelectedContentItemsToEndAsync",
  "MoveContentNodesAsync",
  "BookmarkDragPayload.ForContentRow",
  "GetBookmarkDragPayload(e)?.Nodes",
  "CountSelectedContentRemoval",
  "UpdateSelectedContentItems",
  "CutSelectedContentItemsFromUi",
  "CopySelectedContentItemsFromUi",
  "PasteClipboardFromUiAsync",
  "SortSelectedFolderByNameAsync",
  "ImportBookmarksHtmlAsync",
  "BookmarksList.SelectAll();",
  "BookmarksList.UnselectAll();",
  "e.Key == Key.Escape",
  "Key.Enter",
  "e.Key == Key.X",
  "e.Key == Key.C",
  "e.Key == Key.V"
];

const forbiddenXaml = [
  'ItemsSource="{Binding DisplayedBookmarks}"'
];

const forbiddenCodeBehind = [
  ".AddChild(",
  ".InsertChild(",
  ".RemoveChildAt(",
  ".RecordAddedNode(",
  ".RecordRemovedSubtree(",
  ".RecordRestoredSubtree(",
  ".AllocateNextNodeId(",
  ".AllocateUniqueGuid("
];

for (const text of requiredXaml) {
  if (!xaml.includes(text)) {
    violations.push(`Missing right-pane parity XAML: ${text}`);
  }
}

for (const text of requiredCode) {
  if (!code.includes(text)) {
    violations.push(`Missing right-pane parity code: ${text}`);
  }
}

for (const text of forbiddenXaml) {
  if (xaml.includes(text)) {
    violations.push(`Forbidden bookmark-only right-pane binding: ${text}`);
  }
}

for (const text of forbiddenCodeBehind) {
  if (code.includes(text)) {
    violations.push(
      `Forbidden direct bookmark hierarchy mutation in code-behind: ${text}`);
  }
}

if (violations.length > 0) {
  throw new Error(
    "V1.0 right-pane parity violations:\n- " +
    violations.join("\n- "));
}

console.log("V1.0 command/menu/shortcut and right-pane parity verified.");
