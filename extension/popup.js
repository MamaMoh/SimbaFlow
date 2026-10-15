/**
 * The popup.
 *
 * It knows three things: how to ask SimbaFlow what a form wants, how to find a box on a page by
 * the label printed beside it, and how to type into one. It does not know what a sponsor is,
 * which portal it is standing on, or that a date is a date. All of that is decided on the
 * SimbaFlow side and arrives as a list of labels and text — so a portal that renames a box is
 * fixed by a deploy there, not by twenty people re-installing this.
 *
 * It asks for no permission on any portal. `activeTab` grants access to the one tab you are
 * looking at, at the moment you click the toolbar button, and nothing else: there is no list of
 * portal hostnames here because the page is recognised by the labels printed on it.
 */

const DEFAULT_ORIGIN = "https://app.laba.et";

const $ = (id) => document.getElementById(id);
const say = (text) => ($("message").textContent = text ?? "");

const state = { manifest: null, candidate: null, plan: null, results: [] };

/* ─── talking to SimbaFlow ──────────────────────────────────────────────────── */

async function simbaflowOrigin() {
  const stored = await chrome.storage.local.get("origin");
  return stored.origin || DEFAULT_ORIGIN;
}

async function api(path) {
  const res = await fetch((await simbaflowOrigin()) + path, { credentials: "include" });

  // Signed out does not arrive as a 401. SimbaFlow's middleware redirects to the login page
  // before it reaches the branch that would return one, so a fetch follows the redirect and
  // comes back with that page and a 200 on it. Parsing that as JSON fails with a complaint
  // about a "<", which tells the person nothing about having been signed out.
  if (res.status === 401 || res.redirected) throw new Error("SIGNED_OUT");
  if (!res.ok) throw new Error(`SimbaFlow said ${res.status}`);
  if (!(res.headers.get("content-type") || "").includes("application/json"))
    throw new Error("SIGNED_OUT");

  return res.json();
}

/**
 * Runs one of the page functions in the tab in front of the person.
 *
 * The file goes in first and the call follows, rather than sending a function across on its own.
 * That is what lets filling and diagnosing share one definition of what a box is called — send
 * them separately and each gets its own copy, which drift, and then the diagnosis describes a
 * page the filler never saw.
 */
async function onActiveTab(call, args = []) {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!tab?.id) throw new Error("There is no page here to fill.");

  await chrome.scripting.executeScript({ target: { tabId: tab.id }, files: ["page-functions.js"] });

  const [out] = await chrome.scripting.executeScript({
    target: { tabId: tab.id },
    func: (name, passed) => window.__simbaflow[name](...passed),
    args: [call, args],
  });
  return out?.result;
}

/* ─── which form is this ────────────────────────────────────────────────────── */

const normLabel = (s) =>
  String(s || "")
    .replace(/[*:]/g, " ")
    .replace(/\s+/g, " ")
    .trim()
    .toLowerCase();

function bestManifest(manifests, pageLabels) {
  let best = null;
  for (const manifest of manifests) {
    if (manifest.direction !== "write") continue;
    let hits = 0;
    for (const field of manifest.fields) {
      const want = normLabel(field.label);
      if (pageLabels.some((l) => l === want || l.includes(want))) hits += 1;
    }
    if (!best || hits > best.hits) best = { manifest, hits };
  }
  // Three matching labels is a form we know; one is a coincidence.
  return best && best.hits >= 3 ? best.manifest : null;
}

/* ─── screens ───────────────────────────────────────────────────────────────── */

function show(which) {
  for (const name of ["empty", "search", "plan"]) {
    $(`state-${name}`).hidden = name !== which;
  }
}

function showEmpty(title, detail, action) {
  $("empty-title").textContent = title;
  $("empty-detail").textContent = detail ?? "";
  const button = $("empty-action");
  if (action) {
    button.textContent = action.label;
    button.hidden = false;
    button.onclick = action.onClick;
  } else {
    button.hidden = true;
  }
  show("empty");
}

/**
 * "Hiwot Tadesse" → "HT". Two letters, because three in a 34px circle is a smudge.
 *
 * Falls back to the passport rather than returning nothing: a record with no name on it is rare
 * and an empty circle beside a passport number reads as a broken screen rather than a thin one.
 */
