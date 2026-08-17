import { expect, test } from "@playwright/test";
import { CREDENTIALS, ensureLoggedOut, login } from "./helpers/auth";

test.describe("Login", () => {
  test.beforeEach(async ({ page }) => {
    await ensureLoggedOut(page);
  });

  test("shows login page branding and form", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByRole("heading", { name: "SimbaFlow" })).toBeVisible();
    await expect(page.getByText("Labour Export Agency")).toBeVisible();
    await expect(page.locator("#username")).toBeVisible();
    await expect(page.locator("#password")).toBeVisible();
    await expect(page.getByRole("button", { name: "Sign In" })).toBeVisible();
  });

  test("rejects invalid credentials", async ({ page }) => {
    await page.goto("/login");
    await page.locator("#username").fill("nobody@example.com");
    await page.locator("#password").fill("WrongPass1!");
    await page.getByRole("button", { name: "Sign In" }).click();
    await expect(page.getByText(/invalid username or password/i)).toBeVisible({
      timeout: 20_000,
    });
    await expect(page).toHaveURL(/\/login/);
  });

  test("logs in agency owner and lands on dashboard", async ({ page }) => {
    await login(page, CREDENTIALS.agencyOwner);
    await expect(page.getByRole("heading", { name: /command center/i })).toBeVisible();
  });

  test("logs in platform admin", async ({ page }) => {
    await login(page, CREDENTIALS.admin);
    await expect(page).toHaveURL(/\/overview/);
  });
});
