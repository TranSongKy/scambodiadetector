import assert from "node:assert/strict";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { after, before, test } from "node:test";
import { fileURLToPath } from "node:url";

import { chromium } from "playwright";

const EXTENSION_PATH = join(dirname(fileURLToPath(import.meta.url)), "..");
const API_ROUTE = "http://localhost:8080/api/v1/**";
const OCR_TIMEOUT_MS = 60_000;
const MESSAGES = {
  light: {
    text: "Vietcombank: Tài khoản của quý khách bị tạm khóa do đăng nhập bất thường. Vui lòng xác minh tại vcb-xacminh.com trong 24h.",
    background: "#e5ddd5",
    bubble: "#ffffff",
    color: "#111111",
  },
  dark: {
    text: "Chúc mừng bạn đã trúng thưởng xe SH. Vui lòng chuyển phí nhận thưởng 2.000.000đ vào STK 1234567890123.",
    background: "#0b141a",
    bubble: "#202c33",
    color: "#e9edef",
  },
};

let context;
let page;
let userDataDir;
const apiRequests = [];

function normalize(text) {
  return text.replace(/\s+/g, " ").trim();
}

async function renderScreenshot(message) {
  const renderer = await context.newPage();
  await renderer.setViewportSize({ width: 420, height: 220 });
  await renderer.setContent(
    `<body style="margin:0;background:${message.background};font-family:'DejaVu Sans',sans-serif">` +
      `<div style="margin:16px;padding:12px;background:${message.bubble};border-radius:12px;font-size:15px;line-height:1.4;color:${message.color}">${message.text}</div></body>`,
  );
  const image = await renderer.screenshot();
  await renderer.close();
  return image.toString("base64");
}

async function pasteImage(base64) {
  await page.evaluate((data) => {
    const bytes = Uint8Array.from(atob(data), (character) => character.charCodeAt(0));
    const transfer = new DataTransfer();
    transfer.items.add(new File([bytes], "screenshot.png", { type: "image/png" }));
    document.dispatchEvent(new ClipboardEvent("paste", { clipboardData: transfer, bubbles: true }));
  }, base64);
  await page.waitForFunction(() => document.getElementById("ocr-status").textContent.startsWith("Đã đọc"), null, {
    timeout: OCR_TIMEOUT_MS,
  });
  return page.inputValue("#message");
}

before(async () => {
  userDataDir = mkdtempSync(join(tmpdir(), "scambodia-extension-"));
  context = await chromium.launchPersistentContext(userDataDir, {
    channel: "chromium",
    headless: true,
    args: [`--disable-extensions-except=${EXTENSION_PATH}`, `--load-extension=${EXTENSION_PATH}`],
  });
  await context.route(API_ROUTE, async (route) => {
    const request = route.request();
    apiRequests.push({ url: request.url(), body: request.postDataJSON() });
    const isReport = request.url().endsWith("/reports");
    await route.fulfill({
      status: isReport ? 201 : 200,
      contentType: "application/json",
      body: JSON.stringify(
        isReport ? { id: "0199a0c0-0000-7000-8000-000000000001" } : { label: "scam", confidence: 0.93, reasons: ["url_shortener"] },
      ),
    });
  });
  const worker = context.serviceWorkers()[0] ?? (await context.waitForEvent("serviceworker"));
  page = await context.newPage();
  await page.setViewportSize({ width: 380, height: 900 });
  await page.goto(`chrome-extension://${new URL(worker.url()).host}/src/panel.html`);
});

after(async () => {
  await context?.close();
  rmSync(userDataDir, { recursive: true, force: true });
});

test("pasting a light screenshot fills the message with the recognized Vietnamese text", async () => {
  const text = await pasteImage(await renderScreenshot(MESSAGES.light));

  assert.equal(normalize(text), MESSAGES.light.text);
  assert.ok(await page.isVisible("#preview-image"));
});

test("pasting a dark-mode screenshot is recognized too", async () => {
  const text = await pasteImage(await renderScreenshot(MESSAGES.dark));

  assert.equal(normalize(text), MESSAGES.dark.text);
});

test("checking sends only the text to the API and shows the verdict", async () => {
  apiRequests.length = 0;

  await page.click("#check");
  await page.waitForSelector("#result:not([hidden])");

  assert.equal(apiRequests.length, 1);
  assert.deepEqual(apiRequests[0].body, { text: await page.inputValue("#message") });
  assert.match(await page.textContent("#verdict"), /LỪA ĐẢO/);
});

test("reporting a wrong result posts the label with the extension channel", async () => {
  apiRequests.length = 0;

  await page.click("#report-buttons button:nth-child(3)");
  await page.waitForFunction(() => document.getElementById("report-status").textContent.startsWith("Cảm ơn"));

  assert.equal(apiRequests[0].body.label, "normal");
  assert.equal(apiRequests[0].body.channel, "extension");
});

test("selected text sent from the context menu is checked and hides the old screenshot", async () => {
  apiRequests.length = 0;

  await page.evaluate(() => chrome.storage.session.set({ pendingText: "Click bit.ly/qua-tang de nhan qua tri an" }));
  await page.waitForFunction(() => document.getElementById("message").value.startsWith("Click bit.ly"));
  await page.waitForFunction(() => document.querySelectorAll("#reasons li").length > 0);

  assert.equal(apiRequests.at(-1).body.text, "Click bit.ly/qua-tang de nhan qua tri an");
  assert.ok(!(await page.isVisible("#image-preview")));
});

test("pasting a non-image file shows an error instead of reading it", async () => {
  await page.evaluate(() => {
    const transfer = new DataTransfer();
    transfer.items.add(new File(["hello"], "note.txt", { type: "text/plain" }));
    document.getElementById("drop-zone").dispatchEvent(new DragEvent("drop", { dataTransfer: transfer, bubbles: true }));
  });

  assert.equal(await page.textContent("#error"), "Tệp này không phải ảnh. Hãy dán ảnh chụp màn hình (PNG, JPG...).");
  assert.ok(await page.isVisible("#error"));
});
