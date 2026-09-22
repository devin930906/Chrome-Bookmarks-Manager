import crypto from "node:crypto";
import fs from "node:fs";

const projectPath = "src/ChromeBookmarksManager/ChromeBookmarksManager.csproj";
const iconPath = "src/ChromeBookmarksManager/Assets/ChromeBookmarksManager.ico";
const expectedSha256 = "7552174be5cd3117085677d5d37cfac16c978330221b7ba1749baa9953e8e039";
const expectedVersion = "1.1.1";

const project = fs.readFileSync(projectPath, "utf8");
const icon = fs.readFileSync(iconPath);

const violations = [];

if (!project.includes(`<VersionPrefix>${expectedVersion}</VersionPrefix>`)) {
  violations.push(`Project VersionPrefix must be ${expectedVersion}.`);
}

if (!project.includes("<ApplicationIcon>Assets\\ChromeBookmarksManager.ico</ApplicationIcon>")) {
  violations.push("ApplicationIcon must point to Assets\\ChromeBookmarksManager.ico.");
}

const actualSha256 = crypto.createHash("sha256").update(icon).digest("hex");
if (actualSha256 !== expectedSha256) {
  violations.push(
    `Branded V1.1.1 icon SHA-256 mismatch. Expected ${expectedSha256}, got ${actualSha256}.`);
}

if (icon.length < 50000) {
  violations.push(`ICO looks unexpectedly small: ${icon.length} bytes.`);
}

if (icon.length >= 6) {
  const reserved = icon.readUInt16LE(0);
  const type = icon.readUInt16LE(2);
  const count = icon.readUInt16LE(4);
  if (reserved !== 0 || type !== 1) {
    violations.push("Application icon is not a valid Windows ICO header.");
  }
  if (count < 9) {
    violations.push(`Expected at least 9 icon images, found ${count}.`);
  }
}

if (violations.length > 0) {
  throw new Error(
    "V1.1.1 icon refresh contract violations:\n- " +
    violations.join("\n- "));
}

console.log("V1.1.1 icon refresh contract verified.");
