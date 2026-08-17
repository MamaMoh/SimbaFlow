import { test, expect } from "@playwright/test";
import { CREDENTIALS, login } from "./helpers/auth";

/**
 * Registration must stay short for non-technical field staff: once the five
 * essential fields are filled, "Save now" appears and creates the candidate
 * without walking through the optional steps.
 */
test.describe("Quick candidate registration", () => {
  test("Save now appears once essentials are filled and creates the candidate", async ({
    page,
  }) => {
    await login(page, CREDENTIALS.agencyOwner);
    await page.goto("/candidates/register");
    await page.waitForTimeout(1500);

    // Step 1 (documents) → Step 2 (identity)
    await page.getByRole("button", { name: /^Next$/ }).click();
    await page.waitForTimeout(1200);

    // Save now must NOT be offered before the essentials are complete
    await expect(page.getByRole("button", { name: /save now/i })).toHaveCount(0);

    const stamp = Date.now().toString().slice(-6);
    await page.locator('input[name="firstName"]').fill("E2E");
    await page.locator('input[name="lastName"]').fill("QuickSave");
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

    const saveNow = page.getByRole("button", { name: /save now/i });
    await expect(saveNow).toBeVisible({ timeout: 10_000 });

    const [response] = await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().includes("/api/proxy/candidates") &&
          r.request().method() === "POST",
        { timeout: 20_000 },
      ),
      saveNow.click(),
    ]);
    expect(response.status()).toBe(201);
  });
});
