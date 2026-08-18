import { chromium } from "@playwright/test";
const SHOTS = "/private/tmp/claude-501/-Users-mama-Dev-simbaflow/cd238b41-ca19-4750-be0e-8dab90a19925/scratchpad/shots";
const base = "https://app.laba.et";
const b = await chromium.launch();
const page = await b.newPage({ viewport: { width: 1440, height: 900 } });
page.setDefaultNavigationTimeout(60000);
const msgs = [];
page.on("response", r => { if (r.url().includes("/documents")) msgs.push(`${r.status()} ${r.request().method()} ${r.url().replace(base,"")}`); });

await page.goto(base + "/login", { waitUntil: "networkidle" });
await page.waitForTimeout(2000);
await page.locator('input[name="username"]').fill("owner@demo.local");
await page.locator('input[name="password"]').fill("Demo@2026!");
await page.getByRole("button", { name: /sign in/i }).click();
await page.waitForURL(u => !u.pathname.includes("/login"), { timeout: 60000 });

await page.goto(base + "/candidates/f2f19a25-f398-448b-86ec-9235e7f85174", { waitUntil: "networkidle" });
await page.waitForTimeout(2500);
await page.getByRole("tab", { name: /documents/i }).click().catch(()=>{});
await page.waitForTimeout(2500);

const fi = page.locator('input[type="file"]').first();
await fi.setInputFiles("/tmp/passport.pdf");
await page.waitForTimeout(6000);
await page.screenshot({ path: `${SHOTS}/15-after-upload.png`, fullPage: true });
const txt = await page.locator("body").innerText();
console.log("shows 'No documents yet' after upload:", /No documents yet/i.test(txt));
console.log("document requests:\n" + (msgs.join("\n") || "(none)"));
await b.close();
