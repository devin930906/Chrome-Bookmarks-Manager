import fs from "node:fs";

const requiredFiles = [
  "src/ChromeBookmarksManager/Localization/Strings.zh-CN.xaml",
  "src/ChromeBookmarksManager/Localization/Strings.en-US.xaml",
  "src/ChromeBookmarksManager/Localization/LocalizationService.cs",
  "src/ChromeBookmarksManager/Infrastructure/UserSettingsStore.cs",
  "src/ChromeBookmarksManager/Assets/ChromeBookmarksManager.ico"
];

const violations = [];

for (const file of requiredFiles) {
  if (!fs.existsSync(file)) {
    violations.push(`Missing V1.1 localization/icon file: ${file}`);
  }
}

const mainXamlPath = "src/ChromeBookmarksManager/MainWindow.xaml";
const appXamlPath = "src/ChromeBookmarksManager/App.xaml";
const projectPath = "src/ChromeBookmarksManager/ChromeBookmarksManager.csproj";

const mainCodePath = "src/ChromeBookmarksManager/MainWindow.xaml.cs";
const zhCnPath = "src/ChromeBookmarksManager/Localization/Strings.zh-CN.xaml";
const enUsPath = "src/ChromeBookmarksManager/Localization/Strings.en-US.xaml";

const mainXaml = fs.readFileSync(mainXamlPath, "utf8");
const mainCode = fs.readFileSync(mainCodePath, "utf8");
const zhCn = fs.readFileSync(zhCnPath, "utf8");
const enUs = fs.readFileSync(enUsPath, "utf8");
const appXaml = fs.readFileSync(appXamlPath, "utf8");
const project = fs.readFileSync(projectPath, "utf8");

for (const token of [
  'Header="{DynamicResource MenuFile}"',
  'Header="{DynamicResource MenuEdit}"',
  'Header="{DynamicResource MenuLanguage}"',
  'Content="{DynamicResource ToolbarOpenBookmarks}"',
  'Content="{DynamicResource ToolbarSave}"',
  'Header="{DynamicResource PanelFolders}"',
  'Header="{DynamicResource PanelContents}"'
]) {
  if (!mainXaml.includes(token)) {
    violations.push(`Missing dynamic localization binding: ${token}`);
  }
}

for (const token of [
  'Click="SwitchToSimplifiedChinese_Click"',
  'Click="SwitchToEnglish_Click"'
]) {
  if (!mainXaml.includes(token)) {
    violations.push(`Missing runtime language switch hook: ${token}`);
  }
}

const addFolderClickCount =
  (mainXaml.match(/Click="AddFolder_Click"/g) ?? []).length;

if (addFolderClickCount < 3) {
  violations.push(
    "Add Folder must be available from the Edit menu, left folder tree context menu, and right content-pane context menu.");
}

if (!mainXaml.includes('x:Name="ContentAddFolderMenuItem"')) {
  violations.push(
    "The right content-pane context menu must expose Add Folder.");
}

if (!mainCode.includes('name: L("DefaultNewFolderName")')) {
  violations.push(
    "New-folder dialogs must start with the localized default folder name.");
}

if (!zhCn.includes('<system:String x:Key="DefaultNewFolderName">新建文件夹</system:String>')) {
  violations.push(
    "Simplified Chinese resources must define the default new-folder name.");
}

if (!enUs.includes('<system:String x:Key="DefaultNewFolderName">New folder</system:String>')) {
  violations.push(
    "English resources must define the default new-folder name.");
}

if (!appXaml.includes("Strings.zh-CN.xaml")) {
  violations.push(
    "App.xaml must load Simplified Chinese resources by default.");
}

if (!project.includes("<ApplicationIcon>Assets\\ChromeBookmarksManager.ico</ApplicationIcon>")) {
  violations.push("The EXE must embed ChromeBookmarksManager.ico.");
}

if (violations.length > 0) {
  throw new Error(
    "V1.1 localization/icon contract violations:\n- " +
    violations.join("\n- "));
}

console.log("V1.1 localization/icon contract verified.");
