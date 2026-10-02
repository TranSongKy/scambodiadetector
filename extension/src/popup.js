import { ClassificationError, classify, report, validateText } from "./classification.js";
import { MAX_MESSAGE_LENGTH } from "./constants.js";
import { ERROR_MESSAGES, REPORT_LABEL_TEXT, UI_TEXT } from "./messages.js";
import { loadApiBaseUrl, takePendingText } from "./settings.js";

const elements = {
  message: document.getElementById("message"),
  check: document.getElementById("check"),
  result: document.getElementById("result"),
  verdict: document.getElementById("verdict"),
  confidence: document.getElementById("confidence"),
  reasons: document.getElementById("reasons"),
  advice: document.getElementById("advice"),
  error: document.getElementById("error"),
  openOptions: document.getElementById("open-options"),
  reportPrompt: document.getElementById("report-prompt"),
  reportButtons: document.getElementById("report-buttons"),
  reportStatus: document.getElementById("report-status"),
};

let lastCheckedText = "";

function showError(message) {
  elements.result.hidden = true;
  elements.error.textContent = message;
  elements.error.hidden = false;
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

elements.check.addEventListener("click", checkMessage);
elements.openOptions.addEventListener("click", (event) => {
  event.preventDefault();
  chrome.runtime.openOptionsPage();
});

await chrome.action.setBadgeText({ text: "" });
elements.message.maxLength = MAX_MESSAGE_LENGTH;
renderReportButtons();
const pendingText = await takePendingText();
if (pendingText) {
  elements.message.value = pendingText;
  await checkMessage();
}
