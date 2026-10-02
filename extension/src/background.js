import { CONTEXT_MENU_ID } from "./constants.js";
import { storePendingText } from "./settings.js";

const PENDING_BADGE_TEXT = "!";
const PENDING_BADGE_COLOR = "#d93025";

chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({
    id: CONTEXT_MENU_ID,
    title: "Kiểm tra tin nhắn này có lừa đảo không",
    contexts: ["selection"],
  });
});

chrome.contextMenus.onClicked.addListener(async (info) => {
  if (info.menuItemId !== CONTEXT_MENU_ID || !info.selectionText) {
    return;
  }
  await storePendingText(info.selectionText);
  try {
    await chrome.action.openPopup();
  } catch {
    await chrome.action.setBadgeBackgroundColor({ color: PENDING_BADGE_COLOR });
    await chrome.action.setBadgeText({ text: PENDING_BADGE_TEXT });
  }
});
