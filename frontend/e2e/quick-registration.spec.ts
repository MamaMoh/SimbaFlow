import { test, expect } from "@playwright/test";
import { CREDENTIALS, login } from "./helpers/auth";

/**
 * Registration is a single page. Field staff fill the five essential fields and save; everything
 * else is optional and can be added later. Nothing is hidden behind a step, so the whole form has
 * to be reachable and saveable in one pass.
 */
test.describe("Candidate registration", () => {
  test("every section is on the page at once", async ({ page }) => {
    await login(page, CREDENTIALS.agencyOwner);
    await page.goto("/candidates/register");
    await expect(page.getByRole("heading", { name: /new application/i })).toBeVisible();

    // A field from the first section and one from the last, both present without navigating.
    await expect(page.getByRole("heading", { name: /passport scan/i })).toBeVisible();
    await expect(page.locator('input[name="firstName"]')).toBeVisible();
    await expect(page.getByRole("heading", { name: /sponsor & visa/i })).toBeVisible();

    // No stepper controls survive.
    await expect(page.getByRole("button", { name: /^next$/i })).toHaveCount(0);
    await expect(page.getByRole("button", { name: /^back$/i })).toHaveCount(0);
  });

  test("each group of fields has its own heading", async ({ page }) => {
    await login(page, CREDENTIALS.agencyOwner);
    await page.goto("/candidates/register");
    await expect(page.locator("#documents")).toBeVisible();

    // A long single page still has to read as sections.
    for (const name of [/^documents$/i, /^identity$/i, /^family$/i, /^experience$/i, /^placement$/i]) {
      await expect(page.getByRole("heading", { name })).toBeVisible();
    }
  });

  test("filling only the essentials creates the candidate", async ({ page }) => {
    await login(page, CREDENTIALS.agencyOwner);
    await page.goto("/candidates/register");
    await page.waitForTimeout(1500);

    const stamp = Date.now().toString().slice(-6);
    await page.locator('input[name="firstName"]').fill("E2E");
    await page.locator('input[name="lastName"]').fill("OnePage");
    await page.locator('input[name="passportNumber"]').fill("EP" + stamp);
    await page.locator('input[name="dateOfBirth"]').fill("1998-05-04");

    // Gender (shadcn Select)
    const trigger = page
      .locator('button[role="combobox"]')
      .filter({ hasText: /select/i })
      .first();
    await trigger.click();
    await page.waitForTimeout(400);
    await page.getByRole("option").first().click();
    await page.waitForTimeout(600);

    const save = page.getByRole("button", { name: /save application/i });
    await expect(save).toBeVisible({ timeout: 10_000 });

    const [response] = await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().includes("/api/proxy/candidates") &&
          r.request().method() === "POST",
        { timeout: 20_000 },
      ),
      save.click(),
    ]);
    expect(response.status()).toBe(201);
  });
});
