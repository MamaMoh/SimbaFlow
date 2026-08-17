import { expect, test } from "@playwright/test";
import { CREDENTIALS, login } from "./helpers/auth";

test.describe("Reporting & analytics (Unit 8)", () => {
  test.beforeEach(async ({ page }) => {
    await login(page, CREDENTIALS.agencyOwner);
  });

  test("command center renders KPI tiles and trend chart", async ({ page }) => {
    await page.goto("/overview");
    await expect(
      page.getByRole("heading", { name: /command center/i })
    ).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText(/active candidates/i).first()).toBeVisible();
    await expect(page.getByText(/intake & outcomes/i)).toBeVisible();
    await expect(page.locator("text=Application error")).toHaveCount(0);
  });

  test("reports page lists catalog and shows a report with export buttons", async ({
    page,
  }) => {
    await page.goto("/reports");
    await expect(
      page.getByRole("heading", { name: /^reports$/i })
    ).toBeVisible({ timeout: 20_000 });
    // Catalog item
    await expect(page.getByText(/pipeline summary/i).first()).toBeVisible();
    // Export controls (agency owner has report.export)
    await expect(
      page.getByRole("button", { name: /excel/i }).first()
    ).toBeVisible({ timeout: 20_000 });
    await expect(page.getByRole("button", { name: /pdf/i }).first()).toBeVisible();
    await expect(page.locator("text=Application error")).toHaveCount(0);
  });

  test("compliance center renders bucket tiles", async ({ page }) => {
    await page.goto("/compliance");
    await expect(
      page.getByRole("heading", { name: /compliance/i })
    ).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText(/expired/i).first()).toBeVisible();
    await expect(page.locator("text=Application error")).toHaveCount(0);
  });

  test("my work page renders action buckets", async ({ page }) => {
    await page.goto("/my-work");
    await expect(
      page.getByRole("heading", { name: /my work/i })
    ).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText(/overdue candidates/i).first()).toBeVisible();
    await expect(page.locator("text=Application error")).toHaveCount(0);
  });

  test("command palette opens with ⌘K and navigates", async ({ page }) => {
    await page.goto("/overview");
    await page.getByRole("button", { name: /search and navigate/i }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog).toBeVisible();
    await page.getByPlaceholder(/search candidates, pages/i).fill("compliance");
    await page.getByRole("option", { name: /compliance/i }).first().click();
    await expect(page).toHaveURL(/\/compliance/);
  });
});
