import {
  CLASSIFICATIONS_PATH,
  LOOPBACK_HOSTS,
  MAX_MESSAGE_LENGTH,
  REPORT_CHANNEL,
  REPORTS_PATH,
  REQUEST_TIMEOUT_MS,
} from "./constants.js";
import { ERROR_MESSAGES, REASON_DESCRIPTIONS, SCAM_ADVICE, VERDICTS } from "./messages.js";

const HTTP_SERVICE_UNAVAILABLE = 503;
const PROBLEM_CODE_MESSAGES = Object.freeze({
  "classification.empty_text": ERROR_MESSAGES.emptyText,
  "classification.text_too_long": ERROR_MESSAGES.textTooLong,
});
const PERCENT = 100;
const SCAM_LABEL = "scam";

export class ClassificationError extends Error {}

export function validateText(text) {
  const trimmed = (text ?? "").trim();
  if (trimmed.length === 0) {
    return ERROR_MESSAGES.emptyText;
  }
  return trimmed.length > MAX_MESSAGE_LENGTH ? ERROR_MESSAGES.textTooLong : null;
}

export function normalizeApiBaseUrl(value) {
  try {
    const url = new URL((value ?? "").trim());
    const isSecure = url.protocol === "https:";
    const isLoopbackHttp = url.protocol === "http:" && LOOPBACK_HOSTS.includes(url.hostname);
    if (!isSecure && !isLoopbackHttp) {
      return null;
    }
    return url.origin + url.pathname.replace(/\/+$/, "");
  } catch {
    return null;
  }
}

export function describeReason(code) {
  return REASON_DESCRIPTIONS[code] ?? code;
}

export function formatResult(result) {
  return {
    verdict: VERDICTS[result.label] ?? result.label,
    confidence: `${Math.round(result.confidence * PERCENT)}%`,
    reasons: [...new Set(result.reasons)].map(describeReason),
    advice: result.label === SCAM_LABEL ? SCAM_ADVICE : null,
    isScam: result.label === SCAM_LABEL,
  };
}

export async function problemMessage(response) {
  if (response.status === HTTP_SERVICE_UNAVAILABLE) {
    return ERROR_MESSAGES.modelUnavailable;
  }
  try {
    const problem = await response.json();
    return PROBLEM_CODE_MESSAGES[problem.code] ?? ERROR_MESSAGES.unexpected;
  } catch {
    return ERROR_MESSAGES.unexpected;
  }
}

async function postJson(url, body, fetchImplementation, timeoutMs) {
  let response;
  try {
    response = await fetchImplementation(url, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body),
      signal: AbortSignal.timeout(timeoutMs),
    });
  } catch (error) {
    throw new ClassificationError(error?.name === "TimeoutError" ? ERROR_MESSAGES.timeout : ERROR_MESSAGES.network);
  }
  if (!response.ok) {
    throw new ClassificationError(await problemMessage(response));
  }
  return response.json();
}

export async function classify(apiBaseUrl, text, fetchImplementation = fetch, timeoutMs = REQUEST_TIMEOUT_MS) {
  return formatResult(await postJson(apiBaseUrl + CLASSIFICATIONS_PATH, { text }, fetchImplementation, timeoutMs));
}

export async function report(apiBaseUrl, text, label, fetchImplementation = fetch, timeoutMs = REQUEST_TIMEOUT_MS) {
  const body = { text, label, channel: REPORT_CHANNEL };
  return (await postJson(apiBaseUrl + REPORTS_PATH, body, fetchImplementation, timeoutMs)).id;
}
