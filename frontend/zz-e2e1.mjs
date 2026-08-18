import { chromium } from "@playwright/test";
const SHOTS = "/private/tmp/claude-501/-Users-mama-Dev-simbaflow/cd238b41-ca19-4750-be0e-8dab90a19925/scratchpad/shots";
const base = "https://app.laba.et";
const b = await chromium.launch();
const page = await b.newPage({ viewport: { width: 1440, height: 900 } });
page.setDefaultNavigationTimeout(60000);
const errs = [];
page.on("pageerror", e => errs.push("PAGEERROR " + e.message));
page.on("response", r => { if (r.status() >= 400) errs.push(`${r.status()} ${r.request().method()} ${r.url().replace(base,"")}`); });

await page.goto(base + "/login", { waitUntil: "networkidle" });
await page.waitForTimeout(2500);
await page.screenshot({ path: `${SHOTS}/01-login.png` });

await page.locator('input[name="username"]').fill("owner@demo.local");
await page.locator('input[name="password"]').fill("Demo@2026!");
await page.getByRole("button", { name: /sign in/i }).click();
await page.waitForURL(u => !u.pathname.includes("/login"), { timeout: 60000 });
await page.waitForTimeout(3000);
await page.screenshot({ path: `${SHOTS}/02-overview.png`, fullPage: true });

await page.goto(base + "/candidates", { waitUntil: "networkidle" });
await page.waitForTimeout(2000);
await page.screenshot({ path: `${SHOTS}/03-candidates.png`, fullPage: true });

console.log("errors/4xx:\n" + (errs.join("\n") || "(none)"));
await b.close();
