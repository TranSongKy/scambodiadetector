import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

import {
  ClassificationError,
  classify,
  describeReason,
  formatResult,
  normalizeApiBaseUrl,
  report,
  validateText,
} from "../src/classification.js";
import { MAX_MESSAGE_LENGTH } from "../src/constants.js";
import { ERROR_MESSAGES, REASON_DESCRIPTIONS, SCAM_ADVICE, VERDICTS } from "../src/messages.js";

const API = "http://localhost:8080";

function jsonResponse(status, body) {
  return new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });
}

test("validateText rejects empty, whitespace and too long text", () => {
  assert.equal(validateText(""), ERROR_MESSAGES.emptyText);
  assert.equal(validateText("   "), ERROR_MESSAGES.emptyText);
  assert.equal(validateText(undefined), ERROR_MESSAGES.emptyText);
  assert.equal(validateText("a".repeat(MAX_MESSAGE_LENGTH + 1)), ERROR_MESSAGES.textTooLong);
  assert.equal(validateText("a".repeat(MAX_MESSAGE_LENGTH)), null);
});

test("normalizeApiBaseUrl allows https and loopback http only, stripping trailing slashes", () => {
  assert.equal(normalizeApiBaseUrl(" https://api.example.vn/ "), "https://api.example.vn");
  assert.equal(normalizeApiBaseUrl("http://localhost:8080/scam//"), "http://localhost:8080/scam");
  assert.equal(normalizeApiBaseUrl("http://127.0.0.1:5899"), "http://127.0.0.1:5899");
  assert.equal(normalizeApiBaseUrl("http://api.example.vn"), null);
  assert.equal(normalizeApiBaseUrl("ftp://example.vn"), null);
  assert.equal(normalizeApiBaseUrl("not a url"), null);
});

test("classify reports a timeout when the server does not answer in time", async () => {
  const hangingFetch = (_url, init) =>
    new Promise((_resolve, reject) => {
      const keepAlive = setTimeout(() => reject(new Error("fetch was not aborted")), 5000);
      init.signal.addEventListener("abort", () => {
        clearTimeout(keepAlive);
        reject(init.signal.reason);
      });
    });

  await assert.rejects(classify(API, "xin chao", hangingFetch, 10), (error) => {
    assert.equal(error.message, ERROR_MESSAGES.timeout);
    return true;
  });
});

test("formatResult builds Vietnamese scam verdict with unique reasons and advice", () => {
  const formatted = formatResult({
    label: "scam",
    confidence: 0.934,
    reasons: ["model_predicted_scam", "url_shortener", "url_shortener"],
  });

  assert.equal(formatted.verdict, VERDICTS.scam);
  assert.equal(formatted.confidence, "93%");
  assert.deepEqual(formatted.reasons, [REASON_DESCRIPTIONS.model_predicted_scam, REASON_DESCRIPTIONS.url_shortener]);
  assert.equal(formatted.advice, SCAM_ADVICE);
  assert.equal(formatted.isScam, true);
});

test("formatResult has no advice for normal messages", () => {
  const formatted = formatResult({ label: "normal", confidence: 0.8, reasons: [] });

  assert.equal(formatted.verdict, VERDICTS.normal);
  assert.equal(formatted.advice, null);
  assert.equal(formatted.isScam, false);
});

test("describeReason falls back to the code for unknown reasons", () => {
  assert.equal(describeReason("new_reason"), "new_reason");
});

test("classify posts JSON and formats the response", async () => {
  let captured;
  const fakeFetch = async (url, init) => {
    captured = { url, init };
    return jsonResponse(200, { label: "spam", confidence: 0.75, reasons: ["model_predicted_spam"] });
  };

  const formatted = await classify(API, "Giam gia 50%", fakeFetch);

  assert.equal(captured.url, "http://localhost:8080/api/v1/classifications");
  assert.equal(captured.init.method, "POST");
  assert.deepEqual(JSON.parse(captured.init.body), { text: "Giam gia 50%" });
  assert.equal(formatted.verdict, VERDICTS.spam);
});

test("classify maps problem codes, 503 and network errors to Vietnamese messages", async () => {
  const cases = [
    [async () => jsonResponse(400, { status: 400, code: "classification.text_too_long" }), ERROR_MESSAGES.textTooLong],
    [async () => jsonResponse(400, { status: 400, code: "classification.empty_text" }), ERROR_MESSAGES.emptyText],
    [async () => jsonResponse(503, { status: 503 }), ERROR_MESSAGES.modelUnavailable],
    [async () => new Response("oops", { status: 500 }), ERROR_MESSAGES.unexpected],
    [async () => { throw new TypeError("Failed to fetch"); }, ERROR_MESSAGES.network],
  ];

  for (const [fakeFetch, expectedMessage] of cases) {
    await assert.rejects(classify(API, "xin chao", fakeFetch), (error) => {
      assert.ok(error instanceof ClassificationError);
      assert.equal(error.message, expectedMessage);
      return true;
    });
  }
});

test("report posts text, label and extension channel and returns the id", async () => {
  let captured;
  const fakeFetch = async (url, init) => {
    captured = { url, body: JSON.parse(init.body) };
    return jsonResponse(201, { id: "0199a0c0-0000-7000-8000-000000000001" });
  };

  const id = await report(API, "Goi 0901234567", "scam", fakeFetch);

  assert.equal(captured.url, "http://localhost:8080/api/v1/reports");
  assert.deepEqual(captured.body, { text: "Goi 0901234567", label: "scam", channel: "extension" });
  assert.equal(id, "0199a0c0-0000-7000-8000-000000000001");
});

test("report maps 503 to the maintenance message", async () => {
  await assert.rejects(report(API, "x", "scam", async () => jsonResponse(503, { status: 503 })), (error) => {
    assert.equal(error.message, ERROR_MESSAGES.modelUnavailable);
    return true;
  });
});

test("reason descriptions cover every reason code defined in C#", () => {
  const sources = [
    "../src/ScamDetector.Core/Classification/ClassificationReasons.cs",
    "../src/ScamDetector.Core/Urls/UrlReasons.cs",
  ].map((path) => readFileSync(new URL(`../${path}`, import.meta.url), "utf8"));
  const codes = sources.flatMap((source) => [...source.matchAll(/const string \w+ = "([a-z_]+)";/g)].map((m) => m[1]));

  assert.ok(codes.length > 0);
  assert.deepEqual([...codes].sort(), Object.keys(REASON_DESCRIPTIONS).sort());
});

test("manifest is valid MV3 and points to existing files", () => {
  const manifest = JSON.parse(readFileSync(new URL("../manifest.json", import.meta.url), "utf8"));

  assert.equal(manifest.manifest_version, 3);
  for (const path of [manifest.background.service_worker, manifest.action.default_popup, manifest.options_page]) {
    assert.doesNotThrow(() => readFileSync(new URL(`../${path}`, import.meta.url)), path);
  }
});
