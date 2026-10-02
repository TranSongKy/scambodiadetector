import { ClassificationError, classify, validateText } from "./classification.js";
import { ERROR_MESSAGES } from "./messages.js";
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
};

function showError(message) {
  elements.result.hidden = true;
  elements.error.textContent = message;
  elements.error.hidden = false;
}

function showResult(formatted) {
  elements.error.hidden = true;
  elements.verdict.textContent = formatted.verdict;
  elements.confidence.textContent = `Độ tin cậy: ${formatted.confidence}`;
  elements.reasons.replaceChildren(
    ...formatted.reasons.map((reason) => Object.assign(document.createElement("li"), { textContent: reason })),
  );
  elements.advice.textContent = formatted.advice ?? "";
  elements.advice.hidden = formatted.advice === null;
  elements.result.classList.toggle("scam", formatted.isScam);
  elements.result.hidden = false;
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
const pendingText = await takePendingText();
if (pendingText) {
  elements.message.value = pendingText;
  await checkMessage();
}
