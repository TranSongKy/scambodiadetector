import { classify, inspectLinks, isRateLimited } from "./classification.js";
import {
  LINK_VERDICTS,
  MAX_LINK_LENGTH,
  MAX_LINKS_PER_REQUEST,
  MAX_MESSAGE_LENGTH,
  RATE_LIMIT_PAUSE_MS,
  SCAN_CACHE_SIZE,
  SCAN_MIN_INTERVAL_MS,
} from "./constants.js";
import { maskPersonalData } from "./masking.js";
import { PAGE_SCAN_TEXT, REASON_DESCRIPTIONS } from "./messages.js";
import { loadApiBaseUrl } from "./settings.js";

const WEB_PROTOCOLS = Object.freeze(["http:", "https:"]);
const LINK_TITLES = Object.freeze({
  [LINK_VERDICTS.dangerous]: PAGE_SCAN_TEXT.dangerousLinkTitle,
  [LINK_VERDICTS.suspicious]: PAGE_SCAN_TEXT.suspiciousLinkTitle,
});

class BoundedCache {
  constructor(capacity) {
    this.capacity = capacity;
    this.entries = new Map();
  }

  get(key) {
    return this.entries.get(key);
  }

  set(key, value) {
    this.entries.delete(key);
    this.entries.set(key, value);
    if (this.entries.size > this.capacity) {
      this.entries.delete(this.entries.keys().next().value);
    }
  }
}

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

function isInspectableUrl(url) {
  if (typeof url !== "string" || url.length === 0 || url.length > MAX_LINK_LENGTH) {
    return false;
  }
  try {
    return WEB_PROTOCOLS.includes(new URL(url).protocol);
  } catch {
    return false;
  }
}

function chunk(items, size) {
  return Array.from({ length: Math.ceil(items.length / size) }, (_, index) => items.slice(index * size, (index + 1) * size));
}

export function toLinkWarning(inspection) {
  const reasons = inspection.reasons.map((code) => REASON_DESCRIPTIONS[code] ?? code);
  return {
    url: inspection.url,
    verdict: inspection.verdict,
    title: inspection.verdict in LINK_TITLES ? [LINK_TITLES[inspection.verdict], ...reasons].join("\n") : null,
    confirmText: inspection.verdict === LINK_VERDICTS.dangerous ? PAGE_SCAN_TEXT.dangerousLinkConfirm : null,
  };
}

export function toMessageWarning(result) {
  return {
    isScam: result.isScam,
    title: PAGE_SCAN_TEXT.messageWarningTitle,
    confidence: result.confidence,
    reasons: result.reasons,
    advice: result.advice,
    showLabel: PAGE_SCAN_TEXT.showMessage,
    dismissLabel: PAGE_SCAN_TEXT.dismiss,
  };
}

export function createScanService({
  classifyText = classify,
  inspect = inspectLinks,
  loadBaseUrl = loadApiBaseUrl,
  now = Date.now,
  wait = delay,
} = {}) {
  const messageCache = new BoundedCache(SCAN_CACHE_SIZE);
  const linkCache = new BoundedCache(SCAN_CACHE_SIZE);
  let queue = Promise.resolve();
  let nextAllowedAt = 0;

  function throttled(task) {
    const run = queue.then(async () => {
      const waitMs = nextAllowedAt - now();
      if (waitMs > 0) {
        await wait(waitMs);
      }
      try {
        return await task();
      } catch (error) {
        if (isRateLimited(error)) {
          nextAllowedAt = now() + RATE_LIMIT_PAUSE_MS;
        }
        throw error;
      } finally {
        nextAllowedAt = Math.max(nextAllowedAt, now() + SCAN_MIN_INTERVAL_MS);
      }
    });
    queue = run.catch(() => undefined);
    return run;
  }

  async function checkMessage(text) {
    const maskedText = maskPersonalData((text ?? "").trim());
    if (maskedText.length === 0 || maskedText.length > MAX_MESSAGE_LENGTH) {
      return null;
    }
    const cached = messageCache.get(maskedText);
    if (cached) {
      return cached;
    }
    const result = await throttled(async () => classifyText(await loadBaseUrl(), maskedText));
    const warning = toMessageWarning(result);
    messageCache.set(maskedText, warning);
    return warning;
  }

  async function checkLinks(urls) {
    const uniqueUrls = [...new Set(urls)].filter(isInspectableUrl);
    const uncached = uniqueUrls.filter((url) => linkCache.get(url) === undefined);
    for (const batch of chunk(uncached, MAX_LINKS_PER_REQUEST)) {
      const inspections = await throttled(async () => inspect(await loadBaseUrl(), batch));
      inspections.forEach((inspection) => linkCache.set(inspection.url, toLinkWarning(inspection)));
    }
    return uniqueUrls.map((url) => linkCache.get(url)).filter(Boolean);
  }

  return { checkMessage, checkLinks };
}
