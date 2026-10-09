(() => {
  const SCANNER_MARKER = "__scambodiaPageScanner";
  if (globalThis[SCANNER_MARKER]) {
    return;
  }
  globalThis[SCANNER_MARKER] = true;

  const MESSAGE_TYPES = Object.freeze({
    inspectLinks: "scambodia:inspect-links",
    classifyMessage: "scambodia:classify-message",
  });
  const DANGEROUS_VERDICT = "dangerous";
  const SUSPICIOUS_VERDICT = "suspicious";
  const VERDICT_ATTRIBUTE = "data-scambodia-verdict";
  const OWN_UI_ATTRIBUTE = "data-scambodia";
  const OWN_UI_SELECTOR = `[${OWN_UI_ATTRIBUTE}]`;
  const IGNORED_SELECTOR = `input, textarea, select, script, style, noscript, [contenteditable=""], [contenteditable="true"], ${OWN_UI_SELECTOR}`;
  const MIN_MESSAGE_LENGTH = 20;
  const MAX_MESSAGE_LENGTH = 2000;
  const MAX_MESSAGES_PER_BATCH = 10;
  const MAX_TRACKED_TEXTS = 2000;
  const SCAN_DELAY_MS = 800;
  const DOMINANT_CHILD_RATIO = 0.8;
  const MAX_VISIBLE_WARNINGS = 3;
  const HIGHLIGHT_MS = 2500;
  const OUTLINES = Object.freeze({
    [DANGEROUS_VERDICT]: "2px solid #d93025",
    [SUSPICIOUS_VERDICT]: "2px dashed #e37400",
  });
  const MESSAGE_OUTLINE = "2px solid #d93025";
  const WARNING_STYLES = `
    :host { all: initial; }
    .stack { position: fixed; right: 16px; bottom: 16px; z-index: 2147483647; display: flex; flex-direction: column; gap: 8px; max-width: 360px; font: 14px/1.4 system-ui, sans-serif; }
    .card { background: #fff; color: #202124; border-left: 4px solid #d93025; border-radius: 8px; box-shadow: 0 4px 16px rgba(0,0,0,.25); padding: 12px 36px 12px 12px; position: relative; }
    .title { font-weight: 600; color: #d93025; margin: 0 0 4px; }
    .quote { color: #5f6368; font-style: italic; margin: 0 0 6px; overflow-wrap: anywhere; }
    ul { margin: 0 0 6px; padding-left: 18px; }
    .advice { margin: 0 0 8px; }
    button { font: inherit; cursor: pointer; }
    .show { background: #d93025; color: #fff; border: 0; border-radius: 4px; padding: 4px 10px; }
    .close { position: absolute; top: 6px; right: 6px; background: none; border: 0; font-size: 18px; color: #5f6368; }
    @media (prefers-color-scheme: dark) { .card { background: #292a2d; color: #e8eaed; } .quote, .close { color: #bdc1c6; } }
  `;
  const QUOTE_LENGTH = 90;

  const seenTexts = new Set();
  const inspectedAnchors = new WeakSet();
  const pendingRoots = new Set();
  let scanTimer = null;
  let stopped = false;
  let warningStack = null;

  function normalizeText(text) {
    return (text ?? "").replace(/\s+/g, " ").trim();
  }

  function rememberText(text) {
    if (seenTexts.size >= MAX_TRACKED_TEXTS) {
      seenTexts.clear();
    }
    seenTexts.add(text);
  }

  function isIgnored(element) {
    return element.closest(IGNORED_SELECTOR) !== null || element.getClientRects().length === 0;
  }

  function messageBlocks(root) {
    const elements = [root, ...root.querySelectorAll("*")].reverse();
    const hasSelectedDescendant = new WeakSet();
    const blocks = [];
    for (const element of elements) {
      if (hasSelectedDescendant.has(element)) {
        continue;
      }
      const text = normalizeText(element.textContent);
      if (text.length < MIN_MESSAGE_LENGTH || text.length > MAX_MESSAGE_LENGTH) {
        continue;
      }
      const dominatedByChild = [...element.children].some(
        (child) => normalizeText(child.textContent).length >= text.length * DOMINANT_CHILD_RATIO,
      );
      if (dominatedByChild || isIgnored(element)) {
        continue;
      }
      blocks.push({ element, text });
      for (let ancestor = element.parentElement; ancestor; ancestor = ancestor.parentElement) {
        hasSelectedDescendant.add(ancestor);
      }
    }
    return blocks.reverse();
  }

  function externalAnchors(root) {
    const anchors = root.matches?.("a[href]") ? [root] : [];
    anchors.push(...root.querySelectorAll("a[href]"));
    return anchors.filter((anchor) => {
      if (inspectedAnchors.has(anchor) || anchor.closest(OWN_UI_SELECTOR)) {
        return false;
      }
      try {
        const url = new URL(anchor.href);
        return (url.protocol === "http:" || url.protocol === "https:") && url.host !== location.host;
      } catch {
        return false;
      }
    });
  }

  async function sendToExtension(message) {
    try {
      const response = await chrome.runtime.sendMessage(message);
      if (response?.disabled) {
        stop();
      }
      return response?.ok ? response.result : null;
    } catch {
      stop();
      return null;
    }
  }

  function markLink(anchor, warning) {
    if (!(warning.verdict in OUTLINES)) {
      return;
    }
    anchor.setAttribute(VERDICT_ATTRIBUTE, warning.verdict);
    anchor.style.outline = OUTLINES[warning.verdict];
    anchor.style.outlineOffset = "1px";
    anchor.title = warning.title;
    if (warning.confirmText) {
      anchor.dataset.scambodiaConfirm = warning.confirmText;
    }
  }

  async function inspectLinks(anchors) {
    anchors.forEach((anchor) => inspectedAnchors.add(anchor));
    const urls = [...new Set(anchors.map((anchor) => anchor.href))];
    if (urls.length === 0) {
      return;
    }
    const warnings = await sendToExtension({ type: MESSAGE_TYPES.inspectLinks, urls });
    const warningsByUrl = new Map((warnings ?? []).map((warning) => [warning.url, warning]));
    anchors.forEach((anchor) => {
      const warning = warningsByUrl.get(anchor.href);
      if (warning) {
        markLink(anchor, warning);
      }
    });
  }

  function ensureWarningStack() {
    if (warningStack?.isConnected) {
      return warningStack.shadowRoot.querySelector(".stack");
    }
    warningStack = document.createElement("div");
    warningStack.setAttribute(OWN_UI_ATTRIBUTE, "warnings");
    const shadow = warningStack.attachShadow({ mode: "open" });
    const styleSheet = new CSSStyleSheet();
    styleSheet.replaceSync(WARNING_STYLES);
    shadow.adoptedStyleSheets = [styleSheet];
    const stack = document.createElement("div");
    stack.className = "stack";
    stack.setAttribute("role", "alert");
    shadow.append(stack);
    document.documentElement.append(warningStack);
    return stack;
  }

  function textElement(tag, className, text) {
    const element = document.createElement(tag);
    element.className = className;
    element.textContent = text;
    return element;
  }

  function quoteOf(text) {
    return text.length > QUOTE_LENGTH ? `“${text.slice(0, QUOTE_LENGTH)}…”` : `“${text}”`;
  }

  function highlight(element) {
    element.scrollIntoView({ behavior: "smooth", block: "center" });
    const previousOutline = element.style.outline;
    element.style.outline = MESSAGE_OUTLINE;
    setTimeout(() => {
      element.style.outline = previousOutline;
    }, HIGHLIGHT_MS);
  }

  function showMessageWarning(block, warning) {
    const stack = ensureWarningStack();
    const card = document.createElement("div");
    card.className = "card";
    card.append(
      textElement("p", "title", `${warning.title} (${warning.confidence})`),
      textElement("p", "quote", quoteOf(block.text)),
    );
    if (warning.reasons.length > 0) {
      const list = document.createElement("ul");
      warning.reasons.forEach((reason) => list.append(textElement("li", "reason", reason)));
      card.append(list);
    }
    if (warning.advice) {
      card.append(textElement("p", "advice", warning.advice));
    }
    const showButton = textElement("button", "show", warning.showLabel);
    showButton.type = "button";
    showButton.addEventListener("click", () => highlight(block.element));
    const closeButton = textElement("button", "close", "×");
    closeButton.type = "button";
    closeButton.setAttribute("aria-label", warning.dismissLabel);
    closeButton.addEventListener("click", () => card.remove());
    card.append(showButton, closeButton);
    stack.prepend(card);
    [...stack.children].slice(MAX_VISIBLE_WARNINGS).forEach((oldCard) => oldCard.remove());
  }

  async function classifyMessages(blocks) {
    for (const block of blocks) {
      if (stopped) {
        return;
      }
      const warning = await sendToExtension({ type: MESSAGE_TYPES.classifyMessage, text: block.text });
      if (warning?.isScam) {
        showMessageWarning(block, warning);
      }
    }
  }

  function newMessageBlocks(roots) {
    const blocks = roots.flatMap(messageBlocks).filter((block) => !seenTexts.has(block.text));
    blocks.forEach((block) => rememberText(block.text));
    return blocks.slice(-MAX_MESSAGES_PER_BATCH);
  }

  function scanPending() {
    scanTimer = null;
    const roots = [...pendingRoots].filter((root) => root.isConnected);
    pendingRoots.clear();
    if (stopped || roots.length === 0) {
      return;
    }
    inspectLinks(roots.flatMap(externalAnchors));
    classifyMessages(newMessageBlocks(roots));
  }

  function scheduleScan(root) {
    pendingRoots.add(root);
    if (scanTimer === null) {
      scanTimer = setTimeout(scanPending, SCAN_DELAY_MS);
    }
  }

  function confirmDangerousClick(event) {
    const anchor = event.target instanceof Element ? event.target.closest(`a[${VERDICT_ATTRIBUTE}="${DANGEROUS_VERDICT}"]`) : null;
    if (anchor?.dataset.scambodiaConfirm && !window.confirm(anchor.dataset.scambodiaConfirm)) {
      event.preventDefault();
      event.stopImmediatePropagation();
    }
  }

  const observer = new MutationObserver((mutations) => {
    for (const mutation of mutations) {
      mutation.addedNodes.forEach((node) => {
        if (node.nodeType === Node.ELEMENT_NODE && !node.hasAttribute(OWN_UI_ATTRIBUTE)) {
          scheduleScan(node);
        } else if (node.nodeType === Node.TEXT_NODE && node.parentElement) {
          scheduleScan(node.parentElement);
        }
      });
    }
  });

  function stop() {
    stopped = true;
    observer.disconnect();
    document.removeEventListener("click", confirmDangerousClick, true);
  }

  function start() {
    if (!document.body) {
      return;
    }
    messageBlocks(document.body).forEach((block) => rememberText(block.text));
    inspectLinks(externalAnchors(document.body));
    observer.observe(document.body, { childList: true, subtree: true });
    document.addEventListener("click", confirmDangerousClick, true);
  }

  start();
})();
