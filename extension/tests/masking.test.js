import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

import { PLACEHOLDERS, maskPersonalData } from "../src/masking.js";

const SHARED_CASES = JSON.parse(readFileSync(new URL("../../tests/shared/masking-cases.json", import.meta.url), "utf8"));
const URL_PLACEHOLDER = "<URL>";

function countPlaceholders(text) {
  return Object.fromEntries(Object.values(PLACEHOLDERS).map((placeholder) => [placeholder, text.split(placeholder).length - 1]));
}

test("maskPersonalData masks the same personal data as the backend masker on shared cases", () => {
  for (const sharedCase of SHARED_CASES) {
    const masked = maskPersonalData(sharedCase.input);
    assert.deepEqual(countPlaceholders(masked), countPlaceholders(sharedCase.expected), sharedCase.input);
    if (!sharedCase.expected.includes(URL_PLACEHOLDER)) {
      assert.equal(masked, sharedCase.expected, sharedCase.input);
    }
  }
});

test("maskPersonalData keeps the link host and path so the backend can still inspect them", () => {
  assert.equal(maskPersonalData("Tra cuu tai https://vtp-vandon.online/don/tra-cuu ngay"), "Tra cuu tai https://vtp-vandon.online/don/tra-cuu ngay");
});

test("maskPersonalData removes query, fragment, credentials and numbers inside links", () => {
  assert.equal(
    maskPersonalData("Vao https://user:pass@vcb-xacminh.top/reset?token=abc&email=a#x ngay"),
    "Vao https://vcb-xacminh.top/reset ngay",
  );
  assert.equal(maskPersonalData("Nhan tin zalo.me/0912345678 nhe"), "Nhan tin zalo.me/<ACCOUNT> nhe");
  assert.equal(maskPersonalData("Xem vtp-vandon.online/don/123456789012"), "Xem vtp-vandon.online/don/<ACCOUNT>");
});

test("maskPersonalData removes invisible characters before masking like the backend", () => {
  assert.equal(maskPersonalData("Goi 0912\u200b345678 ngay"), "Goi <PHONE> ngay");
});

test("maskPersonalData is not confused by NUL characters already in the text", () => {
  assert.equal(maskPersonalData("a\u00000\u0000b"), "a0b");
  assert.equal(maskPersonalData("\u00000\u0000 https://a.example/x"), "0 https://a.example/x");
});

test("maskPersonalData masks email before links so the domain inside an email is not kept", () => {
  assert.equal(maskPersonalData("Lien he hotro@vcb-xacminh.com"), "Lien he <EMAIL>");
});

test("maskPersonalData is idempotent", () => {
  for (const sharedCase of SHARED_CASES) {
    const masked = maskPersonalData(sharedCase.input);
    assert.equal(maskPersonalData(masked), masked);
  }
});
