import { chromium } from "@playwright/test";
const SHOTS = "/private/tmp/claude-501/-Users-mama-Dev-simbaflow/cd238b41-ca19-4750-be0e-8dab90a19925/scratchpad/shots";
const base = "https://app.laba.et";
const b = await chromium.launch();
const page = await b.newPage({ viewport: { width: 1440, height: 900 } });
page.setDefaultNavigationTimeout(60000);
const msgs = [];
page.on("pageerror", e => msgs.push("PAGEERROR: " + e.message));
page.on("response", r => { if (r.status() >= 400) msgs.push(`${r.status()} ${r.request().method()} ${r.url().replace(base,"")}`); });

await page.goto(base + "/login", { waitUntil: "networkidle" });
await page.waitForTimeout(2000);
await page.locator('input[name="username"]').fill("owner@demo.local");
await page.locator('input[name="password"]').fill("Demo@2026!");
await page.getByRole("button", { name: /sign in/i }).click();
await page.waitForURL(u => !u.pathname.includes("/login"), { timeout: 60000 });

await page.goto(base + "/candidates", { waitUntil: "networkidle" });
await page.waitForTimeout(1500);
await page.getByRole("button", { name: /create/i }).first().click();
await page.waitForTimeout(2500);
await page.screenshot({ path: `${SHOTS}/05-create-step1.png`, fullPage: true });

// TEST: press Next with nothing filled -> what validation shows?
const next = page.getByRole("button", { name: /^next$/i });
if (await next.isVisible().catch(()=>false)) {
  await next.click(); await page.waitForTimeout(1200);
  await page.screenshot({ path: `${SHOTS}/06-after-next-empty.png`, fullPage: true });
  await next.click(); await page.waitForTimeout(1500);
  await page.screenshot({ path: `${SHOTS}/07-step2-empty-validation.png`, fullPage: true });
}
console.log("url:", page.url());
console.log("messages:\n" + (msgs.join("\n") || "(none)"));
await b.close();
