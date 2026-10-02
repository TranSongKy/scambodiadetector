export const DEFAULT_API_BASE_URL = "http://localhost:8080";
export const CLASSIFICATIONS_PATH = "/api/v1/classifications";
export const REPORTS_PATH = "/api/v1/reports";
export const REPORT_CHANNEL = "extension";
export const MAX_MESSAGE_LENGTH = 2000;
export const REQUEST_TIMEOUT_MS = 15000;
export const LOOPBACK_HOSTS = Object.freeze(["localhost", "127.0.0.1", "[::1]"]);
export const CONTEXT_MENU_ID = "scambodia-check-selection";
export const STORAGE_KEYS = Object.freeze({
  apiBaseUrl: "apiBaseUrl",
  pendingText: "pendingText",
});
export const LABELS = Object.freeze({
  scam: "scam",
  spam: "spam",
  normal: "normal",
});
