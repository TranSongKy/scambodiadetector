import assert from "node:assert/strict";
import { mkdtempSync, rmSync } from "node:fs";
import { createServer } from "node:http";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { after, before, test } from "node:test";
import { fileURLToPath } from "node:url";

import { chromium } from "playwright";

const EXTENSION_PATH = join(dirname(fileURLToPath(import.meta.url)), "..");
const SITE_PORT = 8080;
const SITE_ORIGIN = `http://localhost:${SITE_PORT}`;
const CHAT_URL = `${SITE_ORIGIN}/e2e/chat.html`;
const BLOCKED_HOST = "vtp-vandon.online";
const EXISTING_MESSAGE = "Tin nhắn cũ đã có sẵn trên trang trước khi bật quét tự động";
const SCAM_MESSAGE = "Mẹ ơi con đang cấp cứu, chuyển khoản gấp 5 triệu vào số 0901234567 giúp con";
const NORMAL_MESSAGE = "Tối nay 7 giờ cả nhà mình đi ăn lẩu ở quán cũ nhé";
const CHAT_PAGE = `<!doctype html><html lang="vi"><head><meta charset="utf-8"><title>Chat</title></head><body>
  <main id="messages">
    <div class="bubble">${EXISTING_MESSAGE}</div>
    <div class="bubble">Tra cứu đơn hàng tại <a id="bad-link" href="https://${BLOCKED_HOST}/don">đây</a></div>
    <div class="bubble">Tài liệu ở <a id="good-link" href="https://example.org/tai-lieu">example.org</a></div>
  </main>
  <textarea id="composer">Đang soạn một tin nhắn dài chưa gửi đi cho ai cả</textarea>
</body></html>`;
const WAIT_TIMEOUT_MS = 15_000;

let context;
let server;
let extensionId;
const classifyRequests = [];
const linkRequests = [];

function sendJson(response, body) {
  response.writeHead(200, { "content-type": "application/json" });
  response.end(JSON.stringify(body));
}

function inspectLinks(urls) {
  linkRequests.push(urls);
  return urls.map((url) =>
    new URL(url).host === BLOCKED_HOST
      ? { url, verdict: "dangerous", reasons: ["url_blocklisted"] }
      : { url, verdict: "safe", reasons: [] },
  );
}

function classify(text) {
  classifyRequests.push(text);
  const isScam = text.includes("chuyển khoản");
  return { label: isScam ? "scam" : "normal", confidence: 0.91, reasons: isScam ? ["model_predicted_scam"] : [] };
}

async function readJson(request) {
  const chunks = [];
  for await (const chunk of request) {
    chunks.push(chunk);
  }
  return JSON.parse(Buffer.concat(chunks).toString("utf8"));
}

async function handleRequest(request, response) {
  if (request.url === "/e2e/chat.html") {
    response.writeHead(200, { "content-type": "text/html; charset=utf-8" });
    response.end(CHAT_PAGE);
  } else if (request.url === "/api/v1/link-inspections") {
    sendJson(response, { results: inspectLinks((await readJson(request)).urls) });
  } else if (request.url === "/api/v1/classifications") {
    sendJson(response, classify((await readJson(request)).text));
  } else {
    response.writeHead(404);
    response.end();
  }
}

async function setSiteEnabled(enabled) {
  const options = await context.newPage();
  await options.goto(`chrome-extension://${extensionId}/src/options.html`);
  const checkbox = options.locator(`input[data-origin="${SITE_ORIGIN}"]`);
  if ((await checkbox.isChecked()) !== enabled) {
    await checkbox.click();
    const expectedStatus = enabled ? "Đã bật" : "Đã tắt";
    await options.waitForFunction((status) => document.getElementById("scan-status").textContent.startsWith(status), expectedStatus);
  }
  await options.close();
}

async function openChat() {
  const page = await context.newPage();
  await page.goto(CHAT_URL);
  await page.waitForSelector('#bad-link[data-scambodia-verdict="dangerous"]', { timeout: WAIT_TIMEOUT_MS });
  return page;
}

async function addMessage(page, text) {
  await page.evaluate((messageText) => {
    const bubble = document.createElement("div");
    bubble.className = "bubble";
    bubble.textContent = messageText;
    document.getElementById("messages").append(bubble);
  }, text);
}

before(async () => {
  server = createServer(handleRequest);
  await new Promise((resolve) => server.listen(SITE_PORT, "127.0.0.1", resolve));
  const userDataDir = mkdtempSync(join(tmpdir(), "scambodia-scanner-"));
  context = await chromium.launchPersistentContext(userDataDir, {
    channel: "chromium",
    headless: true,
    args: [`--disable-extensions-except=${EXTENSION_PATH}`, `--load-extension=${EXTENSION_PATH}`],
  });
  context.userDataDir = userDataDir;
  const worker = context.serviceWorkers()[0] ?? (await context.waitForEvent("serviceworker"));
  extensionId = new URL(worker.url()).host;

  const options = await context.newPage();
  await options.goto(`chrome-extension://${extensionId}/src/options.html`);
  await options.fill("#custom-site", SITE_ORIGIN);
  await options.click("#add-site");
  await options.waitForFunction(() => document.getElementById("scan-status").textContent.startsWith("Đã bật"));
  await options.close();
});

