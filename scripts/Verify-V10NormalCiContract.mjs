import fs from "node:fs";

const path = ".github/workflows/build-windows.yml";
const yaml = fs.readFileSync(path, "utf8");

const required = [
  "Verify-V10Version.ps1 -ExpectedTag v1.1.1",
  "Verify-V10UiContract.mjs",
  "Verify-V10RightPaneParity.mjs",
  "Verify-V10ReleaseMeasurementsContract.mjs",
  "Verify-V10ReleaseWorkflow.mjs",
  "Verify-V10Version.ps1 -ExecutablePath artifacts/win-x64/ChromeBookmarksManager.exe",
  "retention-days: 1"
];

const forbidden = [
  "Invoke-V10ReleaseReadiness.ps1",
  "Invoke-V10ReleaseMeasurements.ps1"
];

for (const text of required) {
  if (!yaml.includes(text)) {
    throw new Error(`Missing normal-CI V1.1.1 contract token: ${text}`);
  }
}

for (const text of forbidden) {
  if (yaml.includes(text)) {
    throw new Error(`Normal CI must not execute release-only command: ${text}`);
  }
}

console.log("V1.1.1 normal CI contract verified.");
