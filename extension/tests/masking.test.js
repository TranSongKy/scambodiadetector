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

test("maskPersonalData keeps links so the backend can still inspect them", () => {
  assert.equal(
    maskPersonalData("Goi 0901234567 hoac vao https://vcb-xacminh.top/0901234567 ngay"),
    "Goi <PHONE> hoac vao https://vcb-xacminh.top/0901234567 ngay",
  );
  assert.equal(maskPersonalData("Tra cuu tai vtp-vandon.online/don/123456789012"), "Tra cuu tai vtp-vandon.online/don/123456789012");
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
