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

const mainXaml = fs.readFileSync(mainXamlPath, "utf8");
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
