import assert from "node:assert/strict";
import { test } from "node:test";

import { SCANNER_SCRIPT_FILE, SCANNER_SCRIPT_ID, STORAGE_KEYS } from "../src/constants.js";
import { injectIntoOpenTabs, syncPageScanner } from "../src/scanner-registration.js";
import {
  loadCustomSites,
  loadScanOrigins,
  normalizeSiteOrigin,
  originPattern,
  saveCustomSites,
  saveScanOrigins,
} from "../src/site-settings.js";

function fakeStorage() {
  const values = {};
  return {
    values,
    get: async (key) => (key in values ? { [key]: values[key] } : {}),
    set: async (items) => Object.assign(values, items),
  };
}

function fakeScripting(registered = []) {
  const calls = { registered: [], updated: [], unregistered: [], executed: [] };
  return {
    calls,
    getRegisteredContentScripts: async () => registered,
    unregisterContentScripts: async (filter) => calls.unregistered.push(filter),
    registerContentScripts: async (scripts) => calls.registered.push(...scripts),
    updateContentScripts: async (scripts) => calls.updated.push(...scripts),
    executeScript: async (injection) => calls.executed.push(injection),
  };
}

test("normalizeSiteOrigin accepts https and loopback http origins only", () => {
  assert.equal(normalizeSiteOrigin(" https://chat.zalo.me/abc "), "https://chat.zalo.me");
  assert.equal(normalizeSiteOrigin("http://localhost:8080/chat"), "http://localhost:8080");
  assert.equal(normalizeSiteOrigin("http://chat.example.vn"), null);
  assert.equal(normalizeSiteOrigin("chat.zalo.me"), null);
  assert.equal(normalizeSiteOrigin(undefined), null);
});

test("scan origins are saved sorted without duplicates", async () => {
  const storage = fakeStorage();

  await saveScanOrigins(["https://web.telegram.org", "https://chat.zalo.me", "https://chat.zalo.me"], storage);

  assert.deepEqual(storage.values[STORAGE_KEYS.scanOrigins], ["https://chat.zalo.me", "https://web.telegram.org"]);
  assert.deepEqual(await loadScanOrigins(storage), ["https://chat.zalo.me", "https://web.telegram.org"]);
  assert.deepEqual(await loadScanOrigins(fakeStorage()), []);
});

test("syncPageScanner registers the scanner only for origins with granted permission", async () => {
  const scripting = fakeScripting();
  const permissions = { contains: async ({ origins }) => origins[0] !== originPattern("https://mail.google.com") };

  const enabled = await syncPageScanner(["https://chat.zalo.me", "https://mail.google.com"], { scripting, permissions });

  assert.deepEqual(enabled, ["https://chat.zalo.me"]);
  assert.equal(scripting.calls.registered.length, 1);
  assert.deepEqual(scripting.calls.registered[0].matches, ["https://chat.zalo.me/*"]);
  assert.deepEqual(scripting.calls.registered[0].js, [SCANNER_SCRIPT_FILE]);
  assert.deepEqual(scripting.calls.unregistered, []);
});

test("syncPageScanner updates an existing registration instead of registering a duplicate", async () => {
  const scripting = fakeScripting([{ id: SCANNER_SCRIPT_ID }]);

  await syncPageScanner(["https://web.telegram.org"], { scripting, permissions: { contains: async () => true } });

  assert.deepEqual(scripting.calls.registered, []);
  assert.deepEqual(scripting.calls.updated[0].matches, ["https://web.telegram.org/*"]);
});

test("syncPageScanner unregisters the scanner when the last site is disabled", async () => {
  const scripting = fakeScripting([{ id: SCANNER_SCRIPT_ID }]);

  await syncPageScanner([], { scripting, permissions: { contains: async () => true } });

  assert.deepEqual(scripting.calls.unregistered, [{ ids: [SCANNER_SCRIPT_ID] }]);
});

test("syncPageScanner leaves nothing registered when no site is enabled", async () => {
  const scripting = fakeScripting();

  const enabled = await syncPageScanner([], { scripting, permissions: { contains: async () => true } });

  assert.deepEqual(enabled, []);
  assert.deepEqual(scripting.calls.registered, []);
  assert.deepEqual(scripting.calls.unregistered, []);
});

test("injectIntoOpenTabs runs the scanner in already open tabs of enabled sites", async () => {
  const scripting = fakeScripting();
  const queries = [];
  const tabs = { query: async (filter) => (queries.push(filter), [{ id: 7 }, { id: 9 }]) };

  await injectIntoOpenTabs(["https://chat.zalo.me"], { tabs, scripting });
  await injectIntoOpenTabs([], { tabs, scripting });

  assert.deepEqual(queries, [{ url: ["https://chat.zalo.me/*"] }]);
  assert.deepEqual(
    scripting.calls.executed.map((injection) => injection.target.tabId),
    [7, 9],
  );
});

test("custom sites are stored separately so a disabled custom site stays listed", async () => {
  const storage = fakeStorage();

  await saveCustomSites(["https://chat.example.vn", "https://chat.example.vn"], storage);
  await saveScanOrigins([], storage);

  assert.deepEqual(await loadCustomSites(storage), ["https://chat.example.vn"]);
  assert.deepEqual(await loadScanOrigins(storage), []);
});

