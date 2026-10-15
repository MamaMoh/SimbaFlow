import type { WorkflowStage } from "@/types/workflow";
import { stageHref } from "@/lib/workflow/stage-slug";

/**
 * Which of the two jobs a link belongs to.
 *
 * SimbaFlow is sold to agencies, so there are two quite different people in it: the operator of
 * the platform, who deals in agencies, subscriptions and the shared catalogue, and the agency
 * staff, who deal in candidates. A platform administrator was shown both at once — Embassy, LMIS
 * and Tickets sitting in the sidebar above an empty Agencies page, meaning nothing until they
 * picked an agency to work inside.
 *
 * "both" is for the handful that genuinely serve either: the dashboard (whose content differs),
 * user accounts, the bot, and settings.
 */
export type NavScope = "platform" | "agency" | "both";

export interface NavItem {
  name: string;
  href?: string;
  icon: any;
  claims?: string[];
  /** Defaults to "agency": most of this application is the agency's. */
  scope?: NavScope;
  children?: NavItem[];
  isSeparator?: boolean;
  /**
   * The workflow stage this board is built around.
   *
   * These seven boards are not generic lists — each has its own columns, its own row actions
   * and its own endpoint — but each one is still a window onto one stage of the agency's
   * pipeline. Naming that stage here is what lets the sidebar follow the pipeline: the link
   * appears in the stage's own position, and goes away with the stage.
   *
   * The name, not the id, because ids differ per agency and this file is the same for all of
   * them. It is matched case-insensitively against the stage's name.
   */
  stage?: string;
  isActive?: (pathname: string) => boolean;
  sectionLabel?: string;
}

