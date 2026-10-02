import assert from "node:assert/strict";
import { test } from "node:test";

import { MAX_IMAGE_BYTES } from "../src/constants.js";
import {
  averageLuminance,
  findImageFile,
  invertColors,
  isDarkImage,
  normalizeOcrText,
  upscaleFactor,
  validateImageFile,
} from "../src/image-input.js";
import { ERROR_MESSAGES } from "../src/messages.js";

function solidPixels(red, green, blue, count = 64) {
  const pixels = new Uint8ClampedArray(count * 4);
  for (let index = 0; index < pixels.length; index += 4) {
    pixels.set([red, green, blue, 255], index);
  }
  return pixels;
}

test("validateImageFile accepts images within the size limit", () => {
  assert.equal(validateImageFile({ type: "image/png", size: 1024 }), null);
  assert.equal(validateImageFile({ type: "image/jpeg", size: MAX_IMAGE_BYTES }), null);
});

test("validateImageFile rejects non-images, missing files and oversized images", () => {
  assert.equal(validateImageFile({ type: "text/plain", size: 10 }), ERROR_MESSAGES.notAnImage);
  assert.equal(validateImageFile(null), ERROR_MESSAGES.notAnImage);
  assert.equal(validateImageFile({ type: "image/png", size: MAX_IMAGE_BYTES + 1 }), ERROR_MESSAGES.imageTooLarge);
});

test("findImageFile prefers image files and ignores text items", () => {
  const image = { type: "image/png", name: "shot.png" };
  const fromFiles = findImageFile({ files: [{ type: "text/plain" }, image], items: [] });
  const fromItems = findImageFile({
    files: [],
    items: [
      { kind: "string", type: "text/plain", getAsFile: () => null },
      { kind: "file", type: "image/jpeg", getAsFile: () => image },
    ],
  });

  assert.equal(fromFiles, image);
  assert.equal(fromItems, image);
  assert.equal(findImageFile({ files: [], items: [] }), null);
  assert.equal(findImageFile(undefined), null);
});

test("dark screenshots are detected and light ones are not", () => {
  assert.ok(isDarkImage(solidPixels(20, 20, 24)));
  assert.ok(!isDarkImage(solidPixels(240, 240, 240)));
  assert.equal(averageLuminance(new Uint8ClampedArray(0)), 1);
});

test("invertColors flips RGB and keeps alpha", () => {
  const pixels = invertColors(new Uint8ClampedArray([0, 100, 255, 128]));

  assert.deepEqual([...pixels], [255, 155, 0, 128]);
});

test("normalizeOcrText joins wrapped lines and keeps paragraph breaks", () => {
  const raw = "Tài khoản của quý khách\nbị tạm khóa.  \n\n  Vui lòng xác minh\n tại <URL>\n";

  assert.equal(normalizeOcrText(raw), "Tài khoản của quý khách bị tạm khóa.\nVui lòng xác minh tại <URL>");
  assert.equal(normalizeOcrText(undefined), "");
});

test("upscaleFactor enlarges small screenshots up to the maximum", () => {
  assert.equal(upscaleFactor(1600, 1200, 3), 1);
  assert.equal(upscaleFactor(700, 1200, 3), 2);
  assert.equal(upscaleFactor(200, 1200, 3), 3);
});
