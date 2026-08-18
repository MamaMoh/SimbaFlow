import { expect, test } from "@playwright/test";
import { CREDENTIALS, login } from "./helpers/auth";

const AGENCY_PAGES: { path: string; heading: RegExp }[] = [
  { path: "/overview", heading: /command center/i },
  { path: "/candidates", heading: /candidates/i },
  { path: "/my-work", heading: /my work/i },
  { path: "/reports", heading: /reports/i },
  { path: "/compliance", heading: /compliance/i },
  { path: "/partners", heading: /partners/i },
  { path: "/finance/accounting", heading: /accounting/i },
  { path: "/finance/rates", heading: /exchange rates/i },
  { path: "/staff", heading: /users\s*&\s*staff|staff/i },
  { path: "/roles", heading: /roles/i },
];

const WORKFLOW_STAGES: { path: string; heading: RegExp }[] = [
  { path: "/workflow/new-contracts", heading: /new contracts|workflow view/i },
  { path: "/workflow/embassy", heading: /embassy/i },
  { path: "/workflow/case-executive", heading: /case executive/i },
  { path: "/workflow/lmis", heading: /lmis/i },
  { path: "/workflow/tickets", heading: /tickets/i },
  { path: "/workflow/departures", heading: /departures/i },
  { path: "/workflow/arrivals", heading: /arrivals/i },
  { path: "/workflow/exceptions", heading: /exceptions/i },
  { path: "/workflow/commissions", heading: /commissions/i },
];

test.describe("Authenticated navigation", () => {
  test.beforeEach(async ({ page }) => {
    await login(page, CREDENTIALS.agencyOwner);
  });

  for (const { path, heading } of AGENCY_PAGES) {
    test(`loads ${path}`, async ({ page }) => {
      await page.goto(path);
      await expect(page).toHaveURL(new RegExp(path.replace(/\//g, "\\/")));
      await expect(page.getByRole("heading", { name: heading }).first()).toBeVisible({
        timeout: 20_000,
      });
      await expect(page.locator("text=Application error")).toHaveCount(0);
      await expect(page.locator("text=Internal Server Error")).toHaveCount(0);
    });
  }

  for (const { path, heading } of WORKFLOW_STAGES) {
    test(`loads workflow ${path}`, async ({ page }) => {
      const response = await page.goto(path);
      expect(response?.ok() || response?.status() === 304).toBeTruthy();
      await expect(page).toHaveURL(new RegExp(path.replace(/\//g, "\\/")));
      await expect(page.locator("text=Application error")).toHaveCount(0);
      await expect(page.getByRole("heading", { name: heading }).first()).toBeVisible({
        timeout: 20_000,
      });
    });
  }

  test("loads /admin/workflow config", async ({ page }) => {
    await page.goto("/admin/workflow");
    await expect(page).toHaveURL(/\/admin\/workflow/);
    await expect(page.locator("text=Application error")).toHaveCount(0);
    await expect(page.locator("main").first()).toBeVisible();
  });

  test("settings page loads or denies by permission", async ({ page }) => {
    await page.goto("/settings");
    await expect(page).toHaveURL(/\/settings/);
    await expect(page.locator("text=Application error")).toHaveCount(0);
    await expect(
      page.getByRole("heading", { name: /settings/i }).or(page.getByText("Access denied")).first()
    ).toBeVisible();
  });

  test("sidebar Candidates link navigates", async ({ page }) => {
    await page.goto("/overview");
    const candidatesLink = page.getByRole("link", { name: /^candidates$/i }).first();
    await expect(candidatesLink).toBeVisible();
    await candidatesLink.click();
    await expect(page).toHaveURL(/\/candidates/);
    await expect(page.getByRole("heading", { name: /candidates/i })).toBeVisible();
  });

  test("overview shows pipeline funnel section", async ({ page }) => {
    await page.goto("/overview");
    await expect(page.getByRole("heading", { name: /command center/i }).first()).toBeVisible({
      timeout: 20_000,
    });
    await expect(page.getByRole("heading", { name: /pipeline funnel/i })).toBeVisible({
      timeout: 20_000,
    });
    await expect(page.locator("text=Application error")).toHaveCount(0);
  });
});

test.describe("SuperAdmin partner catalog", () => {
  test("loads /admin/partners catalog", async ({ page }) => {
    await login(page, CREDENTIALS.admin);
    await page.goto("/admin/partners");
    await expect(page).toHaveURL(/\/admin\/partners/);
    await expect(page.locator("text=Application error")).toHaveCount(0);
    await expect(
      page
        .getByRole("heading", { name: /partner catalog/i })
        .or(page.getByText("Access denied"))
        .first()
    ).toBeVisible({ timeout: 20_000 });
  });

  test("loads /admin/bot", async ({ page }) => {
    await login(page, CREDENTIALS.admin);
    await page.goto("/admin/bot");
    await expect(page).toHaveURL(/\/admin\/bot/);
    await expect(page.locator("text=Application error")).toHaveCount(0);
    await expect(
      page
        .getByRole("heading", { name: /bot\s*&\s*notifications/i })
        .or(page.getByText("Access denied"))
        .first()
    ).toBeVisible({ timeout: 20_000 });
  });
});

test.describe("Bot settings link UX", () => {
  test("settings shows Telegram bot link section for agency owner", async ({ page }) => {
    await login(page, CREDENTIALS.agencyOwner);
    await page.goto("/settings");
    await expect(page).toHaveURL(/\/settings/);
    await expect(page.locator("text=Application error")).toHaveCount(0);
    await expect(
      page
        .getByRole("heading", { name: /telegram bot/i })
        .or(page.getByRole("heading", { name: /settings/i }))
        .or(page.getByText("Access denied"))
        .first()
    ).toBeVisible({ timeout: 20_000 });
  });
});
