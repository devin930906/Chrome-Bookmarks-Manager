import fs from "node:fs";

const path = ".github/workflows/release-windows.yml";
const yaml = fs.readFileSync(path, "utf8");

const required = [
  "tags:",
  "v*.*.*",
  "contents: write",
  "Verify-V10Version.ps1",
  "Invoke-V10ReleaseReadiness.ps1",
  "Verify-V10ReleaseAssets.ps1",
  "ChromeBookmarksManager.exe.sha256",
  "gh release create",
  'docs/releases/${{ github.ref_name }}.md'
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
