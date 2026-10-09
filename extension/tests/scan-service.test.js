import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

import { ClassificationError } from "../src/classification.js";
import {
  LINK_VERDICTS,
  MAX_LINK_LENGTH,
  MAX_LINKS_PER_REQUEST,
  RATE_LIMIT_PAUSE_MS,
  SCAN_MESSAGE_TYPES,
  SCAN_MIN_INTERVAL_MS,
} from "../src/constants.js";
import { PAGE_SCAN_TEXT, REASON_DESCRIPTIONS } from "../src/messages.js";
import { createScanService, inspectableLink, toLinkWarning } from "../src/scan-service.js";

const API = "http://localhost:8080";
const SCAM_RESULT = Object.freeze({ isScam: true, confidence: "93%", reasons: ["Có link rút gọn"], advice: "Đừng bấm link" });

function fakeClock() {
  let current = 1_000;
  const waits = [];
  return {
    now: () => current,
    wait: async (milliseconds) => {
      waits.push(milliseconds);
      current += milliseconds;
    },
    advance: (milliseconds) => {
      current += milliseconds;
    },
    waits,
  };
}

function createService(overrides = {}) {
  const calls = { classify: [], inspect: [] };
  const clock = fakeClock();
  const service = createScanService({
    classifyText: async (baseUrl, text) => {
      calls.classify.push({ baseUrl, text });
      return SCAM_RESULT;
    },
    inspect: async (baseUrl, urls) => {
      calls.inspect.push({ baseUrl, urls });
      return urls.map((url) => ({ url, verdict: LINK_VERDICTS.dangerous, reasons: ["url_blocklisted"] }));
    },
    loadBaseUrl: async () => API,
    now: clock.now,
    wait: clock.wait,
    ...overrides,
  });
  return { service, calls, clock };
}

test("checkMessage masks personal data on the device before calling the API", async () => {
  const { service, calls } = createService();

  await service.checkMessage("  Chuyen 5 trieu vao STK 1234567890123, goi 0901234567  ");

  assert.deepEqual(calls.classify, [{ baseUrl: API, text: "Chuyen 5 trieu vao STK <ACCOUNT>, goi <PHONE>" }]);
});

test("checkMessage returns a page warning with texts from the extension", async () => {
  const { service } = createService();

  const warning = await service.checkMessage("Tai khoan bi khoa, bam link de mo");

  assert.equal(warning.isScam, true);
  assert.equal(warning.title, PAGE_SCAN_TEXT.messageWarningTitle);
  assert.equal(warning.confidence, SCAM_RESULT.confidence);
  assert.deepEqual(warning.reasons, SCAM_RESULT.reasons);
  assert.equal(warning.dismissLabel, PAGE_SCAN_TEXT.dismiss);
});

test("checkMessage caches by masked text so repeated messages call the API once", async () => {
  const { service, calls } = createService();

  await service.checkMessage("Goi 0901234567 de nhan qua tang");
  await service.checkMessage("Goi 0912345678 de nhan qua tang");

  assert.equal(calls.classify.length, 1);
});

test("checkMessage ignores empty and too long text", async () => {
  const { service, calls } = createService();

  assert.equal(await service.checkMessage("   "), null);
  assert.equal(await service.checkMessage("a".repeat(2001)), null);
  assert.equal(calls.classify.length, 0);
});

test("requests are spaced by the minimum interval", async () => {
  const { service, clock } = createService();

  await service.checkMessage("Tin nhan thu nhat can kiem tra");
  await service.checkMessage("Tin nhan thu hai can kiem tra");

  assert.deepEqual(clock.waits, [SCAN_MIN_INTERVAL_MS]);
});

test("a rate limited response pauses scanning before the next request", async () => {
  let attempts = 0;
  const { service, clock } = createService({
    classifyText: async () => {
      attempts += 1;
      if (attempts === 1) {
        throw new ClassificationError("rate limited", 429);
      }
      return SCAM_RESULT;
    },
  });

  await assert.rejects(service.checkMessage("Tin nhan bi gioi han toc do"), ClassificationError);
  await service.checkMessage("Tin nhan sau khi doi");

  assert.deepEqual(clock.waits, [RATE_LIMIT_PAUSE_MS]);
});

