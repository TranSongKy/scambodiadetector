import { CONTEXT_MENU_ID, SCAN_MESSAGE_TYPES, STORAGE_KEYS } from "./constants.js";
import { UI_TEXT } from "./messages.js";
import { createScanService } from "./scan-service.js";
import { injectIntoOpenTabs, syncPageScanner } from "./scanner-registration.js";
import { storePendingText } from "./settings.js";
import { loadScanOrigins } from "./site-settings.js";

const scanService = createScanService();
const SCAN_HANDLERS = Object.freeze({
  [SCAN_MESSAGE_TYPES.inspectLinks]: (message) => scanService.checkLinks(message.urls ?? []),
  [SCAN_MESSAGE_TYPES.classifyMessage]: (message) => scanService.checkMessage(message.text),
});

async function refreshPageScanner({ injectOpenTabs }) {
  const enabledOrigins = await syncPageScanner(await loadScanOrigins());
  if (injectOpenTabs) {
    await injectIntoOpenTabs(enabledOrigins);
  }
}

async function handleScanMessage(message, sender) {
  const enabledOrigins = await loadScanOrigins();
  if (!enabledOrigins.includes(sender.origin)) {
    return { ok: false, disabled: true };
  }
  try {
    return { ok: true, result: await SCAN_HANDLERS[message.type](message) };
  } catch (error) {
    return { ok: false, error: error.message };
  }
}

chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({
    id: CONTEXT_MENU_ID,
    title: UI_TEXT.contextMenuTitle,
    contexts: ["selection"],
  });
  refreshPageScanner({ injectOpenTabs: true });
});

chrome.runtime.onStartup.addListener(() => refreshPageScanner({ injectOpenTabs: false }));

chrome.storage.onChanged.addListener((changes, areaName) => {
  if (areaName === "sync" && STORAGE_KEYS.scanOrigins in changes) {
    refreshPageScanner({ injectOpenTabs: true });
  }
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (!sender.tab || !(message?.type in SCAN_HANDLERS)) {
    return false;
  }
  handleScanMessage(message, sender).then(sendResponse);
  return true;
});

chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true });

chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId !== CONTEXT_MENU_ID || !info.selectionText) {
    return;
  }
  chrome.sidePanel.open({ windowId: tab.windowId });
  storePendingText(info.selectionText);
});
