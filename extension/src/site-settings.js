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

export async function loadScanOrigins(storage = chrome.storage.sync) {
  const stored = await storage.get(STORAGE_KEYS.scanOrigins);
  return stored[STORAGE_KEYS.scanOrigins] ?? [];
}

export async function saveScanOrigins(origins, storage = chrome.storage.sync) {
  const uniqueOrigins = [...new Set(origins)].sort();
  await storage.set({ [STORAGE_KEYS.scanOrigins]: uniqueOrigins });
  return uniqueOrigins;
}
