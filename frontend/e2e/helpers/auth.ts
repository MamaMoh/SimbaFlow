import { expect, type Page } from "@playwright/test";

export const CREDENTIALS = {
  admin: { username: "admin", password: "Admin@123!" },
  agencyOwner: { username: "tadesse@ethiostar.et", password: "Agency@123!" },
  demoOwner: { username: "owner@demo.agency", password: "Agency@123!" },
} as const;

export async function login(
  page: Page,
  creds: { username: string; password: string } = CREDENTIALS.agencyOwner
) {
  await page.goto("/login");
  await expect(page.getByRole("heading", { name: "SimbaFlow" })).toBeVisible();
  await page.locator("#username").fill(creds.username);
  await page.locator("#password").fill(creds.password);
  await page.getByRole("button", { name: "Sign In" }).click();

  // Land on whichever page this role is allowed to open — not everyone is permitted on the
  // dashboard — or get bounced to forced password change.
  await Promise.race([
    page.waitForURL((url) => !url.pathname.startsWith("/login"), { timeout: 30_000 }),
    page.getByText(/invalid username or password/i).waitFor({ timeout: 30_000 }),
  ]);

  if (page.url().includes("/change-password")) {
    throw new Error(`Login requires password change for ${creds.username}`);
  }
  if (await page.getByText(/invalid username or password/i).isVisible().catch(() => false)) {
    throw new Error(`Login failed for ${creds.username}`);
  }

  await expect(page).not.toHaveURL(/\/login/);
}

export async function ensureLoggedOut(page: Page) {
  await page.goto("/login");
  await page.evaluate(() => {
    try {
      localStorage.clear();
      sessionStorage.clear();
    } catch {
      /* ignore */
    }
  });
  await page.context().clearCookies();
  await page.goto("/login");
}
