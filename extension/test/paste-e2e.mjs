/**
 * Copy to insurance, pasted into the insurance form, in a real Chromium with the real extension.
 *
 *   cd frontend && npm ci   # once, for Playwright
 *   node ../extension/test/paste-e2e.mjs [--shots <dir>]
 *
 * CHROMIUM_PATH points it at a browser other than the one Playwright downloaded.
 *
 * The extension is loaded unpacked from a copy of this folder whose manifest also grants the
 * local test server — standing in for the person pressing Allow on the popup's switch, which a
 * script cannot do. Everything after that is the shipped code: background.js sees the grant and
 * registers the paste listener, the fixture form loads, a payload goes onto the real clipboard,
 * and Ctrl+V does the rest.
 *
 * The payload is encoded here exactly as frontend/lib/site-bridge/clipboard.ts encodes it; that
 * file's test pins the bytes, so the two cannot drift without one of them failing.
 */
import { cpSync, mkdtempSync, readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { createServer } from "node:http";
import { createRequire } from "node:module";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import assert from "node:assert/strict";

const here = dirname(new URL(import.meta.url).pathname);
const ext = resolve(here, "..");
const require = createRequire(resolve(ext, "..", "frontend", "package.json"));
const { chromium } = require("playwright");

const shotsAt = process.argv.includes("--shots")
  ? resolve(process.argv[process.argv.indexOf("--shots") + 1])
  : null;
if (shotsAt) mkdirSync(shotsAt, { recursive: true });

/* ─── a portal, locally ─────────────────────────────────────────────────────── */

// The fixture without its inlined script: on the real portal the page knows nothing of us.
const form = readFileSync(join(here, "insurance-form.html"), "utf8").replace(
  '<script src="../page-functions.js"></script>',
  "",
);
const other = `<!doctype html><title>Webmail</title><label>To</label><input id="to">
  <label>Subject</label><input id="subject"><textarea id="body"></textarea>`;

const server = createServer((req, res) => {
  res.writeHead(200, { "content-type": "text/html; charset=utf-8" });
  res.end(req.url.startsWith("/mail") ? other : form);
});
await new Promise((ok) => server.listen(0, "127.0.0.1", ok));
const origin = `http://127.0.0.1:${server.address().port}`;

/* ─── the extension, granted that portal ───────────────────────────────────── */

const copy = mkdtempSync(join(tmpdir(), "simbaflow-ext-"));
cpSync(ext, copy, { recursive: true, filter: (p) => !p.includes(`${ext}/test`) });
const manifest = JSON.parse(readFileSync(join(copy, "manifest.json"), "utf8"));
manifest.host_permissions.push("http://127.0.0.1/*");
writeFileSync(join(copy, "manifest.json"), JSON.stringify(manifest, null, 2));

const profile = mkdtempSync(join(tmpdir(), "simbaflow-profile-"));
const context = await chromium.launchPersistentContext(profile, {
  // The full browser rather than the headless shell, which cannot load extensions.
  ...(process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : { channel: "chromium" }),
  headless: true,
  args: [`--disable-extensions-except=${copy}`, `--load-extension=${copy}`],
  permissions: ["clipboard-read", "clipboard-write"],
  viewport: { width: 1000, height: 640 },
});

const worker = context.serviceWorkers()[0] ?? (await context.waitForEvent("serviceworker"));
// The grant is in the manifest, so it is there at install; wait for the sync to have seen it.
await worker.evaluate(async () => {
  for (let i = 0; i < 50; i += 1) {
    const scripts = await chrome.scripting.getRegisteredContentScripts();
    if (scripts.some((s) => s.id.startsWith("paste:"))) return;
    await new Promise((r) => setTimeout(r, 100));
  }
  throw new Error("background.js never registered the paste listener");
});

/* ─── payloads ──────────────────────────────────────────────────────────────── */

const step = (label, value, extra = {}) => ({
  label, kind: "text", value, display: value, alternatives: [], keepTheirs: false, ...extra,
});

const payload = (overrides = {}) => {
  const steps = [
    step("Title", "Ms.", { kind: "select" }),
    step("First Name", "Hiwot"),
    step("Father Name", "Tadesse"),
    step("Grand Father Name", "Gebru"),
    step("Gender", "Female", { kind: "select" }),
    step("Date Of Birth", "1996-01-01", { kind: "date", display: "01/01/1996" }),
    step("Passport Number", "EQ0000001"),
    step("Labor ID", "LB-000001"),
    step("Phone", "+251911000000", { kind: "phone" }),
    step("Woreda", "03"),
    step("Sub city", "Bole"),
    step("Region", "Addis Ababa", { kind: "select" }),
    step("Country", "Ethiopia", { keepTheirs: true }),
    step("Country Of Destination", "Saudi Arabia", { kind: "select", alternatives: ["KSA"] }),
  ];
  const missing = [{ label: "Email" }];
  const body = {
    site: { id: "insurance-member", title: "Insurance — add insured" },
    candidate: { id: "c0ffee00-0000-4000-8000-000000000001", name: "Hiwot Tadesse Gebru", passportNumber: "EQ0000001" },
    plan: { steps, missing, repeatButton: "Add Member" },
    recognise: [...steps, ...missing].map((s) => s.label),
    expiresAt: Date.now() + 20 * 60 * 1000,
    ...overrides,
  };
  return "simbaflow-fill:1:" + Buffer.from(JSON.stringify(body), "utf8").toString("base64");
};

async function pasteInto(page, selector, text) {
  await page.evaluate((t) => navigator.clipboard.writeText(t), text);
  await page.click(selector);
  await page.keyboard.press("ControlOrMeta+V");
  await page.waitForTimeout(400);
}

const results = [];
const check = async (name, fn) => {
  try {
    await fn();
    results.push(`ok    ${name}`);
  } catch (err) {
    results.push(`FAIL  ${name}\n      ${err.message.split("\n")[0]}`);
  }
};

/* ─── the cases ─────────────────────────────────────────────────────────────── */

const page = await context.newPage();
page.on("pageerror", (e) => results.push(`FAIL  page error: ${e.message}`));
await page.emulateMedia({ colorScheme: "light" });

await check("pasting the payload fills the form, and leaves the paste out of the box", async () => {
  await page.goto(`${origin}/insurance`);
  await pasteInto(page, 'input[placeholder="First Name"]', payload());
  const value = (sel) => page.locator(sel).inputValue();
  assert.equal(await value('input[placeholder="First Name"]'), "Hiwot");
  assert.equal(await value('input[placeholder="Father Name"]'), "Tadesse");
  assert.equal(await value('input[placeholder="Grand Father Name"]'), "Gebru");
  assert.equal(await value('input[placeholder="Passport Number"]'), "EQ0000001");
  assert.equal(await value('input[placeholder="Labor ID"]'), "LB-000001");
  assert.equal(await value('input[type="date"] >> nth=0'), "1996-01-01");
  assert.equal(await page.locator("select >> nth=0").evaluate((s) => s.value), "Ms.");
  assert.equal(await page.locator("select >> nth=1").evaluate((s) => s.value), "Female");
  assert.equal(await page.locator("select >> nth=2").evaluate((s) => s.value), "Saudi Arabia");
});

await check("what was already there is kept, and nothing is ticked or submitted", async () => {
  assert.equal(await page.locator('input[value="Ethiopia"]').inputValue(), "Ethiopia");
  assert.equal(await page.locator("#declare").isChecked(), false);
  // The custom Region widget is named for the person, not typed into.
  assert.equal(await page.locator('input[placeholder="Region"]').inputValue(), "");
});

await check("the card says who went in and what is left", async () => {
  assert.equal(await page.locator("simbaflow-paste").count(), 1);
  if (shotsAt) await page.screenshot({ path: join(shotsAt, "filled-light.png") });
  await page.emulateMedia({ colorScheme: "dark" });
  if (shotsAt) await page.screenshot({ path: join(shotsAt, "filled-dark.png") });
  await page.emulateMedia({ colorScheme: "light" });
  await page.keyboard.press("Escape");
  assert.equal(await page.locator("simbaflow-paste").count(), 0);
});

await check("an ordinary paste is left alone", async () => {
  await page.goto(`${origin}/insurance`);
  await pasteInto(page, 'input[placeholder="Email"]', "someone@example.com");
  assert.equal(await page.locator('input[placeholder="Email"]').inputValue(), "someone@example.com");
  assert.equal(await page.locator("simbaflow-paste").count(), 0);
});

await check("a stale copy fills nothing and says why", async () => {
  await page.goto(`${origin}/insurance`);
  await pasteInto(page, 'input[placeholder="First Name"]', payload({ expiresAt: Date.now() - 1 }));
  assert.equal(await page.locator('input[placeholder="First Name"]').inputValue(), "");
  assert.equal(await page.locator("simbaflow-paste").count(), 1);
  if (shotsAt) await page.screenshot({ path: join(shotsAt, "stale.png") });
});

await check("the wrong page fills nothing, and the payload does not land in its box", async () => {
  await page.goto(`${origin}/mail`);
  await pasteInto(page, "#subject", payload());
  assert.equal(await page.locator("#subject").inputValue(), "");
  assert.equal(await page.locator("simbaflow-paste").count(), 1);
  if (shotsAt) await page.screenshot({ path: join(shotsAt, "wrong-page.png") });
});

await check("a newer format is refused rather than misread", async () => {
  await page.goto(`${origin}/insurance`);
  await pasteInto(page, 'input[placeholder="First Name"]', "simbaflow-fill:2:e30=");
  assert.equal(await page.locator('input[placeholder="First Name"]').inputValue(), "");
  assert.equal(await page.locator("simbaflow-paste").count(), 1);
});

await check("a site whose access is gone stops listening", async () => {
  // What the browser leaves behind when access is withdrawn from its own settings page: a
  // registered listener for a site nothing grants any more. The next sync has to clear it.
  // (The test's own grant is in the manifest and cannot be withdrawn, so a stale one is planted.)
  const ids = await worker.evaluate(async () => {
    await chrome.scripting.registerContentScripts([
      { id: "paste:https://withdrawn.example/*", matches: ["http://127.0.0.1/never/*"], js: ["paste.js"] },
    ]);
    await resync();
    return (await chrome.scripting.getRegisteredContentScripts()).map((s) => s.id);
  });
  assert.deepEqual(ids, ["paste:http://127.0.0.1/*"]);
});

await context.close();
server.close();

console.log(results.join("\n"));
if (results.some((r) => r.startsWith("FAIL"))) process.exit(1);
