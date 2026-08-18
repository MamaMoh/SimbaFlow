import { chromium } from "@playwright/test";
const base = "https://app.laba.et";
const browser = await chromium.launch();
const page = await browser.newPage();
page.setDefaultNavigationTimeout(60000);
const bad = [];
page.on("response", r => { if (r.status() >= 400) bad.push(`${r.status()} ${r.url().replace(base,"")}`); });
page.on("pageerror", e => bad.push("pageerror: " + e.message));

// wait for hydration before interacting, otherwise the native form submit fires
await page.goto(base + "/login", { waitUntil: "networkidle" });
await page.waitForTimeout(3000);
await page.locator('input[name="username"]').fill("admin");
await page.locator('input[name="password"]').fill("Admin@123!");
await page.getByRole("button", { name: /sign in/i }).click();
await page.waitForURL(u => !u.pathname.includes("/login"), { timeout: 60000 });
console.log("logged in ->", page.url());
await page.goto(base + "/partners", { waitUntil: "networkidle" });
bad.length = 0;

const t0 = Date.now();
let verdict = "ok";
try {
  await page.getByRole("link", { name: /partner catalog/i }).click({ timeout: 20000 });
  await page.waitForURL(/admin\/partners/, { timeout: 20000 });
  await page.waitForSelector("table", { timeout: 20000 });
  await page.evaluate(() => 1);
} catch(e){ verdict = "FAIL: " + e.message.split("\n")[0]; }
console.log(`click "Partner catalog": ${Date.now()-t0}ms  ${verdict}`);
console.log(`url: ${page.url()}`);
console.log(`catalog rows: ${await page.locator("table tbody tr").count().catch(()=>0)}`);
console.log("failed requests:\n" + (bad.join("\n") || "(none)"));
await browser.close();
