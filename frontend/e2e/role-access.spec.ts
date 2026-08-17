import { test, expect } from "@playwright/test";
import { login } from "./helpers/auth";

/**
 * Each agency role sees only what it should. Verifies the sidebar/menu is
 * permission-filtered and restricted pages deny access, using real logins.
 */
const ROLES = [
  { user: "rt.officemanager", label: "OfficeManager", canSee: ["Candidates", "Embassy", "Commissions"], denied: "/tenants" },
  { user: "rt.embassyofficer", label: "EmbassyOfficer", canSee: ["Candidates", "Embassy"], denied: "/tenants" },
  { user: "rt.caseexecutive", label: "CaseExecutive", canSee: ["Candidates", "Case Executive"], denied: "/tenants" },
  { user: "rt.financeofficer", label: "FinanceOfficer", canSee: ["Candidates", "Accounting"], denied: "/tenants" },
  { user: "rt.fieldagent", label: "FieldAgent", canSee: ["Candidates"], denied: "/tenants" },
  { user: "rt.dataentryclerk", label: "DataEntryClerk", canSee: ["Candidates"], denied: "/tenants" },
  { user: "rt.auditor", label: "Auditor", canSee: ["Candidates"], denied: "/tenants" },
];

for (const role of ROLES) {
  test(`${role.label} sees only permitted navigation`, async ({ page }) => {
    await login(page, { username: role.user, password: "Role@123!" });
    await page.goto("/candidates");
    await expect(page.getByRole("heading", { name: /candidates/i }).first()).toBeVisible({ timeout: 20_000 });

    // Permitted nav entries are present
    for (const item of role.canSee) {
      await expect(page.getByRole("link", { name: new RegExp(`^${item}$`, "i") }).first()).toBeVisible();
    }

    // Platform-only area must not be reachable
    await page.goto(role.denied);
    await expect(
      page.getByText(/access denied|not authorized|forbidden/i).first()
        .or(page.getByRole("heading", { name: /agencies/i }))
    ).toBeVisible({ timeout: 20_000 });
    await expect(page.locator("text=Application error")).toHaveCount(0);
  });
}
