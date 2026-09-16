export interface NavItem {
  name: string;
  href?: string;
  icon: any;
  claims?: string[];
  children?: NavItem[];
  isSeparator?: boolean;
  isActive?: (pathname: string) => boolean;
  sectionLabel?: string;
}

export const navigation: NavItem[] = [
  // ──── Core ────
  {
    name: "Dashboard",
    href: "/overview",
    icon: require("lucide-react").LayoutDashboard,
    claims: ["candidate.read", "system.admin"],
  },
  {
    name: "Candidates",
    href: "/candidates",
    icon: require("lucide-react").Users,
    claims: ["candidate.read", "system.admin"],
  },
  {
    name: "My Work",
    href: "/my-work",
    icon: require("lucide-react").ListChecks,
    claims: ["candidate.read", "system.admin"],
  },
  {
    // Sat under Finance, which meant every role holding candidate.read — a data-entry clerk, a
    // field agent — was shown a "Finance" heading with this as its only entry. It asks for
    // candidate.read, the same as the three above, so this is where it belongs.
    name: "Compliance",
    href: "/compliance",
    icon: require("lucide-react").ShieldAlert,
    claims: ["candidate.read", "system.admin"],
  },

  // ──── Separator: Workflow ────
  { name: "sep-workflow", isSeparator: true, icon: null, sectionLabel: "Workflow Pipeline" },

  {
    name: "New Contracts",
    href: "/workflow/new-contracts",
    icon: require("lucide-react").FileText,
    claims: ["workflow.view", "system.admin"],
  },
  {
    name: "Embassy",
    href: "/workflow/embassy",
    icon: require("lucide-react").Building,
    claims: ["embassy.read", "system.admin"],
  },
  {
    name: "Case Executive",
    href: "/workflow/case-executive",
    icon: require("lucide-react").Briefcase,
    // Only embassy.case_view — the board's own query requires it, so listing embassy.read here
    // showed Embassy Officers a link that opened straight onto a permission error.
    claims: ["embassy.case_view", "system.admin"],
  },
  {
    name: "LMIS",
    href: "/workflow/lmis",
    icon: require("lucide-react").FileCheck,
    claims: ["lmis.read", "system.admin"],
  },
  {
    name: "Tickets",
    href: "/workflow/tickets",
    icon: require("lucide-react").Plane,
    claims: ["travel.read", "system.admin"],
  },
  {
    name: "Departures",
    href: "/workflow/departures",
    icon: require("lucide-react").Clock,
    claims: ["travel.read", "system.admin"],
  },
  {
    name: "Arrivals",
    href: "/workflow/arrivals",
    icon: require("lucide-react").MapPin,
    claims: ["arrival.read", "system.admin"],
  },
  {
    name: "Exceptions",
    href: "/workflow/exceptions",
    icon: require("lucide-react").AlertTriangle,
    claims: ["arrival.read", "arrival.exception", "system.admin"],
  },
  // ──── Separator: Finance ────
  { name: "sep-finance", isSeparator: true, icon: null, sectionLabel: "Finance" },

  {
    name: "Commissions",
    href: "/workflow/commissions",
    icon: require("lucide-react").Banknote,
    claims: ["commission.read", "system.admin"],
  },
  {
    name: "Accounting",
    href: "/finance/accounting",
    icon: require("lucide-react").Calculator,
    claims: ["accounting.read", "system.admin"],
  },
  {
    name: "Exchange rates",
    href: "/finance/rates",
    icon: require("lucide-react").ArrowLeftRight,
    claims: ["accounting.read", "system.admin"],
  },
  {
    name: "Reports",
    href: "/reports",
    icon: require("lucide-react").BarChart3,
    claims: ["report.view", "system.admin"],
  },
  // ──── Separator: Administration ────
  { name: "sep-admin", isSeparator: true, icon: null, sectionLabel: "Administration" },

  {
    name: "Staff & Users",
    icon: require("lucide-react").UserCog,
    claims: ["users.read", "role.read", "system.admin"],
    children: [
      {
        // /staff is the user-account list, not the staff-profile list, so it is reached under
        // users.read. Under staff.read the office manager was shown the link and then met an
        // empty page: they can read staff profiles, and user accounts are somebody else's desk.
        name: "Staff",
        href: "/staff",
        icon: require("lucide-react").IdCard,
        claims: ["users.read", "system.admin"],
      },
      {
        name: "Roles & Permissions",
        href: "/roles",
        icon: require("lucide-react").Shield,
        claims: ["role.read", "system.admin"],
      },
    ],
  },
  {
    name: "Partners",
    href: "/partners",
    icon: require("lucide-react").Handshake,
    claims: ["partner.read", "system.admin"],
  },
  {
    name: "Partner catalog",
    href: "/admin/partners",
    icon: require("lucide-react").Library,
    claims: ["system.admin"],
  },
  {
    name: "Workflow Config",
    href: "/admin/workflow",
    icon: require("lucide-react").Workflow,
    claims: ["workflow.configure", "system.admin"],
  },
  {
    name: "Bot & Notifications",
    href: "/admin/bot",
    icon: require("lucide-react").BellRing,
    claims: ["bot.configure", "system.admin"],
  },
  {
    name: "Errors",
    href: "/admin/errors",
    icon: require("lucide-react").AlertOctagon,
    claims: ["system.admin"],
  },
  {
    name: "Subscriptions",
    href: "/subscriptions",
    icon: require("lucide-react").Receipt,
    claims: ["system.admin"],
  },
  {
    // /api/tenants is behind the SuperAdmin policy, not a permission, so tenant.manage only ever
    // produced a link whose every request failed.
    name: "Tenants",
    href: "/tenants",
    icon: require("lucide-react").Server,
    claims: ["system.admin"],
  },
  {
    name: "Settings",
    href: "/settings",
    icon: require("lucide-react").Settings,
    // Anyone who can link their own Telegram account needs to reach Settings; the page
    // itself still gates the admin-only sections on system.admin. settings.read belongs here
    // too — it is the permission the page is actually named after.
    claims: ["system.admin", "bot.use", "settings.read"],
  },
];

