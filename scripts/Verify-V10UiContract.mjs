import fs from "node:fs";

const path = "src/ChromeBookmarksManager/MainWindow.xaml";
const xaml = fs.readFileSync(path, "utf8");

const required = [
  'Title="{Binding ApplicationTitle}"',
  'Changes are written only when you explicitly choose Save.'
];

const forbidden = [
  'V0.6 edits and moves remain in memory.',
  'Header="_View"',
  'Header="_Tools"',
  'Header="E_xit"'
];

for (const text of required) {
  if (!xaml.includes(text)) {
    throw new Error(`Missing V1.0 UI contract text: ${text}`);
  }
}

for (const text of forbidden) {
  if (xaml.includes(text)) {
    throw new Error(`Forbidden V1.0 production UI residue: ${text}`);
  }
}

console.log("V1.0 production UI contract verified.");