const initialsOf = (name, fallback = "") => {
  const letters = String(name || "")
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");
  return letters || String(fallback).slice(0, 2).toUpperCase() || "—";
};

const icon = (paths) =>
  `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"
        stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths}</svg>`;

const TICK = icon('<path d="M20 6 9 17l-5-5"/>');
const SPINNER = `<svg class="spin" viewBox="0 0 24 24" fill="none" stroke="currentColor"
  stroke-width="2.4" stroke-linecap="round" aria-hidden="true"><path d="M21 12a9 9 0 1 1-6.2-8.6"/></svg>`;
const BANG = icon('<circle cx="12" cy="12" r="9"/><path d="M12 8v5M12 16h.01"/>');
const DOWN = icon('<path d="M12 3v13"/><path d="m7 11 5 5 5-5"/><path d="M5 21h14"/>');

/* ─── starting up ───────────────────────────────────────────────────────────── */

async function start() {
  $("version").textContent = `Version ${chrome.runtime.getManifest().version}`;
  $("privacy").href = `${await simbaflowOrigin()}/privacy`;

  try {
    const [{ data: manifests }, pageLabels] = await Promise.all([
      api("/api/site-maps"),
      onActiveTab("collectLabels"),
    ]);

    state.manifest = bestManifest(manifests ?? [], pageLabels ?? []);

    if (!state.manifest) {
      $("form-name").textContent = "No form recognised";
      // A page with almost no boxes on it is a page to read, not one to fill — a candidate
      // record on a portal looks exactly like this. Saying so beats "not a form I know", which
      // reads as a fault rather than as the wrong page.
      const boxes = (await onActiveTab("describe"))?.controls?.length ?? 0;
      showEmpty(
        boxes < 5 ? "There is nothing to fill on this page" : "This is not a form I know",
        boxes < 5
          ? "This looks like a page to read rather than a form. To bring a candidate into SimbaFlow, copy the whole page and paste it into Candidates → New."
          : "Open the insurance, visa or LMIS contract form and click here again. If you are on it already, the labels differ from the ones on file — copy them below and they can be corrected.",
      );
      return;
    }

    $("form-name").textContent = state.manifest.title;
    void setUpPaste();
    show("search");
    $("search").focus();
  } catch (err) {
    if (err.message === "SIGNED_OUT") {
      $("form-name").textContent = "Not signed in";
      showEmpty("Sign in to SimbaFlow first", "This uses the session in this browser.", {
        label: "Open SimbaFlow",
        onClick: async () => chrome.tabs.create({ url: await simbaflowOrigin() }),
      });
      return;
    }
    $("form-name").textContent = "Could not start";
    showEmpty("SimbaFlow could not be reached", err.message ?? "");
  }
}

/* ─── finding somebody ──────────────────────────────────────────────────────── */

let searchTimer;
let highlighted = -1;

$("search").addEventListener("input", (e) => {
  const term = e.target.value.trim();
  clearTimeout(searchTimer);
  highlighted = -1;
  if (term.length < 2) {
    $("results").innerHTML = "";
    $("search-hint").textContent = "Type a name, or part of a passport number.";
    return;
  }
  $("search-hint").innerHTML = `${SPINNER} Searching…`;
  searchTimer = setTimeout(() => void search(term), 250);
});

$("search").addEventListener("keydown", (e) => {
  const rows = [...$("results").querySelectorAll("li[data-id]")];
  if (rows.length === 0) return;

  if (e.key === "ArrowDown" || e.key === "ArrowUp") {
    e.preventDefault();
    highlighted =
      e.key === "ArrowDown"
        ? Math.min(highlighted + 1, rows.length - 1)
        : Math.max(highlighted - 1, 0);
    rows.forEach((row, i) => row.setAttribute("aria-selected", String(i === highlighted)));
    rows[highlighted].scrollIntoView({ block: "nearest" });
  } else if (e.key === "Enter" && highlighted >= 0) {
    e.preventDefault();
    void choose(rows[highlighted].dataset.id);
  }
});

