import path from "node:path";
import { expect, test } from "@playwright/test";
import { CREDENTIALS, login } from "./helpers/auth";

const shotDir = path.join(__dirname, "../test-results/screenshots");

test.describe("UI screenshots", () => {
  test("capture key screens", async ({ page }) => {
    test.setTimeout(180_000);
    await page.setViewportSize({ width: 1440, height: 900 });

    // Login page
    await page.goto("/login");
    await expect(page.getByRole("heading", { name: "SimbaFlow" })).toBeVisible();
    await page.screenshot({ path: path.join(shotDir, "01-login.png"), fullPage: true });

    // Dashboard
    await login(page, CREDENTIALS.agencyOwner);
    await expect(page.getByRole("heading", { name: /command center/i })).toBeVisible();
    await page.screenshot({ path: path.join(shotDir, "02-dashboard.png"), fullPage: true });

    // Candidates list
    await page.goto("/candidates");
    await expect(page.getByRole("heading", { name: /candidates/i })).toBeVisible();
    await page.screenshot({ path: path.join(shotDir, "03-candidates.png"), fullPage: true });

    // New Application stepped page (Documents)
    await page.getByRole("button", { name: /\+?\s*create/i }).click({ force: true });
    await expect(page).toHaveURL(/\/candidates\/new/);
    await expect(page.getByRole("heading", { name: /new application/i })).toBeVisible();
    await expect(page.getByRole("heading", { name: /passport scan/i })).toBeVisible();
    await page.screenshot({ path: path.join(shotDir, "04-new-application.png"), fullPage: true });

    // Passport OCR filled — then Identity step
    const fixture = path.join(__dirname, "fixtures/eth-passport.jpg");
    await page.locator('input[type="file"]').first().setInputFiles(fixture);
    const scanBtn = page.getByRole("button", { name: /scan passport|scanning/i });
    await expect(scanBtn).toBeVisible({ timeout: 10_000 });
    const label = (await scanBtn.innerText()).toLowerCase();
    if (label.includes("scan passport") && (await scanBtn.isEnabled())) {
      await scanBtn.click();
    }
    await expect
      .poll(async () => page.locator('input[name="passportNumber"]').inputValue(), {
        timeout: 120_000,
      })
      .toMatch(/EP8273953/i);
    await page.getByRole("button", { name: /^next$/i }).click();
    await expect(page.locator('input[name="firstName"]')).toBeVisible();
    await page.screenshot({ path: path.join(shotDir, "05-passport-ocr-filled.png"), fullPage: true });

    // Workflow board
    await page.goto("/workflow/embassy");
    await expect(page.getByRole("heading", { name: /embassy/i })).toBeVisible();
    await page.screenshot({ path: path.join(shotDir, "06-workflow-embassy.png"), fullPage: true });

    await page.goto("/reports");
    await expect(page.getByRole("heading", { name: /reports/i })).toBeVisible();
    await page.screenshot({ path: path.join(shotDir, "07-reports.png"), fullPage: true });
  });
});
