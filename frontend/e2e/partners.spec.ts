import { test, expect } from "@playwright/test";
import path from "path";
import { login } from "./helpers/auth";

const dir = path.join(__dirname, "../test-results/screenshots");

// Demo Agency is the tenant that has partner links + candidates seeded.
const DEMO_OWNER = { username: "owner@demo.agency", password: "Agency@123!" };

test.describe("Partner management", () => {
  test("partners list shows agreement state and capacity", async ({ page }) => {
    await login(page, DEMO_OWNER);
    await page.goto("/partners");
    await expect(page.getByRole("heading", { name: /^partners$/i })).toBeVisible({ timeout: 20_000 });
    // capacity strip
    await expect(page.getByText(/partner capacity/i)).toBeVisible({ timeout: 20_000 });
    // agreement chips (expired agreement must be surfaced here)
    await expect(page.getByText(/expired \d+ day/i).first()).toBeVisible({ timeout: 20_000 });
    await page.screenshot({ path: path.join(dir, "partners-list.png"), fullPage: true });
  });

  test("partner detail shows candidates and billing", async ({ page }) => {
    await login(page, DEMO_OWNER);
    await page.goto("/partners");
    await page.waitForTimeout(2500);
    await page.locator('a[href^="/partners/"]').first().click();
    await page.waitForTimeout(2500);
    await expect(page.getByRole("tab", { name: /candidates/i })).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText(/outstanding/i).first()).toBeVisible();
    await page.screenshot({ path: path.join(dir, "partner-detail.png"), fullPage: true });
    await page.getByRole("tab", { name: /billing/i }).click();
    await page.waitForTimeout(1200);
    await page.screenshot({ path: path.join(dir, "partner-billing.png"), fullPage: true });
    await expect(page.locator("text=Application error")).toHaveCount(0);
  });
});
