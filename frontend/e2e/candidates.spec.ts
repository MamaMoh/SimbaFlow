import path from "node:path";
import { expect, test } from "@playwright/test";
import { CREDENTIALS, login } from "./helpers/auth";

const PASSPORT_SAMPLE = path.join(__dirname, "fixtures/eth-passport.jpg");

test.describe("Candidates intake", () => {
  test.beforeEach(async ({ page }) => {
    await login(page, CREDENTIALS.agencyOwner);
  });

  test("candidates list opens the New Application form", async ({ page }) => {
    await page.goto("/candidates");
    await expect(page.getByRole("heading", { name: /candidates/i })).toBeVisible();

    const createBtn = page.getByRole("button", { name: /\+?\s*create/i });
    await expect(createBtn).toBeVisible({ timeout: 20_000 });
    await createBtn.click({ force: true });

    await expect(page).toHaveURL(/\/candidates\/new/);
    await expect(page.getByRole("heading", { name: /new application/i })).toBeVisible();
    await expect(page.getByText("Documents", { exact: true }).first()).toBeVisible();
    await expect(page.getByRole("heading", { name: /passport scan/i })).toBeVisible();
    await expect(page.getByRole("heading", { name: /candidate photos/i })).toBeVisible();

    // Identity fields are on the same page, not behind a step
    await expect(page.locator('input[name="firstName"]')).toBeVisible();
    await expect(page.locator('input[name="passportNumber"]')).toBeVisible();
  });

  test("passport OCR fills biodata from sample image", async ({ page }) => {
    test.setTimeout(180_000);

    await page.goto("/candidates/new");
    await expect(page.getByRole("heading", { name: /new application/i })).toBeVisible();

    const passportHeading = page.getByRole("heading", { name: /passport scan/i });
    await expect(passportHeading).toBeVisible();

    const fileInput = page.locator('input[type="file"]').first();
    await fileInput.setInputFiles(PASSPORT_SAMPLE);

    // Upload may auto-start OCR; otherwise click the scan button
    const scanBtn = page.getByRole("button", { name: /scan passport|scanning/i });
    await expect(scanBtn).toBeVisible({ timeout: 10_000 });
    const label = (await scanBtn.innerText()).toLowerCase();
    if (label.includes("scan passport") && (await scanBtn.isEnabled())) {
      await scanBtn.click();
    }

    // OCR fills the Identity fields, which are on the same page as the scanner
    await expect
      .poll(
        async () => page.locator('input[name="passportNumber"]').inputValue(),
        { timeout: 120_000, intervals: [2_000, 3_000, 5_000] }
      )
      .toMatch(/EP8273953/i);

    await expect(page.locator('input[name="passportNumber"]')).toBeVisible();
    await expect(page.locator('input[name="lastName"]')).toHaveValue(/TESEMA/i);
    await expect(page.locator('input[name="firstName"]')).toHaveValue(/MENEN/i);
    const dob = await page.locator('input[name="dateOfBirth"]').inputValue();
    const issue = await page.locator('input[name="passportIssueDate"]').inputValue();
    if (issue) expect(issue).not.toBe(dob);
    await page.screenshot({
      path: path.join(__dirname, "../test-results/screenshots/08-form-redesign.png"),
      fullPage: true,
    });
  });
});