async function search(term) {
  try {
    const body = await api(
      `/api/proxy/candidates?page=1&pageSize=8&search=${encodeURIComponent(term)}`,
    );
    const items = body?.data?.items ?? [];
    const list = $("results");
    list.innerHTML = "";

    if (items.length === 0) {
      $("search-hint").textContent = "Nobody by that name.";
      return;
    }
    $("search-hint").textContent = items.length === 1 ? "One match." : `${items.length} matches.`;

    for (const c of items) {
      // The list gives one assembled name, not the three parts — asking for parts it does not
      // send left every row showing a passport number and an empty circle where a person was.
      const name =
        c.fullName || [c.firstName, c.middleName, c.lastName].filter(Boolean).join(" ");
      const li = document.createElement("li");
      li.dataset.id = c.id;
      li.setAttribute("role", "option");
      li.innerHTML = `
        <span class="avatar">${initialsOf(name, c.passportNumber)}</span>
        <span class="who">
          <span class="who-name"></span>
          <span class="who-sub"></span>
        </span>`;
      li.querySelector(".who-name").textContent = name;
      li.querySelector(".who-sub").textContent =
        [c.passportNumber, c.labourId].filter(Boolean).join(" · ") || "No passport on file";
      li.addEventListener("click", () => void choose(c.id));
      list.appendChild(li);
    }
  } catch (err) {
    $("search-hint").textContent =
      err.message === "SIGNED_OUT" ? "Signed out — open SimbaFlow and sign in." : "Search failed.";
  }
}

/* ─── what will go in ───────────────────────────────────────────────────────── */

async function choose(candidateId) {
  try {
    const body = await api(
      `/api/site-maps/plan?site=${encodeURIComponent(state.manifest.id)}&candidateId=${candidateId}`,
    );
    state.candidate = body.data.candidate;
    state.plan = body.data.plan;
    state.results = [];

    $("chosen").textContent = state.candidate.name;
    $("chosen-sub").textContent = state.candidate.passportNumber || "No passport on file";
    $("initials").textContent = initialsOf(state.candidate.name, state.candidate.passportNumber);
    $("outcome").innerHTML = "";
    $("fill-label").textContent = "Fill this form";
    $("fill").disabled = false;

    renderChips([
      { text: `${state.plan.steps.length} ready`, tone: "" },
      ...(state.plan.missing.length
        ? [{ text: `${state.plan.missing.length} not on file`, tone: "attention", icon: BANG }]
        : []),
    ]);

    show("plan");
    say("");
  } catch (err) {
    say(err.message || "Could not build the form.");
  }
}

function renderChips(chips) {
  $("counts").innerHTML = "";
  for (const chip of chips) {
    const span = document.createElement("span");
    span.className = `chip ${chip.tone ?? ""}`.trim();
    span.innerHTML = (chip.icon ?? "") + "<span></span>";
    span.querySelector("span").textContent = chip.text;
    $("counts").appendChild(span);
  }
}

const WHY = {
  dropdown: "choose it yourself",
  nooption: "no matching option",
  locked: "cannot be typed into",
  rejected: "the form refused it",
  notfound: "not on this page",
  absent: "not held in SimbaFlow",
  check: "written into a date picker — make sure it reads right",
};

$("fill").addEventListener("click", async () => {
  $("fill").disabled = true;
  $("fill-icon").innerHTML = SPINNER;
  $("fill-label").textContent = "Filling…";
  try {
    const results = await onActiveTab("fill", [state.plan.steps]);
    state.results = results;

    const filled = results.filter((r) => r.status === "filled").length;
    const kept = results.filter((r) => r.status === "kept").length;
    // Filled, and still wanting a pair of eyes. A date written into a box the picker owns is in
    // our spelling rather than the form's, so it is counted as done and listed anyway — the one
    // place where saying nothing would be the dangerous thing.
    const check = results.filter((r) => r.status === "check");
    const left = [
      ...results.filter((r) => !["filled", "kept", "check"].includes(r.status)),
      ...state.plan.missing.map((m) => ({ label: m.label, status: "absent" })),
    ];

    renderChips([
      { text: `${filled + check.length} filled`, tone: "good", icon: TICK },
      ...(kept ? [{ text: `${kept} left as they were`, tone: "" }] : []),
      ...(check.length ? [{ text: `${check.length} to check`, tone: "attention" }] : []),
      ...(left.length ? [{ text: `${left.length} need you`, tone: "attention", icon: BANG }] : []),
    ]);

    $("outcome").innerHTML = "";
    const section = (head, items) => {
      if (items.length === 0) return;
      const box = document.createElement("div");
      box.className = "outcome";
      box.innerHTML = `<div class="outcome-head"></div><ul></ul>`;
      box.querySelector(".outcome-head").textContent = head;
      const list = box.querySelector("ul");
      for (const item of items) {
        const li = document.createElement("li");
        li.innerHTML = `<span class="what"></span><span class="why"></span>`;
        li.querySelector(".what").textContent = item.label;
        li.querySelector(".why").textContent =
          item.status === "dropdown" && item.value
            ? `choose “${item.value}”`
            : WHY[item.status] ?? item.status;
        list.appendChild(li);
      }
      $("outcome").appendChild(box);
    };
    section("Worth a look before you submit", check);
    section("For you to finish", left);

    $("fill-label").textContent = "Fill again";
  } catch (err) {
    say(err.message || "Could not fill the form.");
    $("fill-label").textContent = "Fill this form";
  } finally {
    $("fill-icon").innerHTML = DOWN;
    $("fill").disabled = false;
  }
});

