import { chromium } from "@playwright/test";
const SHOTS = "/private/tmp/claude-501/-Users-mama-Dev-simbaflow/cd238b41-ca19-4750-be0e-8dab90a19925/scratchpad/shots";
const base = "https://app.laba.et";
const b = await chromium.launch();
const page = await b.newPage({ viewport: { width: 1440, height: 900 } });
page.setDefaultNavigationTimeout(60000);
const msgs = [];
page.on("pageerror", e => msgs.push("PAGEERROR: " + e.message));
page.on("console", m => { if (m.type()==="error") msgs.push("console: " + m.text().slice(0,180)); });
page.on("response", r => { if (r.status() >= 400) msgs.push(`${r.status()} ${r.request().method()} ${r.url().replace(base,"")}`); });

await page.goto(base + "/login", { waitUntil: "networkidle" });
await page.waitForTimeout(2000);
await page.locator('input[name="username"]').fill("owner@demo.local");
await page.locator('input[name="password"]').fill("Demo@2026!");
await page.getByRole("button", { name: /sign in/i }).click();
await page.waitForURL(u => !u.pathname.includes("/login"), { timeout: 60000 });
console.log("landed on:", page.url());
msgs.length = 0;

// give the dashboard a long time
await page.waitForTimeout(9000);
await page.screenshot({ path: `${SHOTS}/04-dashboard-waited.png`, fullPage: true });
const bodyLen = (await page.locator("main").innerText().catch(()=>"" )).trim().length;
console.log("main text length:", bodyLen);
console.log("h1/h2 on page:", await page.locator("h1,h2").allInnerTexts().catch(()=>[]));
console.log("\nmessages:\n" + (msgs.join("\n") || "(none)"));
await b.close();
