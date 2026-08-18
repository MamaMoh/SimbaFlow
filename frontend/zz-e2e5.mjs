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

await page.goto(base + "/candidates/new", { waitUntil: "networkidle" });
await page.waitForSelector('button:has-text("Next")', { timeout: 40000 });
await page.waitForTimeout(2000);
await page.getByRole("button", { name: /^next$/i }).click();   // to Identity
await page.waitForTimeout(1500);

await page.locator('input[name="firstName"]').fill("Hiwot");
await page.locator('input[name="lastName"]').fill("Girma");
await page.locator('input[name="passportNumber"]').fill("ET9900123");
// date of birth + gender
const dob = page.locator('input[name="dateOfBirth"]');
if (await dob.count()) await dob.first().fill("1997-06-15");
await page.screenshot({ path: `${SHOTS}/08-identity-filled.png`, fullPage: true });

// look for a Save now button (the quick-save path)
const saveNow = page.getByRole("button", { name: /save now/i });
console.log("Save now visible:", await saveNow.isVisible().catch(()=>false));
if (await saveNow.isVisible().catch(()=>false)) {
  await saveNow.click();
  await page.waitForTimeout(5000);
  await page.screenshot({ path: `${SHOTS}/09-after-save.png`, fullPage: true });
  console.log("url after save:", page.url());
  const body = await page.locator("body").innerText();
  const m = body.match(/(saved|created|success|error|failed|required|Gender)[^\n]{0,90}/gi);
  console.log("page signals:", (m||[]).slice(0,6));
}
console.log("messages:\n" + (msgs.join("\n") || "(none)"));
await b.close();
