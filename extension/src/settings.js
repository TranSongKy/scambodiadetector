import { DEFAULT_API_BASE_URL, STORAGE_KEYS } from "./constants.js";

export async function loadApiBaseUrl() {
  const stored = await chrome.storage.sync.get(STORAGE_KEYS.apiBaseUrl);
  return stored[STORAGE_KEYS.apiBaseUrl] ?? DEFAULT_API_BASE_URL;
}

export async function saveApiBaseUrl(apiBaseUrl) {
  await chrome.storage.sync.set({ [STORAGE_KEYS.apiBaseUrl]: apiBaseUrl });
}

export async function takePendingText() {
  const stored = await chrome.storage.session.get(STORAGE_KEYS.pendingText);
  await chrome.storage.session.remove(STORAGE_KEYS.pendingText);
  return stored[STORAGE_KEYS.pendingText] ?? "";
}

export async function storePendingText(text) {
  await chrome.storage.session.set({ [STORAGE_KEYS.pendingText]: text });
}
