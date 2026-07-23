import { DEMO_OFFICES } from "@/lib/demo/demo-data";
import type { Office } from "@/types/workflow";

export type DemoUser = {
  id: string;
  username: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  isActive: boolean;
  isSuperAdmin: boolean;
  twoFactorEnabled: boolean;
  departmentName: string | null;
  tenantId: string | null;
  tenantName: string | null;
  lastLoginAt: string | null;
  roles: string[];
};

export type DemoRole = {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isSystemRole: boolean;
  isActive: boolean;
  sortOrder: number;
  permissions: string[];
  userCount: number;
};

export type DemoAgency = {
  id: string;
  name: string;
  slug: string;
  schemaName: string;
  contactEmail: string;
  status: number;
  provisionedAt: string;
};

export type DemoPartner = {
  id: string;
  name: string;
  country: string;
  phone: string;
  email?: string;
  isActive: boolean;
};

export type DemoDepartment = {
  id: string;
  name: string;
  code: string;
  description?: string;
  isActive: boolean;
};

export type DemoLedgerRow = {
  id: string;
  date: string;
  reference: string;
  account: string;
  description: string;
  debit: number;
  credit: number;
  currency: string;
};

export type DemoSettings = {
  agencyName: string;
  timezone: string;
  defaultCurrency: string;
  language: string;
  notifyEmail: boolean;
  notifySms: boolean;
  autoAssignCase: boolean;
};

function nid(prefix: string) {
  return `${prefix}-${Math.random().toString(36).slice(2, 9)}`;
}

