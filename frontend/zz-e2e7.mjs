import { chromium } from "@playwright/test";
const SHOTS = "/private/tmp/claude-501/-Users-mama-Dev-simbaflow/cd238b41-ca19-4750-be0e-8dab90a19925/scratchpad/shots";
const base = "https://app.laba.et";
const b = await chromium.launch({ downloadsPath: SHOTS });
const ctx = await b.newContext({ viewport: { width: 1440, height: 900 }, acceptDownloads: true });
const page = await ctx.newPage();
page.setDefaultNavigationTimeout(60000);
const msgs = [];
page.on("response", r => { if (r.status() >= 400) msgs.push(`${r.status()} ${r.request().method()} ${r.url().replace(base,"")}`); });

await page.goto(base + "/login", { waitUntil: "networkidle" });
await page.waitForTimeout(2000);
await page.locator('input[name="username"]').fill("owner@demo.local");
await page.locator('input[name="password"]').fill("Demo@2026!");
await page.getByRole("button", { name: /sign in/i }).click();
await page.waitForURL(u => !u.pathname.includes("/login"), { timeout: 60000 });

// A) Documents tab on the candidate we just made
await page.goto(base + "/candidates/f2f19a25-f398-448b-86ec-9235e7f85174", { waitUntil: "networkidle" });
await page.waitForTimeout(3000);
await page.getByRole("tab", { name: /documents/i }).click().catch(async()=>{ await page.getByText(/^Documents$/).click().catch(()=>{}); });
await page.waitForTimeout(3500);
await page.screenshot({ path: `${SHOTS}/12-documents-tab.png`, fullPage: true });

// B) duplicate passport
msgs.length = 0;
await page.goto(base + "/candidates/new", { waitUntil: "networkidle" });
await page.waitForSelector('button:has-text("Next")', { timeout: 40000 });
await page.waitForTimeout(1500);
await page.getByRole("button", { name: /^next$/i }).click();
await page.waitForTimeout(1500);
await page.locator('input[name="firstName"]').fill("Dup");
await page.locator('input[name="lastName"]').fill("Test");
await page.locator('input[name="passportNumber"]').fill("ET9900123"); // same as before
await page.locator('input[name="dateOfBirth"]').first().fill("1997-06-15");
await page.locator('button:has-text("Select")').first().click(); await page.waitForTimeout(700);
await page.getByRole("option", { name: /female/i }).click().catch(()=>{});
await page.waitForTimeout(1200);
await page.getByRole("button", { name: /save now/i }).click();
await page.waitForTimeout(5000);
await page.screenshot({ path: `${SHOTS}/13-duplicate-passport.png`, fullPage: true });
console.log("after duplicate save, url:", page.url());
console.log("duplicate responses:", msgs.join(" | ") || "(none)");

// C) Export download
await page.goto(base + "/candidates", { waitUntil: "networkidle" });
await page.waitForTimeout(2500);
const [dl] = await Promise.all([
  page.waitForEvent("download", { timeout: 20000 }).catch(()=>null),
  page.getByRole("button", { name: /export/i }).first().click(),
]);
console.log("download:", dl ? await dl.suggestedFilename() : "NONE");
await page.waitForTimeout(1500);
await page.screenshot({ path: `${SHOTS}/14-after-export.png` });
await b.close();
