import { normalizeApiBaseUrl } from "./classification.js";
import { ERROR_MESSAGES, UI_TEXT } from "./messages.js";
import { loadApiBaseUrl, saveApiBaseUrl } from "./settings.js";

const input = document.getElementById("api-base-url");
const status = document.getElementById("status");

async function save() {
  const apiBaseUrl = normalizeApiBaseUrl(input.value);
  if (!apiBaseUrl) {
    status.textContent = ERROR_MESSAGES.invalidApiUrl;
    return;
  }
  const granted = await chrome.permissions.request({ origins: [`${new URL(apiBaseUrl).origin}/*`] });
  if (!granted) {
    status.textContent = UI_TEXT.permissionDenied;
    return;
  }
  await saveApiBaseUrl(apiBaseUrl);
  input.value = apiBaseUrl;
  status.textContent = UI_TEXT.saved;
}

input.value = await loadApiBaseUrl();
document.getElementById("save").addEventListener("click", save);
