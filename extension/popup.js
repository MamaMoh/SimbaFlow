/**
 * The whole extension.
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

let state = { manifest: null, candidate: null, plan: null };

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

async function onActiveTab(func, args = []) {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!tab?.id) throw new Error("No page to fill");
  const [out] = await chrome.scripting.executeScript({ target: { tabId: tab.id }, func, args });
  return out?.result;
}

/* ─── popup ─────────────────────────────────────────────────────────────────── */

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

async function start() {
  try {
    const [{ data: manifests }, pageLabels] = await Promise.all([
      api("/api/site-maps"),
      onActiveTab(collectLabelsOnPage),
    ]);

    state.manifest = bestManifest(manifests ?? [], pageLabels ?? []);
    if (!state.manifest) {
      $("form-name").textContent = "not a form I know";
      say("Open the insurance or visa form first, then click here again.");
      return;
    }

    $("form-name").textContent = state.manifest.title;
    $("search-area").hidden = false;
    $("search").focus();
    say("");
  } catch (err) {
    if (err.message === "SIGNED_OUT") {
      say("Sign in to SimbaFlow first, then click here again.");
      return;
    }
    say(err.message || "Could not reach SimbaFlow.");
  }
}

let searchTimer;
$("search").addEventListener("input", (e) => {
  const term = e.target.value.trim();
  clearTimeout(searchTimer);
  if (term.length < 2) {
    $("results").innerHTML = "";
    return;
  }
  searchTimer = setTimeout(() => void search(term), 250);
});

async function search(term) {
  try {
    const body = await api(
      `/api/proxy/candidates?page=1&pageSize=8&search=${encodeURIComponent(term)}`,
    );
    const items = body?.data?.items ?? [];
    $("results").innerHTML = "";
    if (items.length === 0) {
      $("results").innerHTML = `<li class="muted">Nobody by that name.</li>`;
      return;
    }
    for (const c of items) {
      const li = document.createElement("li");
      const name = [c.firstName, c.middleName, c.lastName].filter(Boolean).join(" ");
      li.innerHTML = `<div>${name}</div><div class="sub muted">${c.passportNumber ?? ""}</div>`;
      li.addEventListener("click", () => void choose(c.id));
      $("results").appendChild(li);
    }
  } catch (err) {
    say(err.message === "SIGNED_OUT" ? "Sign in to SimbaFlow first." : "Search failed.");
  }
}

async function choose(candidateId) {
  try {
    const body = await api(
      `/api/site-maps/plan?site=${encodeURIComponent(state.manifest.id)}&candidateId=${candidateId}`,
    );
    state.candidate = body.data.candidate;
    state.plan = body.data.plan;

    $("search-area").hidden = true;
    $("plan-area").hidden = false;
    $("chosen").textContent = state.candidate.name;
    const ready = state.plan.steps.length;
    const gaps = state.plan.missing.length;
    $("counts").textContent = gaps
      ? `${ready} boxes ready · ${gaps} the record cannot fill`
      : `${ready} boxes ready`;
    $("outcome").innerHTML = "";
  } catch (err) {
    say(err.message || "Could not build the form.");
  }
}

$("fill").addEventListener("click", async () => {
  $("fill").disabled = true;
  try {
    const results = await onActiveTab(fillOnPage, [state.plan.steps]);
    const filled = results.filter((r) => r.status === "filled").length;
    const kept = results.filter((r) => r.status === "kept").length;
    const left = results.filter((r) => !["filled", "kept"].includes(r.status));

    $("counts").textContent = `${filled} filled${kept ? `, ${kept} left as they were` : ""}`;
    $("outcome").innerHTML = "";
    for (const r of left) {
      const li = document.createElement("li");
      li.className = "bad";
      const why =
        r.status === "dropdown"
          ? "dropdown — choose it yourself"
          : r.status === "nooption"
            ? "no matching option"
            : r.status === "locked"
              ? "the form will not let this be typed"
            : r.status === "rejected"
              ? "the form refused it"
              : "not on this page";
      li.textContent = `${r.label} — ${why}`;
      $("outcome").appendChild(li);
    }
    for (const m of state.plan.missing) {
      const li = document.createElement("li");
      li.className = "bad";
      li.textContent = `${m.label} — not held in SimbaFlow`;
      $("outcome").appendChild(li);
    }
  } catch (err) {
    say(err.message || "Could not fill the form.");
  } finally {
    $("fill").disabled = false;
  }
});

$("back").addEventListener("click", () => {
  $("plan-area").hidden = true;
  $("search-area").hidden = false;
  $("search").value = "";
  $("results").innerHTML = "";
  $("search").focus();
});

void start();
