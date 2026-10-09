import { LOOPBACK_HOSTS, STORAGE_KEYS } from "./constants.js";

export function normalizeSiteOrigin(value) {
  try {
    const url = new URL((value ?? "").trim());
    const isSecure = url.protocol === "https:";
    const isLoopbackHttp = url.protocol === "http:" && LOOPBACK_HOSTS.includes(url.hostname);
    return isSecure || isLoopbackHttp ? url.origin : null;
  } catch {
    return null;
  }
}

export function originPattern(origin) {
  return `${origin}/*`;
}

async function loadOriginList(key, storage) {
  const stored = await storage.get(key);
  return stored[key] ?? [];
}

async function saveOriginList(key, origins, storage) {
  const uniqueOrigins = [...new Set(origins)].sort();
  await storage.set({ [key]: uniqueOrigins });
  return uniqueOrigins;
}

export function loadScanOrigins(storage = chrome.storage.sync) {
  return loadOriginList(STORAGE_KEYS.scanOrigins, storage);
}

export function saveScanOrigins(origins, storage = chrome.storage.sync) {
  return saveOriginList(STORAGE_KEYS.scanOrigins, origins, storage);
}

export function loadCustomSites(storage = chrome.storage.sync) {
  return loadOriginList(STORAGE_KEYS.customScanSites, storage);
}

export function saveCustomSites(origins, storage = chrome.storage.sync) {
  return saveOriginList(STORAGE_KEYS.customScanSites, origins, storage);
}
