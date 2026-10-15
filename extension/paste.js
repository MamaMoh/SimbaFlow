/**
 * Filling a form by pasting into it.
 *
 * SimbaFlow's row menus have Copy to insurance, which puts one candidate on the clipboard as a
 * finished fill — the same list of labels and text the toolbar button fetches, only chosen on
 * the board instead of in the popup. This listens for that paste and fills the form with it,
 * instead of letting a line of base64 land in whichever box had the cursor.
 *
 * It runs only on sites a person has switched on from the popup (see background.js), and it
 * reads nothing but the one paste it is handed. Anything that is not a SimbaFlow payload passes
 * straight through untouched: this is somebody's form, and an ordinary paste into it is theirs.
 *
 * The format is decoded here, by hand, rather than shared with the app — this file cannot import
 * from it. `frontend/lib/site-bridge/clipboard.ts` is the other half, and its test pins the
 * exact bytes so that a change there fails until this changes too.
 */
(() => {
  if (window.__simbaflowPaste) return;
  window.__simbaflowPaste = true;

  const PREFIX = "simbaflow-fill:1:";
  /** Any newer `simbaflow-fill:N:` is ours too, and gets "update the helper" rather than a paste. */
  const ANY_VERSION = /^simbaflow-fill:(\d+):/;

  const norm = (s) =>
    String(s || "")
      .replace(/[*:]/g, " ")
      .replace(/\s+/g, " ")
      .trim()
      .toLowerCase();

  function decode(text) {
    try {
      const binary = atob(text.slice(PREFIX.length));
      const bytes = Uint8Array.from(binary, (c) => c.charCodeAt(0));
      return JSON.parse(new TextDecoder().decode(bytes));
    } catch {
      return null;
    }
  }

  /**
   * Whether this page is the form the payload was made for, by the form's own words.
   *
   * The same test the popup uses to recognise a form — three of its labels on the page — so a
   * paste and a click agree about which pages are which. A payload pasted into a webmail compose
   * box, or into the visa form, fills nothing.
   */
  function isTheForm(payload) {
    const pageLabels = window.__simbaflow.collectLabels();
    let hits = 0;
    for (const label of payload.recognise || []) {
      const want = norm(label);
      if (want && pageLabels.some((l) => l === want || l.includes(want))) hits += 1;
    }
    return hits >= 3;
  }

  const onPaste = async (event) => {
    const text = (event.clipboardData?.getData("text/plain") || "").trim();
    if (window.__simbaflowPasteOff || !ANY_VERSION.test(text)) return;

    // Ours, whatever happens next. Letting it through would put a candidate's details, encoded
    // but not hidden, into a box on somebody else's site.
    event.preventDefault();
    event.stopImmediatePropagation();

    if (!text.startsWith(PREFIX)) {
      toast.show({
        tone: "attention",
        title: "This helper is too old for that copy",
        detail: "SimbaFlow has moved on. Update the browser helper from Settings → Browser helper, then paste again.",
      });
      return;
    }

    const payload = decode(text);
    if (!payload?.plan?.steps || !payload.candidate) {
      toast.show({
        tone: "attention",
        title: "That copy could not be read",
        detail: "Copy the candidate again from SimbaFlow and paste once more.",
      });
      return;
    }

    const who = payload.candidate.name || payload.candidate.passportNumber || "This candidate";

    if (typeof payload.expiresAt !== "number" || Date.now() > payload.expiresAt) {
      toast.show({
        tone: "attention",
        title: `${who} was copied too long ago`,
        detail: "An old copy can fill the form with the wrong person, so it is not used. Copy them again from SimbaFlow.",
      });
      return;
    }

    if (!isTheForm(payload)) {
      toast.show({
        tone: "attention",
        title: "Nothing was filled",
        detail: `${who} is copied for ${payload.site?.title || "another form"}, and this page does not look like that form.`,
      });
      return;
    }

    toast.show({ tone: "busy", title: `Filling ${who}…` });

    try {
      const results = await window.__simbaflow.fill(payload.plan.steps);
      toast.report(payload, who, results);
    } catch (err) {
      toast.show({
        tone: "attention",
        title: "The form could not be filled",
        detail: String(err?.message || err),
      });
    }
  };

  // Capture, at the window: the form's own paste handlers — an input mask, a framework's
  // listener — would otherwise see the payload first and some of them rewrite it.
  window.addEventListener("paste", onPaste, true);

  /* ─── the card in the corner ──────────────────────────────────────────────── */

  const WHY = {
    dropdown: "choose it yourself",
    nooption: "no matching option",
    rejected: "the form refused it",
    notfound: "not on this page",
    absent: "not held in SimbaFlow",
    check: "written into a date picker — check it reads right",
  };

  const ICONS = {
    good: '<path d="M20 6 9 17l-5-5"/>',
    attention: '<circle cx="12" cy="12" r="9"/><path d="M12 8v5M12 16h.01"/>',
    busy: '<path d="M21 12a9 9 0 1 1-6.2-8.6"/>',
    close: '<path d="M18 6 6 18M6 6l12 12"/>',
  };
  const svg = (paths, cls = "") =>
    `<svg class="${cls}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"
          stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths}</svg>`;

  /**
   * SimbaFlow's own surface, in somebody else's page.
   *
   * In a shadow root so neither side's styles reach the other: the portal's stylesheet does not
   * turn this into a Bootstrap alert, and nothing here can restyle the form. The tokens are the
   * popup's, which are the app's.
   */
  const CSS = `
    :host { all: initial; }
    .card {
      --background: oklch(1 0 0); --foreground: oklch(0.145 0 0);
      --muted: oklch(0.97 0 0); --muted-foreground: oklch(0.556 0 0);
      --border: oklch(0.922 0 0); --brand: #006633; --good: #0f7a43; --attention: #9a5b00;
      position: fixed; right: 16px; bottom: 16px; z-index: 2147483647;
      width: 340px; max-width: calc(100vw - 32px); max-height: calc(100vh - 32px);
      display: flex; flex-direction: column;
      box-sizing: border-box; overflow: hidden;
      background: var(--background); color: var(--foreground);
      border: 1px solid var(--border); border-radius: 10px;
      box-shadow: 0 10px 30px -10px rgb(0 0 0 / 0.25), 0 2px 6px rgb(0 0 0 / 0.06);
      font: 13px/1.45 "SimbaFlow Geist", ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif;
      -webkit-font-smoothing: antialiased;
      animation: rise 160ms ease-out;
    }
    @media (prefers-color-scheme: dark) {
      .card {
        --background: oklch(0.205 0 0); --foreground: oklch(0.985 0 0);
        --muted: oklch(0.269 0 0); --muted-foreground: oklch(0.708 0 0);
        --border: oklch(0.32 0 0); --brand: #27a567; --good: #4ade80; --attention: #fbbf24;
      }
    }
    @keyframes rise { from { opacity: 0; transform: translateY(6px); } }
    @keyframes spin { to { transform: rotate(360deg); } }
    @media (prefers-reduced-motion: reduce) { .card, .spin { animation: none !important; } }

    .head { display: flex; gap: 10px; align-items: flex-start; padding: 12px 12px 12px 14px; }
    .badge { flex: none; width: 22px; height: 22px; margin-top: 1px; display: grid; place-items: center;
             border-radius: 999px; background: var(--muted); }
    .badge svg { width: 13px; height: 13px; }
    .tone-good .badge { color: var(--good); }
    .tone-attention .badge { color: var(--attention); }
    .tone-busy .badge { color: var(--muted-foreground); }
    .spin { animation: spin 0.9s linear infinite; }
    .titles { flex: 1; min-width: 0; }
    .brand { font-size: 11px; font-weight: 600; letter-spacing: 0.01em; color: var(--brand); }
    .title { font-weight: 600; font-size: 13.5px; margin-top: 1px; overflow-wrap: anywhere; }
    .detail { color: var(--muted-foreground); margin-top: 2px; }
    .close { all: unset; flex: none; cursor: pointer; width: 26px; height: 26px; display: grid;
             place-items: center; border-radius: 6px; color: var(--muted-foreground); }
    .close:hover { background: var(--muted); color: var(--foreground); }
    .close:focus-visible { outline: 2px solid var(--brand); outline-offset: 1px; }
    .close svg { width: 14px; height: 14px; }

    .chips { display: flex; flex-wrap: wrap; gap: 6px; padding: 0 14px 12px 46px; }
    .chip { display: inline-flex; align-items: center; gap: 5px; padding: 3px 8px; border-radius: 999px;
            background: var(--muted); color: var(--muted-foreground); font-size: 11px; font-weight: 500; }
    .chip.good { background: color-mix(in srgb, var(--good) 13%, transparent); color: var(--good); }
    .chip.attention { background: color-mix(in srgb, var(--attention) 15%, transparent); color: var(--attention); }

    .body { overflow-y: auto; border-top: 1px solid var(--border); }
    .section { padding: 10px 14px 4px; }
    .section-head { font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em;
                    color: var(--muted-foreground); margin-bottom: 4px; }
    ul { list-style: none; margin: 0; padding: 0; }
    li { display: flex; justify-content: space-between; gap: 12px; padding: 5px 0;
         border-bottom: 1px dashed var(--border); }
    li:last-child { border-bottom: 0; }
    .what { font-weight: 500; }
    .why { color: var(--muted-foreground); text-align: right; }
    .foot { padding: 10px 14px; color: var(--muted-foreground); font-size: 12px; background: var(--muted); }
  `;

  /**
   * The bundled typeface, loaded into the page's font set under a name of our own so it cannot
   * collide with a Geist the portal might have. Best effort: a page whose policy blocks it gets
   * the system face, which is what the fallback chain is for.
   */
  let fontRequested = false;
  function loadFont() {
    if (fontRequested) return;
    fontRequested = true;
    try {
      const face = new FontFace(
        "SimbaFlow Geist",
        `url("${chrome.runtime.getURL("fonts/Geist-Variable.woff2")}") format("woff2-variations")`,
        { weight: "100 900" },
      );
      document.fonts.add(face);
      face.load().catch(() => {});
    } catch {
      /* the system face it is */
    }
  }

  const toast = (() => {
    let host = null;
    let root = null;
    let timer = null;

    const mount = () => {
      if (host?.isConnected) return;
      loadFont();
      host = document.createElement("simbaflow-paste");
      root = host.attachShadow({ mode: "closed" });
      document.documentElement.appendChild(host);
    };

    const close = () => {
      clearTimeout(timer);
      host?.remove();
      host = null;
    };

    document.addEventListener(
      "keydown",
      (e) => {
        if (e.key === "Escape" && host) close();
      },
      true,
    );

    /**
     * One card at a time, replaced rather than stacked: the second paste is the one that matters.
     * It goes away on its own only when there is nothing left to do — a list of boxes to finish
     * stays until it is closed, because it is the list the person is working through.
     */
    function render({ tone, title, detail = "", chips = [], sections = [], foot = "", sticky }) {
      mount();
      clearTimeout(timer);
      const icon = tone === "busy" ? svg(ICONS.busy, "spin") : svg(ICONS[tone] || ICONS.good);
      root.innerHTML = `<style>${CSS}</style>
        <div class="card tone-${tone}" role="status" aria-live="polite">
          <div class="head">
            <span class="badge">${icon}</span>
            <div class="titles">
              <div class="brand">SimbaFlow</div>
              <div class="title"></div>
              <div class="detail"></div>
            </div>
            <button class="close" type="button" aria-label="Close">${svg(ICONS.close)}</button>
          </div>
          <div class="chips"></div>
          <div class="body"></div>
          <div class="foot"></div>
        </div>`;

      const $ = (sel) => root.querySelector(sel);
      $(".title").textContent = title;
      if (detail) $(".detail").textContent = detail;
      else $(".detail").remove();
      $(".close").addEventListener("click", close);

      if (chips.length) {
        for (const chip of chips) {
          const span = document.createElement("span");
          span.className = `chip ${chip.tone || ""}`.trim();
          span.textContent = chip.text;
          $(".chips").appendChild(span);
        }
      } else {
        $(".chips").remove();
      }

      if (sections.some((s) => s.items.length)) {
        for (const { head, items } of sections) {
          if (!items.length) continue;
          const box = document.createElement("div");
          box.className = "section";
          box.innerHTML = `<div class="section-head"></div><ul></ul>`;
          box.querySelector(".section-head").textContent = head;
          for (const item of items) {
            const li = document.createElement("li");
            li.innerHTML = `<span class="what"></span><span class="why"></span>`;
            li.querySelector(".what").textContent = item.label;
            li.querySelector(".why").textContent = WHY[item.status] || item.status;
            box.querySelector("ul").appendChild(li);
          }
          $(".body").appendChild(box);
        }
      } else {
        $(".body").remove();
      }

      if (foot) $(".foot").textContent = foot;
      else $(".foot").remove();

      if (!sticky && tone !== "busy") timer = setTimeout(close, tone === "good" ? 6000 : 10000);
    }

    /** What happened, in the same words the popup uses for the same results. */
    function report(payload, who, results) {
      const filled = results.filter((r) => r.status === "filled").length;
      const kept = results.filter((r) => r.status === "kept").length;
      const check = results.filter((r) => r.status === "check");
      const left = [
        ...results.filter((r) => !["filled", "kept", "check"].includes(r.status)),
        ...(payload.plan.missing || []).map((m) => ({ label: m.label, status: "absent" })),
      ];
      const done = filled + check.length;
      const button = payload.plan.repeatButton;

      render({
        tone: left.length || check.length ? "attention" : "good",
        title: `${who} is in the form`,
        detail: payload.candidate.passportNumber || "",
        chips: [
          { text: `${done} filled`, tone: "good" },
          ...(kept ? [{ text: `${kept} left as they were` }] : []),
          ...(check.length ? [{ text: `${check.length} to check`, tone: "attention" }] : []),
          ...(left.length ? [{ text: `${left.length} need you`, tone: "attention" }] : []),
        ],
        sections: [
          { head: "Worth a look before you submit", items: check },
          { head: "For you to finish", items: left },
        ],
        foot: button
          ? `Nothing is submitted. Check the boxes, then press ${button} yourself.`
          : "Nothing is submitted. Check the boxes, then press the form's own button yourself.",
        sticky: left.length > 0 || check.length > 0,
      });
    }

    return { show: render, report, close };
  })();
})();