/* ─── filling on paste ──────────────────────────────────────────────────────── */

/**
 * The switch that lets this site fill on paste.
 *
 * Copy to insurance in SimbaFlow puts a candidate on the clipboard; pasting it into the form
 * fills every box. That needs a listener already on the page when the paste happens, which the
 * toolbar's one-click access cannot provide — so it is offered here, on a form this already
 * recognises, and the browser asks the person to allow this one site. Turning it off hands the
 * access back. background.js keeps the listening in step with whatever the browser has granted.
 */
async function setUpPaste() {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  let origin;
  try {
    origin = new URL(tab?.url ?? "").origin;
  } catch {
    return;
  }
  if (!/^https?:/.test(origin)) return;

  const pattern = `${origin}/*`;
  const host = new URL(origin).host;
  const toggle = $("paste-switch");
  const describe = (on) => {
    $("paste-sub").textContent = on
      ? `On for ${host}. Copy a candidate in SimbaFlow, then paste into this form.`
      : `Copy a candidate in SimbaFlow and paste here to fill every box. Asks to allow ${host}.`;
  };

  toggle.checked = await chrome.permissions.contains({ origins: [pattern] });
  describe(toggle.checked);
  $("paste").hidden = false;

  toggle.addEventListener("change", () => {
    const on = toggle.checked;
    toggle.disabled = true;
    // Asked for inside the click itself: the browser only shows its permission prompt while the
    // gesture that asked for it is still current, so nothing may be awaited before this line.
    const asked = on
      ? chrome.permissions.request({ origins: [pattern] })
      : chrome.permissions.remove({ origins: [pattern] });

    void asked
      .then(async (granted) => {
        const nowOn = on ? granted : !granted;
        toggle.checked = nowOn;
        describe(nowOn);
        await chrome.runtime.sendMessage({ type: "paste-sites:sync" });
        // Registered scripts reach the next page load. This one is already open, so it is told
        // directly — on, by putting the listener in now; off, by telling the one already there.
        await chrome.scripting.executeScript({
          target: { tabId: tab.id, allFrames: true },
          func: (off) => {
            window.__simbaflowPasteOff = off;
          },
          args: [!nowOn],
        });
        if (nowOn) {
          await chrome.scripting.executeScript({
            target: { tabId: tab.id, allFrames: true },
            files: ["page-functions.js", "paste.js"],
          });
        }
        say("");
      })
      .catch((err) => say(err?.message || "Could not change that."))
      .finally(() => {
        toggle.disabled = false;
      });
  });
}

$("back").addEventListener("click", () => {
  show("search");
  $("search").value = "";
  $("results").innerHTML = "";
  $("search-hint").textContent = "Type a name, or part of a passport number.";
  highlighted = -1;
  $("search").focus();
});

/**
 * What this page calls its boxes, on the clipboard.
 *
 * "Not on this page" about a box that is visibly on the page has exactly two causes — the label
 * is printed differently from what the map expects, or the box is somewhere this cannot reach —
 * and from outside they look identical. Rather than describing a form over a screenshot, this
 * hands over the page's own words so the map can be corrected against them.
 */
$("labels").addEventListener("click", async () => {
  try {
    const page = await onActiveTab("describe");
    await navigator.clipboard.writeText(JSON.stringify(page, null, 1));
    say(`Copied ${page.controls.length} boxes to the clipboard.`);
  } catch (err) {
    say(err.message || "Could not read the page.");
  }
});

void start();