test("checkLinks sends only unique web links in batches and caches results", async () => {
  const { service, calls } = createService();
  const urls = Array.from({ length: MAX_LINKS_PER_REQUEST + 5 }, (_, index) => `https://site${index}.example/`);

  const warnings = await service.checkLinks([...urls, urls[0], "javascript:alert(1)", "mailto:a@b.example", "not a url"]);
  await service.checkLinks([urls[0]]);

  assert.deepEqual(
    calls.inspect.map((call) => call.urls.length),
    [MAX_LINKS_PER_REQUEST, 5],
  );
  assert.equal(warnings.length, urls.length);
});

test("toLinkWarning describes dangerous links and asks before opening them", () => {
  const warning = toLinkWarning({ url: "https://a.example", verdict: LINK_VERDICTS.dangerous, reasons: ["url_blocklisted"] });

  assert.equal(warning.title, `${PAGE_SCAN_TEXT.dangerousLinkTitle}\n${REASON_DESCRIPTIONS.url_blocklisted}`);
  assert.equal(warning.confirmText, PAGE_SCAN_TEXT.dangerousLinkConfirm);
});

test("toLinkWarning leaves safe links unmarked and suspicious links without a confirmation", () => {
  assert.equal(toLinkWarning({ url: "https://a.example", verdict: LINK_VERDICTS.safe, reasons: [] }).title, null);
  const suspicious = toLinkWarning({ url: "https://bit.ly/a", verdict: LINK_VERDICTS.suspicious, reasons: ["url_shortener"] });
  assert.ok(suspicious.title.startsWith(PAGE_SCAN_TEXT.suspiciousLinkTitle));
  assert.equal(suspicious.confirmText, null);
});

test("the content script uses the same message types and verdicts as the extension", () => {
  const contentScript = readFileSync(new URL("../src/content/page-scanner.js", import.meta.url), "utf8");

  for (const messageType of Object.values(SCAN_MESSAGE_TYPES)) {
    assert.ok(contentScript.includes(`"${messageType}"`), messageType);
  }
  assert.ok(contentScript.includes(`"${LINK_VERDICTS.dangerous}"`));
  assert.ok(contentScript.includes(`"${LINK_VERDICTS.suspicious}"`));
});

test("identical messages arriving together share one API call", async () => {
  const { service, calls } = createService();

  const [first, second] = await Promise.all([
    service.checkMessage("Hai tin giong het nhau den cung luc"),
    service.checkMessage("Hai tin giong het nhau den cung luc"),
  ]);

  assert.equal(calls.classify.length, 1);
  assert.equal(first, second);
});

test("a failed link batch does not lose the results of other batches", async () => {
  let batches = 0;
  const { service } = createService({
    inspect: async (_, urls) => {
      batches += 1;
      if (batches === 1) {
        throw new ClassificationError("too large", 413);
      }
      return urls.map((url) => ({ url, verdict: LINK_VERDICTS.safe, reasons: [] }));
    },
  });
  const urls = Array.from({ length: MAX_LINKS_PER_REQUEST + 2 }, (_, index) => `https://site${index}.example/`);

  const warnings = await service.checkLinks(urls);

  assert.equal(warnings.length, 2);
});

test("inspectableLink keeps only origin and path of web links within the length limit", () => {
  assert.equal(inspectableLink("https://user:pw@a.example/reset?token=1#x"), "https://a.example/reset");
  assert.equal(inspectableLink("http://a.example"), "http://a.example/");
  assert.equal(inspectableLink(`https://a.example/${"x".repeat(MAX_LINK_LENGTH)}`), null);
  assert.equal(inspectableLink("javascript:alert(1)"), null);
  assert.equal(inspectableLink(42), null);
});

test("checkLinks never sends query strings or fragments to the API", async () => {
  const { service, calls } = createService();

  await service.checkLinks(["https://a.example/reset?token=secret#frag"]);

  assert.deepEqual(calls.inspect[0].urls, ["https://a.example/reset"]);
});

