import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = process.argv[2] ?? path.resolve(scriptDirectory, '..');
const xamlPath = path.join(projectRoot, 'src/ChromeBookmarksManager/MainWindow.xaml');
const codePath = path.join(projectRoot, 'src/ChromeBookmarksManager/MainWindow.xaml.cs');
const viewModelPath = path.join(projectRoot, 'src/ChromeBookmarksManager/ViewModels/MainViewModel.cs');
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

const xaml = readRequired(xamlPath);
const code = readRequired(codePath);
const viewModel = readRequired(viewModelPath);

requireText(xaml, 'Header="{DynamicResource MenuUndo}"', 'Edit menu must expose Undo.');
requireText(xaml, 'Header="{DynamicResource MenuRedo}"', 'Edit menu must expose Redo.');
requireText(xaml, 'InputGestureText="Ctrl+Z"', 'Undo must advertise Ctrl+Z.');
requireText(xaml, 'InputGestureText="Ctrl+Y / Ctrl+Shift+Z"', 'Redo must advertise both supported shortcuts.');
requireText(xaml, 'IsEnabled="{Binding CanUndo}"', 'Undo UI must bind to CanUndo.');
requireText(xaml, 'IsEnabled="{Binding CanRedo}"', 'Redo UI must bind to CanRedo.');
requireText(xaml, 'Click="Undo_Click"', 'Undo UI must route through Undo_Click.');
requireText(xaml, 'Click="Redo_Click"', 'Redo UI must route through Redo_Click.');
requireText(xaml, 'Content="{DynamicResource ToolbarUndo}"', 'A visible Undo toolbar button is required.');
requireText(xaml, 'Content="{DynamicResource ToolbarRedo}"', 'A visible Redo toolbar button is required.');
requireText(xaml, 'ToolTip="{Binding UndoDescription}"', 'Undo description must be surfaced in the UI.');
requireText(xaml, 'ToolTip="{Binding RedoDescription}"', 'Redo description must be surfaced in the UI.');

requireText(code, 'e.Key == Key.Z', 'Keyboard path must handle Z for Undo/alternate Redo.');
requireText(code, 'e.Key == Key.Y', 'Keyboard path must handle Y for Redo.');
requireText(code, 'ModifierKeys.Control | ModifierKeys.Shift', 'Keyboard path must support Ctrl+Shift+Z.');
requireText(code, 'Keyboard.FocusedElement is TextBox', 'History shortcuts must preserve native TextBox undo/redo.');
requireText(code, 'await ViewModel.UndoAsync()', 'Undo UI must call the ViewModel history command.');
requireText(code, 'await ViewModel.RedoAsync()', 'Redo UI must call the ViewModel history command.');

requireText(viewModel, 'public bool CanUndo', 'ViewModel must expose CanUndo.');
requireText(viewModel, 'public bool CanRedo', 'ViewModel must expose CanRedo.');
requireText(viewModel, 'public string? UndoDescription', 'ViewModel must expose UndoDescription.');
requireText(viewModel, 'public string? RedoDescription', 'ViewModel must expose RedoDescription.');
requireText(viewModel, 'public async Task<bool> UndoAsync()', 'ViewModel must expose UndoAsync.');
requireText(viewModel, 'public async Task<bool> RedoAsync()', 'ViewModel must expose RedoAsync.');

if (errors.length > 0) {
  for (const error of errors) {
    console.error(`FAIL: ${error}`);
  }
  process.exitCode = 1;
} else {
  console.log('V0.8 Undo/Redo UI contract verified.');
}