export const navigation: NavItem[] = [
  // ──── Core ────
  {
    name: "Dashboard",
    href: "/overview",
    scope: "both",
    icon: require("lucide-react").LayoutDashboard,
    claims: ["candidate.read", "system.admin"],
  },
  {
    name: "My work",
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
  { name: "sep-workflow", isSeparator: true, icon: null, sectionLabel: "Workflow" },

  {
    // At the head of Workflow rather than up in Core: the candidate list is the first board of
    // the pipeline, not a separate place, and sitting above the separator it read as though
    // registering someone and moving them through were two different jobs.
    name: "Candidates",
    href: "/candidates",
    icon: require("lucide-react").Users,
    claims: ["candidate.read", "system.admin"],
  },
  {
    name: "New Contracts",
    href: "/workflow/new-contracts",
    stage: "New Contracts",
    icon: require("lucide-react").FileText,
    claims: ["workflow.view", "system.admin"],
  },
  {
    name: "Embassy",
    href: "/workflow/embassy",
    stage: "Embassy",
    icon: require("lucide-react").Building,
    claims: ["embassy.read", "system.admin"],
  },
  {
    name: "Case Executive",
    href: "/workflow/case-executive",
    stage: "Case Executive",
    icon: require("lucide-react").Briefcase,
    // Only embassy.case_view — the board's own query requires it, so listing embassy.read here
    // showed Embassy Officers a link that opened straight onto a permission error.
    claims: ["embassy.case_view", "system.admin"],
  },
  {
    name: "LMIS",
    href: "/workflow/lmis",
    stage: "LMIS",
    icon: require("lucide-react").FileCheck,
    claims: ["lmis.read", "system.admin"],
  },
  {
    name: "Tickets",
    href: "/workflow/tickets",
    stage: "Ticket",
    icon: require("lucide-react").Plane,
    claims: ["travel.read", "system.admin"],
  },
  {
    name: "Departures",
    href: "/workflow/departures",
    stage: "Departure",
    icon: require("lucide-react").Clock,
    claims: ["travel.read", "system.admin"],
  },
  {
    name: "Arrivals",
    href: "/workflow/arrivals",
    stage: "Arrival",
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
    stage: "Commission",
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
    name: "Expenses",
    href: "/finance/expenses",
    icon: require("lucide-react").Receipt,
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
    name: "Staff & access",
    scope: "both",
    icon: require("lucide-react").UserCog,
    claims: ["users.read", "role.read", "system.admin"],
    children: [
      {
        // /staff is the user-account list, not the staff-profile list, so it is reached under
        // users.read. Under staff.read the office manager was shown the link and then met an
        // empty page: they can read staff profiles, and user accounts are somebody else's desk.
        name: "Staff",
        href: "/staff",
        scope: "both",
        icon: require("lucide-react").IdCard,
        claims: ["users.read", "system.admin"],
      },
      {
        // Platform only. The role catalogue says what every permission means and who may hold
        // it, the permission to edit roles included, so an agency owner with it could grant
        // their own account anything.
        name: "Roles & permissions",
        href: "/roles",
        scope: "both",
        icon: require("lucide-react").Shield,
        claims: ["system.admin"],
      },
    ],
  },
  {
    // Who changed or opened what in this agency. Owners and auditors hold audit.read.
    name: "Audit trail",
    href: "/audit",
    icon: require("lucide-react").ScrollText,
    claims: ["audit.read", "system.admin"],
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
    scope: "platform",
    icon: require("lucide-react").Library,
    claims: ["system.admin"],
  },
  {
    name: "Workflow setup",
    href: "/admin/workflow",
    icon: require("lucide-react").Workflow,
    claims: ["workflow.configure", "system.admin"],
  },
  {
    name: "Bot & notifications",
    href: "/admin/bot",
    scope: "both",
    icon: require("lucide-react").BellRing,
    claims: ["bot.configure", "system.admin"],
  },
  {
    name: "Errors",
    href: "/admin/errors",
    scope: "platform",
    icon: require("lucide-react").AlertOctagon,
    claims: ["system.admin"],
  },
  {
    name: "Subscriptions",
    href: "/subscriptions",
    scope: "platform",
    icon: require("lucide-react").Receipt,
    claims: ["system.admin"],
  },
  {
    // /api/tenants is behind the SuperAdmin policy, not a permission, so tenant.manage only ever
    // produced a link whose every request failed.
    name: "Agencies",
    href: "/tenants",
    scope: "platform",
    icon: require("lucide-react").Server,
    claims: ["system.admin"],
  },
  {
    name: "Settings",
    href: "/settings",
    scope: "both",
    icon: require("lucide-react").Settings,
    // Everyone. The browser helper, which the people filling forms all day need, is on this
    // page, and it was hidden from the clerk, the case executive, finance and the auditor
    // because none of them hold bot.use or settings.read. Each card still gates itself.

  },
];

/**
 * Stages that deliberately have no link of their own.
 *
 * Intake is the candidate list: everyone registered is in it, and a second entry pointing at
 * the same people under a different word is the kind of link somebody clicks exactly once.
 */
const STAGES_WITHOUT_A_LINK = new Set(["intake"]);

/** Where a generated board sits in the sidebar — the stage's own name, and a plain icon. */
function generatedBoard(stage: WorkflowStage, href: string): NavItem {
  return {
    name: stage.name,
    href,
    icon: require("lucide-react").Layers,
    // What the generic board asks for. A stage an agency added has no endpoint of its own, so
    // there is no narrower permission to name.
    claims: ["workflow.view", "system.admin"],
    stage: stage.name,
  };
}

/**
 * The navigation, with the Workflow section following the agency's own pipeline.
 *
 * The sidebar used to be a fixed list. An agency could add a stage on the setup screen, and the
 * board existed and worked, but nothing in the app pointed at it — so the pipeline was
 * configurable and the way into it was not. Remove a stage and the opposite: a link that opened
 * onto nothing.
 *
 * So the Workflow section is derived. Each stage, in the order the agency put them in, becomes
 * one entry: the board built for it where there is one — Embassy and LMIS keep their own
 * columns and their own actions — and the general-purpose board otherwise. A stage that is gone
 * takes its link with it.
 *
 * Two entries in that section are not stages and keep their places: Candidates at the top, which
 * is the pipeline's front door rather than a step in it, and Exceptions at the bottom, which is
 * every stage at once.
 *
 * Given no stages — still loading, or the request failed — this returns the static list
 * unchanged. A sidebar that empties itself while a fetch is in flight is worse than one that is
 * briefly out of date.
 */
export function navigationForStages(
  stages: readonly WorkflowStage[],
  items: NavItem[] = navigation,
): NavItem[] {
  if (stages.length === 0) return items;

  const firstBoard = items.findIndex((i) => i.stage && !i.isSeparator);
  if (firstBoard < 0) return items;

  // The run of the list the stages govern: from the first stage-backed board to the next
  // section heading. Commissions sits below the Finance heading and so is outside it — it is
  // still claimed by its stage, which is what keeps a second Commission link from appearing up
  // here, but it is not moved into the pipeline's order.
  let end = firstBoard;
  while (end < items.length && !items[end].isSeparator) end += 1;

  const before = items.slice(0, firstBoard);
  const run = items.slice(firstBoard, end);
  const after = items.slice(end);

  const boardFor = new Map<string, NavItem>();
  for (const item of items)
    if (item.stage) boardFor.set(item.stage.toLowerCase(), item);

  // Anything in the run that is not a board — Exceptions — stays where it is relative to the
  // boards, which in practice means after them.
  const notABoard = run.filter((i) => !i.stage);

  const inRun = new Set(run.filter((i) => i.stage).map((i) => i.name));
  const ordered = [...stages].sort((a, b) => a.sortOrder - b.sortOrder);

  const boards: NavItem[] = [];
  for (const stage of ordered) {
    const key = stage.name.toLowerCase().trim();
    if (STAGES_WITHOUT_A_LINK.has(key)) continue;

    const built = boardFor.get(key);
    // A stage whose board lives in another section — Commission, under Finance — is already
    // reachable, and is left there rather than listed twice.
    if (built && !inRun.has(built.name)) continue;

    boards.push(built ?? generatedBoard(stage, stageHref([...stages], stage)));
  }

  return [...before, ...boards, ...notABoard, ...after];
}

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
  /**
   * "platform" when a platform administrator has no agency selected. Everyone else is working
   * inside one agency — their own, or the one a platform administrator switched into — and sees
   * the agency's navigation.
   */
  mode: NavScope = "agency",
): NavItem[] {
  const hasAnyClaim = (required?: string[]) =>
    isSuperAdmin || !required?.length || required.some((c) => claims.includes(c));
  const inScope = (item: NavItem) => {
    const scope = item.scope ?? "agency";
    return scope === "both" || scope === mode;
  };

  const recur = (list: NavItem[]): NavItem[] =>
    dropEmptySections(
      list
        .map((item) => {
          if (item.isSeparator) return item;
          if (!inScope(item)) return null;
          const children = item.children ? recur(item.children) : undefined;
          // A parent with no href earns its place only through its children.
          if (item.children && (!children || children.length === 0)) return null;
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
