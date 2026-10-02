import { normalizeApiBaseUrl } from "./classification.js";
import { ERROR_MESSAGES } from "./messages.js";
import { loadApiBaseUrl, saveApiBaseUrl } from "./settings.js";

const input = document.getElementById("api-base-url");
const status = document.getElementById("status");
const SAVED_MESSAGE = "Đã lưu.";
const PERMISSION_DENIED_MESSAGE = "Cần cấp quyền truy cập địa chỉ này để kiểm tra tin nhắn.";

async function save() {
  const apiBaseUrl = normalizeApiBaseUrl(input.value);
  if (!apiBaseUrl) {
    status.textContent = ERROR_MESSAGES.invalidApiUrl;
    return;
  }
  const granted = await chrome.permissions.request({ origins: [`${new URL(apiBaseUrl).origin}/*`] });
  if (!granted) {
    status.textContent = PERMISSION_DENIED_MESSAGE;
    return;
  }
  await saveApiBaseUrl(apiBaseUrl);
  input.value = apiBaseUrl;
  status.textContent = SAVED_MESSAGE;
}

input.value = await loadApiBaseUrl();
document.getElementById("save").addEventListener("click", save);
