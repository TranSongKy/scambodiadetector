import { ClassificationError, classify, report, validateText } from "./classification.js";
import { MAX_MESSAGE_LENGTH, STORAGE_KEYS } from "./constants.js";
import { findImageFile, validateImageFile } from "./image-input.js";
import { ERROR_MESSAGES, REPORT_LABEL_TEXT, UI_TEXT } from "./messages.js";
import { recognizeText, releaseOcr } from "./ocr.js";
import { loadApiBaseUrl, takePendingText } from "./settings.js";

const elements = Object.fromEntries(
  [
    "message", "check", "result", "verdict", "confidence", "reasons", "advice", "error", "open-options",
    "report-prompt", "report-buttons", "report-status", "drop-zone", "file-input", "image-preview",
    "preview-image", "remove-image", "ocr-status", "ocr-progress",
  ].map((id) => [id.replace(/-(\w)/g, (_, letter) => letter.toUpperCase()), document.getElementById(id)]),
);

let lastCheckedText = "";
let previewUrl = null;

function showError(message) {
  elements.result.hidden = true;
  elements.error.textContent = message;
  elements.error.hidden = false;
}

function setOcrStatus(message, progress) {
  elements.ocrStatus.textContent = message ?? "";
  elements.ocrStatus.hidden = !message;
  elements.ocrProgress.hidden = progress === undefined;
  elements.ocrProgress.value = progress ?? 0;
}

function showResult(formatted) {
  elements.error.hidden = true;
  elements.verdict.textContent = formatted.verdict;
  elements.confidence.textContent = `${UI_TEXT.confidenceLabel}: ${formatted.confidence}`;
  elements.reasons.replaceChildren(
    ...formatted.reasons.map((reason) => Object.assign(document.createElement("li"), { textContent: reason })),
  );
  elements.advice.textContent = formatted.advice ?? "";
  elements.advice.hidden = formatted.advice === null;
  elements.result.classList.toggle("scam", formatted.isScam);
  elements.reportStatus.textContent = "";
  setReportButtonsDisabled(false);
  elements.result.hidden = false;
}

function setReportButtonsDisabled(disabled) {
  for (const button of elements.reportButtons.querySelectorAll("button")) {
    button.disabled = disabled;
  }
}

async function sendReport(label) {
  setReportButtonsDisabled(true);
  try {
    await report(await loadApiBaseUrl(), lastCheckedText, label);
    elements.reportStatus.textContent = UI_TEXT.reportThanks;
  } catch (error) {
    elements.reportStatus.textContent = error instanceof ClassificationError ? error.message : ERROR_MESSAGES.unexpected;
    setReportButtonsDisabled(false);
  }
}

function renderReportButtons() {
  elements.reportPrompt.textContent = UI_TEXT.reportPrompt;
  elements.reportButtons.replaceChildren(
    ...Object.entries(REPORT_LABEL_TEXT).map(([label, text]) => {
      const button = Object.assign(document.createElement("button"), { type: "button", textContent: text });
      button.addEventListener("click", () => sendReport(label));
      return button;
    }),
  );
}

async function checkMessage() {
  const text = elements.message.value;
  const validationError = validateText(text);
  if (validationError) {
    showError(validationError);
    return;
  }
  elements.check.disabled = true;
  try {
    showResult(await classify(await loadApiBaseUrl(), text));
    lastCheckedText = text;
  } catch (error) {
    showError(error instanceof ClassificationError ? error.message : ERROR_MESSAGES.unexpected);
  } finally {
    elements.check.disabled = false;
  }
}

function showPreview(file) {
  if (previewUrl) {
    URL.revokeObjectURL(previewUrl);
  }
  previewUrl = file ? URL.createObjectURL(file) : null;
  if (previewUrl) {
    elements.previewImage.src = previewUrl;
  } else {
    elements.previewImage.removeAttribute("src");
  }
  elements.imagePreview.hidden = !file;
}

async function readImage(file) {
  const imageError = validateImageFile(file);
  if (imageError) {
    showError(imageError);
    return;
  }
  showPreview(file);
  elements.error.hidden = true;
  elements.result.hidden = true;
  elements.check.disabled = true;
  setOcrStatus(UI_TEXT.ocrRunning, 0);
  try {
    const text = await recognizeText(file, (progress) => {
      elements.ocrProgress.value = progress;
    });
    if (!text) {
      setOcrStatus(null);
      showError(ERROR_MESSAGES.ocrEmpty);
      return;
    }
    elements.message.value = text.slice(0, MAX_MESSAGE_LENGTH);
    setOcrStatus(UI_TEXT.ocrDone);
  } catch {
    setOcrStatus(null);
    showError(ERROR_MESSAGES.ocrFailed);
  } finally {
    elements.check.disabled = false;
  }
}

function handlePaste(event) {
  const file = findImageFile(event.clipboardData);
  if (!file) {
    return;
  }
  event.preventDefault();
  readImage(file);
}

function handleDrop(event) {
  event.preventDefault();
  elements.dropZone.classList.remove("dragging");
  const file = findImageFile(event.dataTransfer);
  if (file) {
    readImage(file);
  } else if (event.dataTransfer?.files?.length) {
    showError(ERROR_MESSAGES.notAnImage);
  }
}

async function checkPendingText() {
  const pendingText = await takePendingText();
  if (pendingText) {
    showPreview(null);
    setOcrStatus(null);
    elements.message.value = pendingText.slice(0, MAX_MESSAGE_LENGTH);
    await checkMessage();
  }
}

elements.check.addEventListener("click", checkMessage);
document.addEventListener("paste", handlePaste);
elements.dropZone.addEventListener("click", () => elements.fileInput.click());
elements.dropZone.addEventListener("keydown", (event) => {
  if (event.key === "Enter" || event.key === " ") {
    event.preventDefault();
    elements.fileInput.click();
  }
});
elements.dropZone.addEventListener("dragover", (event) => {
  event.preventDefault();
  elements.dropZone.classList.add("dragging");
});
elements.dropZone.addEventListener("dragleave", () => elements.dropZone.classList.remove("dragging"));
elements.dropZone.addEventListener("drop", handleDrop);
elements.fileInput.addEventListener("change", () => {
  const [file] = elements.fileInput.files;
  elements.fileInput.value = "";
  if (file) {
    readImage(file);
  }
});
elements.removeImage.addEventListener("click", () => {
  showPreview(null);
  setOcrStatus(null);
});
elements.openOptions.addEventListener("click", (event) => {
  event.preventDefault();
  chrome.runtime.openOptionsPage();
});
chrome.storage.session.onChanged.addListener((changes) => {
  if (changes[STORAGE_KEYS.pendingText]?.newValue) {
    checkPendingText();
  }
});
window.addEventListener("pagehide", releaseOcr);

elements.message.maxLength = MAX_MESSAGE_LENGTH;
renderReportButtons();
await checkPendingText();
