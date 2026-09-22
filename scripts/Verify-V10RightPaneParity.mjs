import fs from "node:fs";

const xamlPath = "src/ChromeBookmarksManager/MainWindow.xaml";
const codePath = "src/ChromeBookmarksManager/MainWindow.xaml.cs";
const xaml = fs.readFileSync(xamlPath, "utf8");
const code = fs.readFileSync(codePath, "utf8");

const violations = [];

const requiredXaml = [
  'ItemsSource="{Binding DisplayedItems}"',
  'Header="Open Folder"',
  'Header="Rename Folder..."',
  'Header="Move Folder to..."',
  'Header="Delete Folder..."',
  'IsFolder',
  'IsBookmark'
];

const requiredCode = [
  "BookmarkListItemViewModel",
  "NavigateToFolder",
  "RenameFolderAsync",
  "DeleteFolderAsync",
  "UpdateSelectedContentItems"
];

const forbiddenXaml = [
  'ItemsSource="{Binding DisplayedBookmarks}"'
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

if (violations.length > 0) {
  throw new Error(
    "V1.0 right-pane parity violations:\n- " +
    violations.join("\n- "));
}

console.log("V1.0 right-pane browser-manager parity verified.");
