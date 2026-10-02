export const DEFAULT_API_BASE_URL = "http://localhost:8080";
export const CLASSIFICATIONS_PATH = "/api/v1/classifications";
export const REPORTS_PATH = "/api/v1/reports";
export const REPORT_CHANNEL = "extension";
export const MAX_MESSAGE_LENGTH = 2000;
export const REQUEST_TIMEOUT_MS = 15000;
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
});
export const LABELS = Object.freeze({
  scam: "scam",
  spam: "spam",
  normal: "normal",
});
