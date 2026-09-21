import fs from "node:fs";

const path = "scripts/Invoke-V10ReleaseMeasurements.ps1";
const source = fs.readFileSync(path, "utf8");

const required = [
  "Measure-Reader.ps1",
  "Measure-BrowserState.ps1",
  "Measure-Search.ps1",
  "CBM_MOVE_URL_COUNT",
  "CBM_MOVE_FOLDER_COUNT",
  "CBM_DELETE_URL_COUNT",
  "CBM_DELETE_FOLDER_COUNT",
  "CBM_HISTORY_URL_COUNT",
  "CBM_HISTORY_EDIT_COUNT",
  "CBM_WRITE_URL_COUNT",
  "Category=MoveScale",
  "Category=DeleteBatchScale",
  "Category=UndoRedoScale",
  "Category=WriteScale",
  "try",
  "finally"
];

for (const text of required) {
  if (!source.includes(text)) {
    throw new Error(`Missing release measurement contract token: ${text}`);
  }
}

console.log("V1.0 release measurement runner contract verified.");
