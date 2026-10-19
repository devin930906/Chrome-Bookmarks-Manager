import fs from "node:fs";
import { inflateSync } from "node:zlib";

const projectPath = "src/ChromeBookmarksManager/ChromeBookmarksManager.csproj";
const windowPath = "src/ChromeBookmarksManager/MainWindow.xaml";
const iconPath = "src/ChromeBookmarksManager/Assets/ChromeBookmarksManager.ico";
const requiredSizes = [16, 24, 32, 48, 64, 128];

const project = fs.readFileSync(projectPath, "utf8");
const windowXaml = fs.readFileSync(windowPath, "utf8");
const icon = fs.readFileSync(iconPath);
const pngSignature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
const assert = (condition, message) => {
  if (!condition) throw new Error("Application icon contract: " + message);
};

assert(project.includes("<ApplicationIcon>Assets\\ChromeBookmarksManager.ico</ApplicationIcon>"),
  "EXE ApplicationIcon must point to the checked ICO resource.");
assert(project.includes('<Resource Include="Assets\\ChromeBookmarksManager.ico" />'),
  "The ICO must be compiled as a WPF resource.");
assert(windowXaml.includes('Icon="Assets/ChromeBookmarksManager.ico"'),
  "MainWindow must use the same ICO as the EXE.");

function crc32(buffer) {
  let crc = 0xffffffff;
  for (const byte of buffer) {
    crc ^= byte;
    for (let i = 0; i < 8; i += 1) {
      crc = (crc >>> 1) ^ ((crc & 1) ? 0xedb88320 : 0);
    }
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function verifyPng(frame, size) {
  assert(frame.subarray(0, 8).equals(pngSignature),
    size + "px frame is not a PNG.");
  let cursor = 8;
  let sawHeader = false;
  let sawEnd = false;
  const imageData = [];

  while (cursor + 12 <= frame.length && !sawEnd) {
    const length = frame.readUInt32BE(cursor);
    assert(length <= frame.length - cursor - 12,
      size + "px PNG chunk extends past the frame.");
    const type = frame.toString("ascii", cursor + 4, cursor + 8);
    const data = frame.subarray(cursor + 8, cursor + 8 + length);
    const expectedCrc = frame.readUInt32BE(cursor + 8 + length);
    const actualCrc = crc32(frame.subarray(cursor + 4, cursor + 8 + length));
    assert(expectedCrc === actualCrc,
      size + "px PNG " + type + " CRC mismatch.");

    if (type === "IHDR") {
      assert(!sawHeader && cursor === 8 && length === 13,
        size + "px PNG must start with one IHDR.");
      assert(data.readUInt32BE(0) === size && data.readUInt32BE(4) === size,
        size + "px ICO directory and PNG dimensions disagree.");
      assert(data[8] === 8 && data[9] === 6 &&
        data[10] === 0 && data[11] === 0 && data[12] === 0,
      size + "px PNG must be non-interlaced RGBA 8-bit.");
      sawHeader = true;
    } else if (type === "IDAT") {
      assert(sawHeader, size + "px IDAT precedes IHDR.");
      imageData.push(data);
    } else if (type === "IEND") {
      assert(length === 0, size + "px invalid IEND.");
      sawEnd = true;
    }
    cursor += 12 + length;
  }

  assert(sawHeader && sawEnd && cursor === frame.length && imageData.length > 0,
    size + "px PNG is truncated or has trailing/invalid data.");

  let inflated;
  try {
    inflated = inflateSync(Buffer.concat(imageData));
  } catch (error) {
    throw new Error(size + "px PNG IDAT decompression failed: " + error.message);
  }
  const stride = size * 4;
  assert(inflated.length === size * (stride + 1),
    size + "px RGBA scanline size mismatch.");

  const pixels = Buffer.alloc(size * stride);
  let input = 0;
  let topVisible = 0;
  let bottomVisible = 0;

  for (let y = 0; y < size; y += 1) {
    const filter = inflated[input++];
    assert(filter >= 0 && filter <= 4, size + "px PNG has invalid filter.");
    for (let x = 0; x < stride; x += 1) {
      const index = y * stride + x;
      const left = x >= 4 ? pixels[index - 4] : 0;
      const above = y > 0 ? pixels[index - stride] : 0;
      const upperLeft = y > 0 && x >= 4 ? pixels[index - stride - 4] : 0;
      let prediction = 0;
      if (filter === 1) prediction = left;
      else if (filter === 2) prediction = above;
      else if (filter === 3) prediction = (left + above) >>> 1;
      else if (filter === 4) {
        const p = left + above - upperLeft;
        const a = Math.abs(p - left);
        const b = Math.abs(p - above);
        const c = Math.abs(p - upperLeft);
        prediction = a <= b && a <= c ? left : b <= c ? above : upperLeft;
      }
      pixels[index] = (inflated[input++] + prediction) & 255;
    }
    for (let x = 3; x < stride; x += 4) {
      if (pixels[y * stride + x] > 20) {
        if (y < size / 2) topVisible += 1;
        else bottomVisible += 1;
      }
    }
  }
  assert(topVisible > size * size / 16 && bottomVisible > size * size / 16,
    size + "px icon appears transparent/clipped in one half.");
}

assert(icon.length >= 6, "ICO header is missing.");
assert(icon.readUInt16LE(0) === 0 && icon.readUInt16LE(2) === 1,
  "Invalid ICO header.");
const count = icon.readUInt16LE(4);
assert(count === requiredSizes.length, "Unexpected ICO frame count: " + count + ".");
let nextOffset = 6 + count * 16;
const sizes = [];

for (let i = 0; i < count; i += 1) {
  const entry = 6 + i * 16;
  assert(entry + 16 <= icon.length, "ICO entry " + i + " is truncated.");
  const size = icon[entry] || 256;
  const height = icon[entry + 1] || 256;
  const planes = icon.readUInt16LE(entry + 4);
  const depth = icon.readUInt16LE(entry + 6);
  const length = icon.readUInt32LE(entry + 8);
  const offset = icon.readUInt32LE(entry + 12);

  assert(size === height && size === requiredSizes[i],
    "Missing or out-of-order " + requiredSizes[i] + "px square icon.");
  assert(planes === 1 && depth === 32,
    size + "px ICO entry must be 32-bit RGBA.");
  assert(offset === nextOffset && length > 0 && length <= icon.length - offset,
    size + "px ICO offset/length points to incomplete or overlapping data.");
  verifyPng(icon.subarray(offset, offset + length), size);
  nextOffset += length;
  sizes.push(size);
}
assert(nextOffset === icon.length, "ICO contains trailing or unindexed bytes.");
console.log("Application icon verified: " + sizes.join(", ") +
  "px; PNG CRC, dimensions, decompression, alpha coverage and ICO offsets passed.");
