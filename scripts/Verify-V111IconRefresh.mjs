import fs from "node:fs";

const projectPath = "src/ChromeBookmarksManager/ChromeBookmarksManager.csproj";
const iconPath = "src/ChromeBookmarksManager/Assets/ChromeBookmarksManager.ico";
const requiredSizes = [16, 24, 32, 48, 64, 128];

const project = fs.readFileSync(projectPath, "utf8");
const icon = fs.readFileSync(iconPath);

const violations = [];


if (!project.includes("<ApplicationIcon>Assets\\ChromeBookmarksManager.ico</ApplicationIcon>")) {
  violations.push("ApplicationIcon must point to Assets\\ChromeBookmarksManager.ico.");
}

if (icon.length < 10000) {
  violations.push(`ICO looks unexpectedly small: ${icon.length} bytes.`);
}

if (icon.length < 6) {
  violations.push("Application icon is too short to contain an ICO header.");
} else {
  const reserved = icon.readUInt16LE(0);
  const type = icon.readUInt16LE(2);
  const count = icon.readUInt16LE(4);

  if (reserved !== 0 || type !== 1) {
    violations.push("Application icon is not a valid Windows ICO header.");
  }

  const actualSizes = [];
  for (let index = 0; index < count; index += 1) {
    const entryOffset = 6 + (index * 16);
    if (entryOffset + 16 > icon.length) {
      violations.push(`ICO directory entry ${index} is truncated.`);
      break;
    }

    const width = icon[entryOffset] || 256;
    const height = icon[entryOffset + 1] || 256;
    const imageLength = icon.readUInt32LE(entryOffset + 8);
    const imageOffset = icon.readUInt32LE(entryOffset + 12);

    if (width !== height) {
      violations.push(`ICO frame ${index} is not square: ${width}x${height}.`);
    }

    if (imageOffset + imageLength > icon.length) {
      violations.push(`ICO frame ${index} extends beyond the file boundary.`);
    }

    actualSizes.push(width);
  }

  for (const requiredSize of requiredSizes) {
    if (!actualSizes.includes(requiredSize)) {
      violations.push(`ICO is missing required ${requiredSize}x${requiredSize} frame.`);
    }
  }
}

if (violations.length > 0) {
  throw new Error(
    "Application icon contract violations:\n- " +
    violations.join("\n- "));
}

console.log("Application icon contract verified.");
