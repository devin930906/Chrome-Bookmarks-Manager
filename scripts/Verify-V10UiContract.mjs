import fs from "node:fs";

const xamlPath = "src/ChromeBookmarksManager/MainWindow.xaml";
const codePath = "src/ChromeBookmarksManager/MainWindow.xaml.cs";
const xaml = fs.readFileSync(xamlPath, "utf8");
const code = fs.readFileSync(codePath, "utf8");

const violations = [];

const requiredXaml = [
  'Title="{Binding ApplicationTitle}"',
  'Changes are written only when you explicitly choose Save.',
  'Header="E_xit"',
  'Click="Exit_Click"'
];

const forbiddenXaml = [
  'V0.6 edits and moves remain in memory.',
  'Header="_View"',
  'Header="_Tools"'
];

for (const text of requiredXaml) {
  if (!xaml.includes(text)) {
    violations.push(`Missing V1.0 UI contract text: ${text}`);
  }
}

for (const text of forbiddenXaml) {
  if (xaml.includes(text)) {
    violations.push(`Forbidden V1.0 production UI residue: ${text}`);
  }
}

const closingStart = code.indexOf("private async void Window_Closing");
const closingEnd = code.indexOf(
  "private Task<SaveDiscardCancel>",
  closingStart);

if (closingStart < 0 || closingEnd < 0) {
  violations.push("Window_Closing contract method could not be located.");
} else {
  const closing = code.slice(closingStart, closingEnd);
  const loadingGuard = closing.indexOf(
    "if (ViewModel.State == DocumentState.Loading)");
  const allowCloseGuard = closing.indexOf("if (_allowClose)");

  if (loadingGuard < 0) {
    violations.push(
      "Window_Closing must explicitly block close while Loading.");
  } else {
    const loadingBlockEnd = closing.indexOf("}", loadingGuard);
    const loadingBlock = closing.slice(
      loadingGuard,
      loadingBlockEnd >= 0 ? loadingBlockEnd + 1 : closing.length);

    if (!loadingBlock.includes("e.Cancel = true;")) {
      violations.push(
        "The Loading close guard must set e.Cancel = true.");
    }

    if (!loadingBlock.includes("return;")) {
      violations.push(
        "The Loading close guard must return immediately.");
    }
  }

  if (allowCloseGuard < 0) {
    violations.push("Window_Closing is missing the _allowClose guard.");
  } else if (loadingGuard < 0 || loadingGuard > allowCloseGuard) {
    violations.push(
      "The Loading close guard must run before the _allowClose guard.");
  }
}

const exitHandlerStart = code.indexOf("private void Exit_Click");
if (exitHandlerStart < 0) {
  violations.push("A working File -> Exit handler is required.");
} else {
  const exitHandlerEnd = code.indexOf(
    "\n    private ",
    exitHandlerStart + 1);
  const exitHandler = code.slice(
    exitHandlerStart,
    exitHandlerEnd >= 0 ? exitHandlerEnd : code.length);

  if (!exitHandler.includes("Close();")) {
    violations.push("Exit_Click must route through Window.Close().");
  }
}

if (violations.length > 0) {
  throw new Error(
    "V1.0 UI contract violations:\n- " +
    violations.join("\n- "));
}

console.log("V1.0 production UI contract verified.");
