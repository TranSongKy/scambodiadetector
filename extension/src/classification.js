import { CLASSIFICATIONS_PATH, MAX_MESSAGE_LENGTH } from "./constants.js";
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
    if (url.protocol !== "http:" && url.protocol !== "https:") {
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

export async function classify(apiBaseUrl, text, fetchImplementation = fetch) {
  let response;
  try {
    response = await fetchImplementation(apiBaseUrl + CLASSIFICATIONS_PATH, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ text }),
    });
  } catch {
    throw new ClassificationError(ERROR_MESSAGES.network);
  }
  if (!response.ok) {
    throw new ClassificationError(await problemMessage(response));
  }
  return formatResult(await response.json());
}
