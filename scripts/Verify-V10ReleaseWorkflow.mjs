import fs from "node:fs";

const path = ".github/workflows/release-windows.yml";
const yaml = fs.readFileSync(path, "utf8");

const required = [
  "tags:",
  "v*.*.*",
  "branches:",
  "main",
  "workflow_dispatch:",
  "release_tag:",
  "release-publish: v",
  "RELEASE_COMMIT_MESSAGE",
  "contents: write",
  "RELEASE_TAG",
  "Verify-V10Version.ps1",
  "Invoke-V10ReleaseReadiness.ps1",
  "Verify-V10ReleaseAssets.ps1",
  "ChromeBookmarksManager.exe.sha256",
  "gh @arguments",
  'docs/releases/$env:RELEASE_TAG.md',
  '"--target", "${{ github.sha }}"',
  "--verify-tag"
];

const forbidden = [
  "V09-Task12DisposableProfile.ps1",
  "V09-TASK12-OWNER-GUIDE.txt",
  "V09-Task13ProductionAcceptance.ps1",
  "V09-TASK13-OWNER-GUIDE.txt"
];

for (const text of required) {
  if (!yaml.includes(text)) {
    throw new Error(`Missing release workflow contract token: ${text}`);
  }
}

for (const text of forbidden) {
  if (yaml.includes(text)) {
    throw new Error(`Release workflow must not package: ${text}`);
  }
}

console.log("Release workflow contract verified.");
