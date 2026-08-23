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
    claims: ["embassy.case_view", "embassy.read", "system.admin"],
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
  {
    name: "Commissions",
    href: "/workflow/commissions",
    icon: require("lucide-react").Banknote,
    claims: ["commission.read", "system.admin"],
  },

  // ──── Separator: Finance ────
  { name: "sep-finance", isSeparator: true, icon: null, sectionLabel: "Finance" },

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
  {
    name: "Compliance",
    href: "/compliance",
    icon: require("lucide-react").ShieldAlert,
    claims: ["candidate.read", "system.admin"],
  },

  // ──── Separator: Administration ────
  { name: "sep-admin", isSeparator: true, icon: null, sectionLabel: "Administration" },

  {
    name: "Staff & Users",
    icon: require("lucide-react").UserCog,
    claims: ["staff.read", "system.admin"],
    children: [
      {
        name: "Staff",
        href: "/staff",
        icon: require("lucide-react").IdCard,
        claims: ["staff.read", "system.admin"],
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
    name: "Tenants",
    href: "/tenants",
    icon: require("lucide-react").Server,
    claims: ["tenant.manage", "system.admin"],
  },
  {
    name: "Settings",
    href: "/settings",
    icon: require("lucide-react").Settings,
    // Anyone who can link their own Telegram account needs to reach Settings; the page
    // itself still gates the admin-only sections on system.admin.
    claims: ["system.admin", "bot.use"],
  },
];

export function filterNavigationByClaims(
  items: NavItem[],
  claims: string[],
  isSuperAdmin: boolean,
): NavItem[] {
  if (isSuperAdmin) return items;
  const hasAnyClaim = (required?: string[]) =>
    !required?.length || required.some((c) => claims.includes(c));
  const recur = (list: NavItem[]): NavItem[] =>
    list
      .map((item) => {
        if (item.isSeparator) return item;
        const children = item.children ? recur(item.children) : undefined;
        const allowed =
          hasAnyClaim(item.claims) || (children && children.length > 0);
        return allowed ? { ...item, children } : null;
      })
      .filter(Boolean) as NavItem[];
  return recur(items);
}
