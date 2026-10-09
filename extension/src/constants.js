export const DEFAULT_API_BASE_URL = "http://localhost:8080";
export const CLASSIFICATIONS_PATH = "/api/v1/classifications";
export const REPORTS_PATH = "/api/v1/reports";
export const LINK_INSPECTIONS_PATH = "/api/v1/link-inspections";
export const REPORT_CHANNEL = "extension";
export const MAX_MESSAGE_LENGTH = 2000;
export const REQUEST_TIMEOUT_MS = 45000;
export const LOOPBACK_HOSTS = Object.freeze(["localhost", "127.0.0.1", "[::1]"]);
export const CONTEXT_MENU_ID = "scambodia-check-selection";
export const MAX_IMAGE_BYTES = 10 * 1024 * 1024;
export const IMAGE_MIME_PREFIX = "image/";
export const OCR_LANGUAGE = "vie";
export const OCR_ENGINE_LSTM_ONLY = 1;
export const OCR_MIN_WIDTH = 1200;
export const OCR_MAX_UPSCALE = 3;
export const DARK_IMAGE_LUMINANCE = 0.45;
export const LUMINANCE_SAMPLE_STEP = 16;
export const TESSERACT_PATHS = Object.freeze({
  worker: "vendor/tesseract/worker.min.js",
  core: "vendor/tesseract/core",
  lang: "vendor/tesseract/lang",
});
export const STORAGE_KEYS = Object.freeze({
  apiBaseUrl: "apiBaseUrl",
  pendingText: "pendingText",
  scanOrigins: "scanOrigins",
});
export const MAX_LINKS_PER_REQUEST = 50;
export const MAX_LINK_LENGTH = 2048;
export const SCANNER_SCRIPT_ID = "scambodia-page-scanner";
export const SCANNER_SCRIPT_FILE = "src/content/page-scanner.js";
export const SCAN_MESSAGE_TYPES = Object.freeze({
  inspectLinks: "scambodia:inspect-links",
  classifyMessage: "scambodia:classify-message",
});
export const LINK_VERDICTS = Object.freeze({
  safe: "safe",
  suspicious: "suspicious",
  dangerous: "dangerous",
});
export const SCAN_MIN_INTERVAL_MS = 3000;
export const RATE_LIMIT_PAUSE_MS = 60000;
export const SCAN_CACHE_SIZE = 500;
export const CHAT_SITE_PRESETS = Object.freeze([
  Object.freeze({ name: "Messenger", origin: "https://www.messenger.com" }),
  Object.freeze({ name: "Facebook", origin: "https://www.facebook.com" }),
  Object.freeze({ name: "Zalo Web", origin: "https://chat.zalo.me" }),
  Object.freeze({ name: "Telegram Web", origin: "https://web.telegram.org" }),
  Object.freeze({ name: "Gmail", origin: "https://mail.google.com" }),
]);
export const LABELS = Object.freeze({
  scam: "scam",
  spam: "spam",
  normal: "normal",
});
