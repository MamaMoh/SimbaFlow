import { test, expect, type Page } from "@playwright/test";
import { login, ensureLoggedOut } from "./helpers/auth";

/**
 * One real sign-in per role.
 *
 * These assertions are deliberately exact rather than "contains": a role showing one item too
 * many is the bug this suite exists to catch, and a "contains" check would sail past it. The
 * expected lists come from the role-permission map crossed with the navigation claims — if you
 * change either, one of these fails and tells you which role you moved.
 */
const PASSWORD = process.env.ROLE_USER_PASSWORD || "SimbaRole@2026!";

interface RoleCase {
  user: string;
  label: string;
  /** Exactly the sidebar entries this role should see, in order. */
  nav: string[];
  /** A page this role must not be able to open. */
  denied: string;
}

const ROLES: RoleCase[] = [
  {
    user: "owner.amir", label: "AgencyOwner", denied: "/tenants",
    nav: ["Dashboard", "Candidates", "My Work", "New Contracts", "Embassy", "Case Executive",
      "LMIS", "Tickets", "Departures", "Arrivals", "Exceptions", "Commissions", "Accounting",
      "Exchange rates", "Reports", "Compliance", "Staff & Users", "Partners", "Workflow Config",
      "Bot & Notifications", "Settings"],
  },
  {
    user: "mgr.hana", label: "OfficeManager", denied: "/admin/workflow",
    nav: ["Dashboard", "Candidates", "My Work", "New Contracts", "Embassy", "Case Executive",
      "LMIS", "Tickets", "Departures", "Arrivals", "Exceptions", "Commissions", "Accounting",
      "Exchange rates", "Reports", "Compliance", "Staff & Users", "Partners", "Settings"],
  },
  {
    // The Embassy Officer used to be shown Case Executive and get a permission error on it.
    user: "embassy.dawit", label: "EmbassyOfficer", denied: "/workflow/case-executive",
    nav: ["Dashboard", "Candidates", "My Work", "New Contracts", "Embassy", "LMIS", "Compliance"],
  },
  {
    user: "case.sara", label: "CaseExecutive", denied: "/finance/accounting",
    nav: ["Dashboard", "Candidates", "My Work", "New Contracts", "Case Executive", "Compliance"],
  },
  {
    user: "fin.yonas", label: "FinanceOfficer", denied: "/workflow/embassy",
    nav: ["Dashboard", "Candidates", "My Work", "Commissions", "Accounting", "Exchange rates",
      "Reports", "Compliance"],
  },
  {
    user: "field.kebede", label: "FieldAgent", denied: "/finance/accounting",
    nav: ["Dashboard", "Candidates", "My Work", "New Contracts", "Embassy", "Arrivals",
      "Exceptions", "Compliance", "Settings"],
  },
  {
    user: "clerk.tigist", label: "DataEntryClerk", denied: "/workflow/embassy",
    nav: ["Dashboard", "Candidates", "My Work", "New Contracts", "Compliance"],
  },
  {
    user: "audit.abebe", label: "Auditor", denied: "/staff",
    nav: ["Dashboard", "Candidates", "My Work", "New Contracts", "Embassy", "LMIS", "Tickets",
      "Departures", "Arrivals", "Exceptions", "Commissions", "Accounting", "Exchange rates",
      "Reports", "Compliance"],
  },
  {
    // Two items only — and crucially it must be able to land on one of them.
    user: "notify.selam", label: "NotificationManager", denied: "/candidates",
    nav: ["Bot & Notifications", "Settings"],
  },
  {
    user: "platform.rediet", label: "PlatformAdmin", denied: "/candidates",
    nav: ["Staff & Users", "Roles & Permissions", "Bot & Notifications", "Settings"],
  },
];

/** Top-level sidebar links, with collapsible groups expanded. */
async function sidebarItems(page: Page): Promise<string[]> {
  const nav = page.locator("nav").first();
  await nav.waitFor({ state: "visible", timeout: 20_000 });
  const raw = await nav.locator("a, button").allInnerTexts();
  return raw
    .map((t) => t.replace(/\s+/g, " ").trim())
    .filter((t) => t.length > 0 && t.length < 40);
}

for (const role of ROLES) {
  test(`${role.label} sees only its own navigation`, async ({ page }) => {
    await ensureLoggedOut(page);
    await login(page, { username: role.user, password: PASSWORD });

    // A successful sign-in must not land on a denial. This is the regression that made the
    // Notification Manager unusable: correct password, then "Access denied" with no way out.
    await expect(page.getByText(/access denied/i)).toHaveCount(0);

    const items = await sidebarItems(page);
    for (const expected of role.nav) {
      expect(items, `${role.label} should see "${expected}"`).toContain(expected);
    }

    // Nothing beyond what the role is entitled to. "Staff" and "Roles & Permissions" are
    // children of "Staff & Users" and only appear once that group is open, so they are allowed
    // extras rather than required ones.
    const allowed = new Set([...role.nav, "Staff", "Roles & Permissions", "Partner catalog"]);
    const known = new Set(ROLES.flatMap((r) => r.nav).concat(["Tenants", "Errors", "Partner catalog"]));
    const surplus = items.filter((i) => known.has(i) && !allowed.has(i));
    expect(surplus, `${role.label} should not see these`).toEqual([]);
  });

  test(`${role.label} is refused ${role.denied}`, async ({ page }) => {
    await ensureLoggedOut(page);
    await login(page, { username: role.user, password: PASSWORD });
    await page.goto(role.denied);

    await expect(
      page.getByText(/access denied|not authorized|forbidden|do not have permission/i).first()
    ).toBeVisible({ timeout: 20_000 });

    // The way out must lead somewhere this role can actually open, not back to a second denial.
    const escape = page.getByRole("link", { name: /go to your work/i }).first();
    if (await escape.isVisible().catch(() => false)) {
      await escape.click();
      await expect(page.getByText(/access denied/i)).toHaveCount(0);
    }
  });
}
