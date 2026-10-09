import { SCANNER_SCRIPT_FILE, SCANNER_SCRIPT_ID } from "./constants.js";
import { originPattern } from "./site-settings.js";

export async function grantedOrigins(origins, permissions = chrome.permissions) {
  const checks = await Promise.all(origins.map((origin) => permissions.contains({ origins: [originPattern(origin)] })));
  return origins.filter((_, index) => checks[index]);
}

export async function syncPageScanner(origins, { scripting = chrome.scripting, permissions = chrome.permissions } = {}) {
  const enabledOrigins = await grantedOrigins(origins, permissions);
  const isRegistered = (await scripting.getRegisteredContentScripts({ ids: [SCANNER_SCRIPT_ID] })).length > 0;
  const script = {
    id: SCANNER_SCRIPT_ID,
    matches: enabledOrigins.map(originPattern),
    js: [SCANNER_SCRIPT_FILE],
    runAt: "document_idle",
    persistAcrossSessions: true,
  };
  if (enabledOrigins.length === 0) {
    if (isRegistered) {
      await scripting.unregisterContentScripts({ ids: [SCANNER_SCRIPT_ID] });
    }
  } else if (isRegistered) {
    await scripting.updateContentScripts([script]);
  } else {
    await scripting.registerContentScripts([script]);
  }
  return enabledOrigins;
}

export async function injectIntoOpenTabs(origins, { tabs = chrome.tabs, scripting = chrome.scripting } = {}) {
  if (origins.length === 0) {
    return;
  }
  const openTabs = await tabs.query({ url: origins.map(originPattern) });
  await Promise.allSettled(
    openTabs.map((tab) => scripting.executeScript({ target: { tabId: tab.id }, files: [SCANNER_SCRIPT_FILE] })),
  );
}