const listeners = new Set<() => void>();
function notify() {
  listeners.forEach((l) => l());
}
export function subscribeAdminDemo(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

let offices: Office[] = structuredClone(DEMO_OFFICES);

let partners: DemoPartner[] = [
  { id: "p1", name: "Etenaa Resources Company", country: "KSA", phone: "+966500000001", email: "ops@etenaa.sa", isActive: true },
  { id: "p2", name: "Samood Al-khaleej Recruitment", country: "Kuwait", phone: "+965500000002", isActive: true },
  { id: "p3", name: "Golden Gate Partners", country: "UAE", phone: "+971500000003", isActive: true },
  { id: "p4", name: "Amjad Khayat", country: "KSA", phone: "+966500000004", isActive: true },
  { id: "p5", name: "Gulf Care Agency", country: "Bahrain", phone: "+973500000005", isActive: false },
];

let users: DemoUser[] = [
  {
    id: "u1",
    username: "sara.t",
    firstName: "Sara",
    lastName: "Tesfaye",
    email: "sara.t@simbaflow.local",
    phoneNumber: "+251911100001",
    isActive: true,
    isSuperAdmin: false,
    twoFactorEnabled: true,
    departmentName: "Operations",
    tenantId: "t1",
    tenantName: "SimbaFlow Demo Agency",
    lastLoginAt: "2026-07-22T08:10:00Z",
    roles: ["CaseExecutive"],
  },
  {
    id: "u2",
    username: "yonas.b",
    firstName: "Yonas",
    lastName: "Bekele",
    email: "yonas.b@simbaflow.local",
    phoneNumber: "+251911100002",
    isActive: true,
    isSuperAdmin: false,
    twoFactorEnabled: false,
    departmentName: "Embassy Desk",
    tenantId: "t1",
    tenantName: "SimbaFlow Demo Agency",
    lastLoginAt: "2026-07-21T16:40:00Z",
    roles: ["EmbassyOfficer"],
  },
  {
    id: "u3",
    username: "admin",
    firstName: "Agency",
    lastName: "Owner",
    email: "admin@simbaflow.local",
    phoneNumber: "+251911100000",
    isActive: true,
    isSuperAdmin: true,
    twoFactorEnabled: true,
    departmentName: "Management",
    tenantId: "t1",
    tenantName: "SimbaFlow Demo Agency",
    lastLoginAt: "2026-07-22T07:00:00Z",
    roles: ["AgencyOwner", "Admin"],
  },
  {
    id: "u4",
    username: "finance.h",
    firstName: "Helen",
    lastName: "Asrat",
    email: "helen.a@simbaflow.local",
    phoneNumber: null,
    isActive: false,
    isSuperAdmin: false,
    twoFactorEnabled: false,
    departmentName: "Finance",
    tenantId: "t1",
    tenantName: "SimbaFlow Demo Agency",
    lastLoginAt: null,
    roles: ["FinanceOfficer"],
  },
];

let roles: DemoRole[] = [
  {
    id: "r1",
    name: "Agency Owner",
    code: "agency_owner",
    description: "Full agency control",
    isSystemRole: true,
    isActive: true,
    sortOrder: 1,
    permissions: ["system.admin", "tenant.manage", "workflow.configure"],
    userCount: 1,
  },
  {
    id: "r2",
    name: "Case Executive",
    code: "case_executive",
    description: "Manage candidate pipeline",
    isSystemRole: false,
    isActive: true,
    sortOrder: 2,
    permissions: ["candidate.read", "candidate.write", "workflow.view"],
    userCount: 1,
  },
  {
    id: "r3",
    name: "Embassy Officer",
    code: "embassy_officer",
    description: "Embassy / medical tracks",
    isSystemRole: false,
    isActive: true,
    sortOrder: 3,
    permissions: ["embassy.read", "embassy.write", "workflow.view"],
    userCount: 1,
  },
  {
    id: "r4",
    name: "Finance Officer",
    code: "finance_officer",
    description: "Commissions and accounting",
    isSystemRole: false,
    isActive: true,
    sortOrder: 4,
    permissions: ["commission.read", "accounting.read", "accounting.write"],
    userCount: 1,
  },
];

let agencies: DemoAgency[] = [
  {
    id: "t1",
    name: "SimbaFlow Demo Agency",
    slug: "simbaflow-demo",
    schemaName: "tenant_simbaflow_demo",
    contactEmail: "admin@simbaflow.local",
    status: 0,
    provisionedAt: "2026-01-10T10:00:00Z",
  },
  {
    id: "t2",
    name: "Nile Labour Partners",
    slug: "nile-labour",
    schemaName: "tenant_nile_labour",
    contactEmail: "ops@nilelabour.et",
    status: 0,
    provisionedAt: "2026-03-02T09:00:00Z",
  },
  {
    id: "t3",
    name: "Horn Export Services",
    slug: "horn-export",
    schemaName: "tenant_horn_export",
    contactEmail: "info@hornexport.et",
    status: 1,
    provisionedAt: "2026-05-18T12:00:00Z",
  },
];

let departments: DemoDepartment[] = [
  { id: "d1", name: "Operations", code: "OPS", description: "Case handling", isActive: true },
  { id: "d2", name: "Embassy Desk", code: "EMB", description: "Visa & medical", isActive: true },
  { id: "d3", name: "Finance", code: "FIN", description: "Commissions & ledger", isActive: true },
  { id: "d4", name: "Management", code: "MGT", isActive: true },
];

let ledger: DemoLedgerRow[] = [
  {
    id: "l1",
    date: "2026-07-18",
    reference: "JE-1042",
    account: "2100 · Commission Payable",
    description: "Commission accrual — Selam Getachew",
    debit: 0,
    credit: 12500,
    currency: "ETB",
  },
  {
    id: "l2",
    date: "2026-07-18",
    reference: "JE-1042",
    account: "4100 · Placement Revenue",
    description: "Placement fee recognized",
    debit: 18500,
    credit: 0,
    currency: "ETB",
  },
  {
    id: "l3",
    date: "2026-07-19",
    reference: "JE-1048",
    account: "1100 · Cash",
    description: "Partner receipt — Etenaa",
    debit: 2200,
    credit: 0,
    currency: "USD",
  },
  {
    id: "l4",
    date: "2026-07-20",
    reference: "JE-1051",
    account: "5100 · Medical Expense",
    description: "Clinic batch invoice",
    debit: 4800,
    credit: 0,
    currency: "ETB",
  },
];

let settings: DemoSettings = {
  agencyName: "SimbaFlow Demo Agency",
  timezone: "Africa/Addis_Ababa",
  defaultCurrency: "ETB",
  language: "en",
  notifyEmail: true,
  notifySms: false,
  autoAssignCase: true,
};

// ── Offices ──
export function getDemoOffices() {
  return offices;
}
export function upsertDemoOffice(input: Partial<Office> & { name: string; code: string }) {
  if (input.id) {
    offices = offices.map((o) => (o.id === input.id ? { ...o, ...input, isActive: input.isActive ?? o.isActive } : o));
  } else {
    offices = [
      ...offices,
      {
        id: nid("off"),
        name: input.name,
        code: input.code,
        city: input.city,
        phone: input.phone,
        email: input.email,
        isActive: input.isActive ?? true,
      },
    ];
  }
  notify();
  return offices;
}
export function deleteDemoOffice(id: string) {
  offices = offices.filter((o) => o.id !== id);
  notify();
}

// ── Partners ──
export function getDemoPartners() {
  return partners;
}
export function upsertDemoPartner(input: Partial<DemoPartner> & { name: string; country: string }) {
  if (input.id) {
    partners = partners.map((p) => (p.id === input.id ? { ...p, ...input, isActive: input.isActive ?? p.isActive } : p));
  } else {
    partners = [
      ...partners,
      {
        id: nid("pt"),
        name: input.name,
        country: input.country,
        phone: input.phone ?? "",
        email: input.email,
        isActive: input.isActive ?? true,
      },
    ];
  }
  notify();
  return partners;
}
export function deleteDemoPartner(id: string) {
  partners = partners.filter((p) => p.id !== id);
  notify();
}

// ── Users ──
export function getDemoUsers() {
  return users;
}
export function createDemoUser(input: {
  firstName: string;
  lastName: string;
  username: string;
  email: string;
  phoneNumber?: string;
  role: string;
  requireMfa?: boolean;
}) {
  const user: DemoUser = {
    id: nid("u"),
    username: input.username,
    firstName: input.firstName,
    lastName: input.lastName,
    email: input.email,
    phoneNumber: input.phoneNumber ?? null,
    isActive: true,
    isSuperAdmin: false,
    twoFactorEnabled: !!input.requireMfa,
    departmentName: "Operations",
    tenantId: "t1",
    tenantName: "SimbaFlow Demo Agency",
    lastLoginAt: null,
    roles: [input.role],
  };
  users = [user, ...users];
  notify();
  return user;
}
export function toggleDemoUser(id: string) {
  users = users.map((u) => (u.id === id ? { ...u, isActive: !u.isActive } : u));
  notify();
}
export function deleteDemoUser(id: string) {
  users = users.filter((u) => u.id !== id);
  notify();
}
export function resetDemoUserPassword(_id: string) {
  return true;
}

// ── Roles ──
export function getDemoRoles() {
  return roles;
}
export function createDemoRole(input: {
  name: string;
  code: string;
  description?: string;
  permissions?: string[];
  sortOrder?: number;
}) {
  const role: DemoRole = {
    id: nid("r"),
    name: input.name,
    code: input.code,
    description: input.description ?? null,
    isSystemRole: false,
    isActive: true,
    sortOrder: input.sortOrder ?? roles.length + 1,
    permissions: input.permissions ?? [],
    userCount: 0,
  };
  roles = [...roles, role];
  notify();
  return role;
}
export function deleteDemoRole(id: string) {
  roles = roles.filter((r) => !r.isSystemRole && r.id !== id);
  notify();
}

// ── Tenants ──
export function getDemoAgencies() {
  return agencies;
}
export function createDemoAgency(input: { name: string; slug: string; contactEmail: string }) {
  const agency: DemoAgency = {
    id: nid("t"),
    name: input.name,
    slug: input.slug,
    schemaName: `tenant_${input.slug.replace(/-/g, "_")}`,
    contactEmail: input.contactEmail,
    status: 0,
    provisionedAt: new Date().toISOString(),
  };
  agencies = [agency, ...agencies];
  notify();
  return agency;
}
export function updateDemoAgency(id: string, patch: Partial<DemoAgency>) {
  agencies = agencies.map((a) => (a.id === id ? { ...a, ...patch } : a));
  notify();
}
export function setDemoAgencyStatus(id: string, status: number) {
  updateDemoAgency(id, { status });
}
export function deleteDemoAgency(id: string) {
  agencies = agencies.filter((a) => a.id !== id);
  notify();
}

// ── Departments ──
export function getDemoDepartments() {
  return departments;
}
export function upsertDemoDepartment(input: Partial<DemoDepartment> & { name: string; code: string }) {
  if (input.id) {
    departments = departments.map((d) => (d.id === input.id ? { ...d, ...input, isActive: input.isActive ?? d.isActive } : d));
  } else {
    departments = [
      ...departments,
      { id: nid("d"), name: input.name, code: input.code, description: input.description, isActive: input.isActive ?? true },
    ];
  }
  notify();
}
export function deleteDemoDepartment(id: string) {
  departments = departments.filter((d) => d.id !== id);
  notify();
}

// ── Finance ──
export function getDemoLedger() {
  return ledger;
}
export function addDemoLedgerEntry(input: Omit<DemoLedgerRow, "id">) {
  ledger = [{ ...input, id: nid("l") }, ...ledger];
  notify();
}

// ── Settings ──
export function getDemoSettings() {
  return settings;
}
export function saveDemoSettings(patch: Partial<DemoSettings>) {
  settings = { ...settings, ...patch };
  notify();
  return settings;
}
