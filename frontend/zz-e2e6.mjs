import { chromium } from "@playwright/test";
const SHOTS = "/private/tmp/claude-501/-Users-mama-Dev-simbaflow/cd238b41-ca19-4750-be0e-8dab90a19925/scratchpad/shots";
const base = "https://app.laba.et";
const b = await chromium.launch();
const page = await b.newPage({ viewport: { width: 1440, height: 900 } });
page.setDefaultNavigationTimeout(60000);
const msgs = [];
page.on("response", r => { if (r.status() >= 400) msgs.push(`${r.status()} ${r.request().method()} ${r.url().replace(base,"")}`); });

async function login() {
  await page.goto(base + "/login", { waitUntil: "networkidle" });
  await page.waitForTimeout(2000);
  await page.locator('input[name="username"]').fill("owner@demo.local");
  await page.locator('input[name="password"]').fill("Demo@2026!");
  await page.getByRole("button", { name: /sign in/i }).click();
  await page.waitForURL(u => !u.pathname.includes("/login"), { timeout: 60000 });
}
async function fillForm(passport) {
  await page.goto(base + "/candidates/new", { waitUntil: "networkidle" });
  await page.waitForSelector('button:has-text("Next")', { timeout: 40000 });
  await page.waitForTimeout(1500);
  await page.getByRole("button", { name: /^next$/i }).click();
  await page.waitForTimeout(1500);
  await page.locator('input[name="firstName"]').fill("Hiwot");
  await page.locator('input[name="lastName"]').fill("Girma");
  await page.locator('input[name="passportNumber"]').fill(passport);
  await page.locator('input[name="dateOfBirth"]').first().fill("1997-06-15");
  // Gender is a Select trigger
  const g = page.locator('button:has-text("Select")').first();
  await g.click(); await page.waitForTimeout(700);
  await page.getByRole("option", { name: /female/i }).click().catch(async () => {
    await page.locator('[role="option"]').first().click().catch(()=>{});
  });
  await page.waitForTimeout(1200);
}

await login();
await fillForm("ET9900123");
await page.screenshot({ path: `${SHOTS}/10-ready-to-save.png` });
const saveNow = page.getByRole("button", { name: /save now/i });
console.log("Save now visible after gender:", await saveNow.isVisible().catch(()=>false));
if (await saveNow.isVisible().catch(()=>false)) {
  await saveNow.click();
  await page.waitForTimeout(6000);
  await page.screenshot({ path: `${SHOTS}/11-save-success.png`, fullPage: true });
  console.log("url:", page.url());
}
console.log("4xx/5xx:\n" + (msgs.join("\n") || "(none)"));
await b.close();
