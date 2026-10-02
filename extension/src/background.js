import { CONTEXT_MENU_ID } from "./constants.js";
import { UI_TEXT } from "./messages.js";
import { storePendingText } from "./settings.js";

chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({
    id: CONTEXT_MENU_ID,
    title: UI_TEXT.contextMenuTitle,
    contexts: ["selection"],
  });
});

chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true });

chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId !== CONTEXT_MENU_ID || !info.selectionText) {
    return;
  }
  chrome.sidePanel.open({ windowId: tab.windowId });
  storePendingText(info.selectionText);
});
