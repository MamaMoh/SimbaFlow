/**
 * Builds a runnable copy of the popup, for looking at.
 *
 * The real popup.html, the real popup.css and the real popup.js, with the browser's extension
 * APIs and SimbaFlow itself replaced by stubs. So what is reviewed is the actual interface and
 * the actual rendering code, not a mock-up of them that can quietly disagree.
 *
 * The font is inlined because a page opened from the file system in a preview pane fetches no
 * siblings, and the whole point of bundling Geist is to see whether it looks like the app.
 */
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";

const here = dirname(new URL(import.meta.url).pathname);
const ext = join(here, "..");

const css = readFileSync(join(ext, "popup.css"), "utf8").replace(
  'url("fonts/Geist-Variable.woff2") format("woff2-variations")',
  `url("data:font/woff2;base64,${readFileSync(join(ext, "fonts/Geist-Variable.woff2")).toString("base64")}") format("woff2-variations")`,
);

const version = JSON.parse(readFileSync(join(ext, "manifest.json"), "utf8")).version;
const html = readFileSync(join(ext, "popup.html"), "utf8");
const body = html.slice(html.indexOf("<body>") + 6, html.indexOf("</body>"))
  .replace('<script src="popup.js"></script>', "");

/** Invented, as every fixture in here is. */
const PLAN = {
  steps: Array.from({ length: 21 }, (_, i) => ({ label: `Box ${i + 1}`, kind: "text" })),
  missing: [
    { label: "Arabic First Name" },
    { label: "Arabic Father Name" },
    { label: "Arabic Grand Name" },
    { label: "Arabic Family Name" },
  ],
};

const STUB = `
<script>
window.chrome = {
  runtime: { getManifest: () => ({ version: "${version}" }), sendMessage: async () => ({ ok: true }) },
  storage: { local: { get: async () => ({}) } },
  tabs: { query: async () => [{ id: 1, url: "https://insurance.example/members/add" }], create: async () => {} },
  permissions: {
    contains: async () => false,
    request: async () => true,
    remove: async () => true,
  },
  scripting: {
    executeScript: async ({ func, args }) =>
      func ? [{ result: window.__stubPage(args[0], args[1]) }] : [{ result: null }],
  },
};

window.__stubPage = (call) => {
  if (call === "collectLabels") return ["visa type", "sponsor name", "passport number", "gender"];
  if (call === "describe") return { controls: new Array(36) };
  // A realistic spread: most filled, a few dropdowns, one the page would not take.
  return ${JSON.stringify(PLAN.steps)}.map((s, i) =>
    i < 13 ? { label: s.label, status: "filled" }
    : i < 14 ? { label: s.label, status: "kept" }
    : i < 20 ? { label: s.label, status: "dropdown" }
    : { label: s.label, status: "notfound" });
};

window.fetch = async (url) => {
  const json = (data) => ({
    ok: true, status: 200, redirected: false,
    headers: { get: () => "application/json" },
    json: async () => data,
  });
  if (url.includes("/api/site-maps/plan")) {
    return json({ data: {
      site: { id: "visa-application", title: "Visa platform — application" },
      candidate: { id: "1", name: "Hiwot Tadesse Gebru", passportNumber: "EQ0000001" },
      plan: ${JSON.stringify(PLAN)},
    }});
  }
  if (url.includes("/api/site-maps")) {
    return json({ data: [{
      id: "visa-application", title: "Visa platform — application", direction: "write",
      fields: [{ label: "Visa Type" }, { label: "Sponsor Name" }, { label: "Passport Number" }, { label: "Gender" }],
    }]});
  }
  if (url.includes("/candidates")) {
    return json({ data: { items: [
      // fullName, because that is what the list endpoint actually returns — a stub that sends
      // the three parts would have hidden the bug where every row came back nameless.
      { id: "1", fullName: "Hiwot Tadesse Gebru", passportNumber: "EQ0000001", labourId: "LAB0000123" },
      { id: "2", fullName: "Meron Abate Desta", passportNumber: "EQ0000002", labourId: "LAB0000124" },
      { id: "3", fullName: "Sara Nuru Hailu", passportNumber: "EQ0000003" },
    ]}});
  }
  return json({ data: [] });
};
</script>`;

const page = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Popup preview</title>
<style>${css}
/* Preview chrome only: the popup itself is 360px and unaware of this. */
html { background: #8884; }
body { width: 360px; margin: 24px auto; border: 1px solid var(--border);
       border-radius: 12px; overflow: hidden; box-shadow: 0 10px 30px #0002; }
</style></head>
<body>${body}${STUB}
<script>${readFileSync(join(ext, "popup.js"), "utf8")}</script>
</body></html>`;

writeFileSync(join(here, "popup-preview.generated.html"), page);
console.log("popup-preview.generated.html written");