after(async () => {
  await context?.close();
  await new Promise((resolve) => server?.close(resolve));
  rmSync(context.userDataDir, { recursive: true, force: true });
});

test("dangerous links on an enabled chat page are marked and safe links are left alone", async () => {
  const page = await context.newPage();
  await page.goto(CHAT_URL);

  await page.waitForSelector('#bad-link[data-scambodia-verdict="dangerous"]', { timeout: WAIT_TIMEOUT_MS });

  assert.match(await page.getAttribute("#bad-link", "title"), /link nguy hiểm/);
  assert.equal(await page.getAttribute("#good-link", "data-scambodia-verdict"), null);
  assert.deepEqual(linkRequests.flat().sort(), ["https://example.org/tai-lieu", `https://${BLOCKED_HOST}/don`]);
  await page.close();
});

test("a new scam message shows a warning card and is sent with personal data masked", async () => {
  const page = await context.newPage();
  await page.goto(CHAT_URL);
  await page.waitForSelector('#bad-link[data-scambodia-verdict="dangerous"]', { timeout: WAIT_TIMEOUT_MS });
  classifyRequests.length = 0;

  await addMessage(page, NORMAL_MESSAGE);
  await addMessage(page, SCAM_MESSAGE);
  const card = page.locator('[data-scambodia="warnings"] .card');
  await card.first().waitFor({ timeout: WAIT_TIMEOUT_MS });

  assert.equal(await card.count(), 1);
  assert.match(await card.locator(".title").textContent(), /lừa đảo \(91%\)/);
  assert.match(await card.locator(".quote").textContent(), /cấp cứu/);
  assert.ok(classifyRequests.includes(NORMAL_MESSAGE));
  assert.ok(classifyRequests.includes("Mẹ ơi con đang cấp cứu, chuyển khoản gấp 5 triệu vào số <PHONE> giúp con"));
  assert.ok(!classifyRequests.some((text) => text.includes(EXISTING_MESSAGE) || text.includes("Đang soạn")));
  await page.close();
});

test("clicking a dangerous link asks for confirmation and stays on the page when declined", async () => {
  const page = await context.newPage();
  await page.goto(CHAT_URL);
  await page.waitForSelector('#bad-link[data-scambodia-verdict="dangerous"]', { timeout: WAIT_TIMEOUT_MS });
  const dialogs = [];
  page.on("dialog", async (dialog) => {
    dialogs.push(dialog.message());
    await dialog.dismiss();
  });

  await page.click("#bad-link");

  assert.equal(dialogs.length, 1);
  assert.match(dialogs[0], /dấu hiệu lừa đảo/);
  assert.equal(page.url(), CHAT_URL);
  await page.close();
});

test("text typed into rich composers is never sent", async () => {
  const page = await openChat();
  classifyRequests.length = 0;

  await page.evaluate(() => {
    const plainComposer = document.createElement("div");
    plainComposer.setAttribute("contenteditable", "plaintext-only");
    plainComposer.textContent = "Bản nháp riêng tư trong ô soạn plaintext chuyển khoản";
    const roleComposer = document.createElement("div");
    roleComposer.setAttribute("role", "textbox");
    roleComposer.textContent = "Bản nháp riêng tư trong ô soạn role textbox chuyển khoản";
    document.body.append(plainComposer, roleComposer);
  });
  await addMessage(page, SCAM_MESSAGE);
  await page.locator('[data-scambodia="warnings"] .card').first().waitFor({ timeout: WAIT_TIMEOUT_MS });

  assert.ok(!classifyRequests.some((text) => text.includes("Bản nháp")));
  await page.close();
});

test("middle-clicking a dangerous link also asks for confirmation", async () => {
  const page = await openChat();
  const dialogs = [];
  page.on("dialog", async (dialog) => {
    dialogs.push(dialog.message());
    await dialog.dismiss();
  });

  await page.click("#bad-link", { button: "middle" });

  assert.equal(dialogs.length, 1);
  await page.close();
});

test("re-enabling a site resumes scanning in an already open tab", async () => {
  const page = await openChat();
  await setSiteEnabled(false);
  await addMessage(page, "Tin nhắn trong lúc đã tắt quét tự động, chuyển khoản thử");
  await page.waitForTimeout(2_000);

  await setSiteEnabled(true);
  await page.waitForTimeout(1_000);
  classifyRequests.length = 0;
  await addMessage(page, SCAM_MESSAGE.replace("5 triệu", "7 triệu"));
  await page.locator('[data-scambodia="warnings"] .card').first().waitFor({ timeout: WAIT_TIMEOUT_MS });

  assert.ok(classifyRequests.some((text) => text.includes("7 triệu")));
  await page.close();
});