/**
 * Remove section headings with nothing left underneath them.
 *
 * The navigation is a flat list in which a heading is just an item carrying a sectionLabel, so
 * filtering the links out by permission left the heading behind: a field agent saw "FINANCE" with
 * no finance under it, and the sidebar read like a menu of things that had failed to load rather
 * than one shaped to the job.
 *
 * A heading earns its place only if a real item follows it before the next heading. Applied after
 * filtering, this also collapses two headings left adjacent by an entirely removed section, and
 * drops a heading stranded at the very end.
 */
function dropEmptySections(list: NavItem[]): NavItem[] {
  return list.filter((item, index) => {
    if (!item.isSeparator) return true;
    const next = list[index + 1];
    return next !== undefined && !next.isSeparator;
  });
}

export function filterNavigationByClaims(
  items: NavItem[],
  claims: string[],
  isSuperAdmin: boolean,
): NavItem[] {
  if (isSuperAdmin) return items;
  const hasAnyClaim = (required?: string[]) =>
    !required?.length || required.some((c) => claims.includes(c));
  const recur = (list: NavItem[]): NavItem[] =>
    dropEmptySections(
      list
        .map((item) => {
          if (item.isSeparator) return item;
          const children = item.children ? recur(item.children) : undefined;
          const allowed =
            hasAnyClaim(item.claims) || (children && children.length > 0);
          return allowed ? { ...item, children } : null;
        })
        .filter(Boolean) as NavItem[],
    );
  return recur(items);
}

/**
 * The first page this user is allowed to open.
 *
 * Sign-in used to go to the dashboard unconditionally, which needs candidate.read — so a role
 * without it (platform administration, notifications) landed on "Access denied" immediately
 * after a successful login, with only a button back to the same denial.
 */
export function firstPermittedRoute(claims: string[], isSuperAdmin: boolean): string {
  if (isSuperAdmin) return "/overview";
  const walk = (items: NavItem[]): string | null => {
    for (const item of items) {
      if (item.isSeparator) continue;
      if (item.children) {
        const child = walk(item.children);
        if (child) return child;
      }
      if (!item.href) continue;
      if (!item.claims?.length || item.claims.some((c) => claims.includes(c))) return item.href;
    }
    return null;
  };
  return walk(navigation) ?? "/settings";
}
