import { test, expect } from "@playwright/test";
import { CREDENTIALS, login } from "./helpers/auth";

/**
 * A Select renders its list in a separate portal, outside the Dialog or Sheet containing it. Radix
 * therefore reads a click on an option as an interaction *outside*, and dismissed the whole form —
 * losing the value with it. Every status form on the boards is a dialog containing a select, so
 * the behaviour is checked rather than assumed.
 *
 * Driven through the workflow config rather than a board: which forms a board row offers depends
 * on how far that candidate has got, and earlier tests in this suite move candidates along. The
 * mirror editor depends only on the workflow definition, so it is here whatever the data says —
 * and being a Sheet, it covers the other half of the fix.
 *
 * Queried by data-slot, not role: while a select is open Radix marks the surrounding dialog
 * aria-hidden, so a role-based query reports it missing even though it is on screen.
 */
test("picking a select option keeps the form open and the value set", async ({ page }) => {
  test.setTimeout(120_000);
  await login(page, CREDENTIALS.admin);
  await page.goto("/admin/workflow");
  await page.waitForTimeout(3000);

  await page.getByRole("button", { name: /new mirror/i }).click();

  const form = page.locator('[data-slot="sheet-content"]');
  await expect(form).toBeVisible();

  const combo = form.locator('button[role="combobox"]').first();
  await expect(combo).toBeVisible();
  await combo.click();
  await page.waitForTimeout(400);

  const option = page.locator('[data-slot="select-item"]').first();
  const chosen = (await option.innerText()).trim();
  await option.click();
  await page.waitForTimeout(500);

  // The form must survive the pick, and the pick must land.
  await expect(form).toBeVisible();
  await expect(combo).toContainText(chosen);
});
