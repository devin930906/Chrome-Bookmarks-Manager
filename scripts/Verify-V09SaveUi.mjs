import fs from "node:fs";

const xaml = fs.readFileSync(
  "src/ChromeBookmarksManager/MainWindow.xaml",
  "utf8");
const code = fs.readFileSync(
  "src/ChromeBookmarksManager/MainWindow.xaml.cs",
  "utf8");

const failures = [];

function requireText(haystack, needle, description) {
  if (!haystack.includes(needle)) {
    failures.push(description + " (missing: " + needle + ")");
  }
}

function forbidText(haystack, needle, description) {
  if (haystack.includes(needle)) {
    failures.push(description + " (found: " + needle + ")");
  }
}

requireText(xaml, 'Header="{DynamicResource MenuSave}"', "File menu must expose Save");
requireText(xaml, 'InputGestureText="Ctrl+S"', "Save menu must advertise Ctrl+S");
requireText(xaml, 'IsEnabled="{Binding CanSave}"', "Save controls must bind to CanSave");
requireText(xaml, 'Click="Save_Click"', "Save controls must route through Save_Click");

requireText(code, "ChromeBookmarksSaveService", "MainWindow must construct the safe-save service");
requireText(code, "BookmarkFileTransaction", "MainWindow must construct the verified file transaction");
requireText(code, "BookmarkSourceBaselineService", "MainWindow must construct source baseline verification");
requireText(code, "ChromeProcessDetector", "MainWindow must construct the Chrome process safety guard");
requireText(code, "private async void Save_Click", "MainWindow must implement Save_Click");
requireText(code, "await ViewModel.SaveAsync", "Save_Click must call MainViewModel.SaveAsync");
requireText(code, "e.Key == Key.S", "Window keyboard handling must support S");
requireText(code, "ModifierKeys.Control", "Ctrl modifier must be checked for Save");
requireText(code, "SaveDiscardCancel", "Dirty document flow must expose Save / Discard / Cancel semantics");
requireText(code, "PromptDirtyDocumentAsync", "Open/close dirty handling must share one save-aware prompt");
requireText(code, "ChromeBookmarksSaveException", "UI must surface typed save failures");
requireText(
  code,
  "ViewModel.State == DocumentState.Saving",
  "Window close must be blocked while Save is in progress");
requireText(
  code,
  "exception.HasVerifiedRecoveryBackup",
  "Save failure UI must only label a backup verified after the recovery backup reached a verified stage");

forbidText(
  xaml,
  "Save is not available yet",
  "V0.8 no-save banner must be removed");
forbidText(
  code,
  "V0.7 has no Save or write-back path",
  "legacy delete copy must no longer claim Save does not exist");

if (failures.length > 0) {
  for (const failure of failures) {
    console.error("V0.9 Save UI violation: " + failure);
  }
  process.exit(1);
}

console.log("V0.9 Save UI contract verified.");
