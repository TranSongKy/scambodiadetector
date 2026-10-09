import { normalizeApiBaseUrl } from "./classification.js";
import { CHAT_SITE_PRESETS } from "./constants.js";
import { ERROR_MESSAGES, PAGE_SCAN_TEXT, UI_TEXT } from "./messages.js";
import { loadApiBaseUrl, saveApiBaseUrl } from "./settings.js";
import { loadScanOrigins, normalizeSiteOrigin, originPattern, saveScanOrigins } from "./site-settings.js";

const input = document.getElementById("api-base-url");
const status = document.getElementById("status");
const sitesContainer = document.getElementById("scan-sites");
const customSiteInput = document.getElementById("custom-site");
const scanStatus = document.getElementById("scan-status");
const addSiteButton = document.getElementById("add-site");
let enabledOrigins = new Set(await loadScanOrigins());

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

async function enableSite(origin) {
  const granted = await chrome.permissions.request({ origins: [originPattern(origin)] });
  if (!granted) {
    scanStatus.textContent = UI_TEXT.permissionDenied;
    return;
  }
  enabledOrigins.add(origin);
  enabledOrigins = new Set(await saveScanOrigins([...enabledOrigins]));
  scanStatus.textContent = PAGE_SCAN_TEXT.siteEnabled;
}

async function disableSite(origin) {
  enabledOrigins.delete(origin);
  enabledOrigins = new Set(await saveScanOrigins([...enabledOrigins]));
  const apiOrigin = new URL(await loadApiBaseUrl()).origin;
  if (origin !== apiOrigin) {
    await chrome.permissions.remove({ origins: [originPattern(origin)] });
  }
  scanStatus.textContent = PAGE_SCAN_TEXT.siteDisabled;
}

function siteToggle(name, origin) {
  const label = document.createElement("label");
  label.className = "site";
  const checkbox = document.createElement("input");
  checkbox.type = "checkbox";
  checkbox.checked = enabledOrigins.has(origin);
  checkbox.dataset.origin = origin;
  checkbox.addEventListener("change", async () => {
    await (checkbox.checked ? enableSite(origin) : disableSite(origin));
    renderSites();
  });
  label.append(checkbox, ` ${name} (${origin})`);
  return label;
}

function renderSites() {
  const presetOrigins = new Set(CHAT_SITE_PRESETS.map((preset) => preset.origin));
  const customOrigins = [...enabledOrigins].filter((origin) => !presetOrigins.has(origin));
  sitesContainer.replaceChildren(
    ...CHAT_SITE_PRESETS.map((preset) => siteToggle(preset.name, preset.origin)),
    ...customOrigins.map((origin) => siteToggle(new URL(origin).host, origin)),
  );
}

async function addCustomSite() {
  const origin = normalizeSiteOrigin(customSiteInput.value);
  if (!origin) {
    scanStatus.textContent = PAGE_SCAN_TEXT.invalidSite;
    return;
  }
  await enableSite(origin);
  customSiteInput.value = "";
  renderSites();
}

document.getElementById("scan-heading").textContent = PAGE_SCAN_TEXT.optionsHeading;
document.getElementById("scan-hint").textContent = PAGE_SCAN_TEXT.optionsHint;
document.getElementById("custom-site-label").textContent = PAGE_SCAN_TEXT.customSiteLabel;
addSiteButton.textContent = PAGE_SCAN_TEXT.addSite;
addSiteButton.addEventListener("click", addCustomSite);
input.value = await loadApiBaseUrl();
document.getElementById("save").addEventListener("click", save);
renderSites();
