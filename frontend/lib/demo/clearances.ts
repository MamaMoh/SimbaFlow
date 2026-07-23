import {
  DEMO_CANDIDATES,
  applyDemoCandidateAction,
  type DemoCandidate,
} from "@/lib/demo/demo-data";

export type ClearanceStatus =
  | "Ready"
  | "InProgress"
  | "Blocked"
  | "Waiting"
  | "Complete"
  | "NotStarted";

export interface ClearanceServiceDef {
  id: string;
  label: string;
  easyenjazAlias: string;
  trackKey: string;
  stageSlug: string;
  description: string;
  doneValues: string[];
  progressValues: string[];
  blockedValues: string[];
  completeStatus: string;
}

export interface CandidateClearance {
  serviceId: string;
  label: string;
  easyenjazAlias: string;
  description: string;
  status: ClearanceStatus;
  statusLabel: string;
  since?: string;
  blocking: boolean;
  href: string;
  trackKey: string;
  completeStatus: string;
}

export interface ReadinessItem {
  id: string;
  label: string;
  done: boolean;
  detail?: string;
}

export interface CandidateReadiness {
  percent: number;
  completed: number;
  total: number;
  items: ReadinessItem[];
}

export interface ClearanceQueueCount {
  serviceId: string;
  label: string;
  easyenjazAlias: string;
  waiting: number;
  blocked: number;
  href: string;
}

/** EasyEnjaz-inspired clearance catalog mapped onto SimbaFlow tracks. */
export const CLEARANCE_SERVICES: ClearanceServiceDef[] = [
  {
    id: "wafid",
    label: "Wafid Medical",
    easyenjazAlias: "Wafid",
    trackKey: "medical",
    stageSlug: "embassy",
    description: "GCC medical fitness (Wafid)",
    doneValues: ["Fit"],
    progressValues: ["OnProgress", "Checked", "Verified"],
    blockedValues: ["Unfit", "Expired", "Rejected"],
    completeStatus: "Fit",
  },
  {
    id: "visa",
    label: "Visa / MOFA",
    easyenjazAlias: "Visa",
    trackKey: "embassy",
    stageSlug: "embassy",
    description: "Visa issuance & embassy clearance",
    doneValues: ["Issued"],
    progressValues: ["Ready", "OnProgress", "Submitted"],
    blockedValues: ["Rejected", "Expired"],
    completeStatus: "Issued",
  },
  {
    id: "musaned",
    label: "Musaned",
    easyenjazAlias: "Musaned",
    trackKey: "musaned",
    stageSlug: "new-contracts",
    description: "Sponsor contract & Musaned readiness",
    doneValues: ["Linked", "Approved", "Done"],
    progressValues: ["Pending", "Submitted", "OnProgress"],
    blockedValues: ["Rejected"],
    completeStatus: "Linked",
  },
  {
    id: "insurance",
    label: "Nyala Insurance",
    easyenjazAlias: "Insurance",
    trackKey: "insurance",
    stageSlug: "lmis",
    description: "Policy payment & verification",
    doneValues: ["Paid", "Verified"],
    progressValues: ["Requested", "PaymentVerified", "OnProgress"],
    blockedValues: ["Unpaid", "Expired", "Rejected"],
    completeStatus: "Paid",
  },
  {
    id: "coc",
    label: "COC / LMIS",
    easyenjazAlias: "COC",
    trackKey: "lmis",
    stageSlug: "lmis",
    description: "Labour market & certificate of competency",
    doneValues: ["Issued", "Approved"],
    progressValues: ["Submitted", "OnProgress", "Verified", "Checked"],
    blockedValues: ["Rejected", "Expired"],
    completeStatus: "Issued",
  },
  {
    id: "ticket",
    label: "Ticketing",
    easyenjazAlias: "Ticket",
    trackKey: "ticket",
    stageSlug: "tickets",
    description: "Flight booking through departure",
    doneValues: ["Booked"],
    progressValues: ["Requested", "OnProgress"],
    blockedValues: ["NotBooked", "Cancelled"],
    completeStatus: "Booked",
  },
];

function findCandidate(candidateId: string): DemoCandidate | undefined {
  const id = candidateId.replace(/-preview$/, "");
  return DEMO_CANDIDATES.find((c) => c.id === id);
}

function classifyStatus(
  raw: string | undefined,
  def: ClearanceServiceDef,
): { status: ClearanceStatus; blocking: boolean } {
  if (!raw) return { status: "NotStarted", blocking: false };
  if (def.doneValues.includes(raw)) return { status: "Complete", blocking: false };
  if (def.blockedValues.includes(raw)) return { status: "Blocked", blocking: true };
  if (def.progressValues.includes(raw)) return { status: "InProgress", blocking: false };
  return { status: "Waiting", blocking: false };
}

function musanedRaw(c: DemoCandidate): string | undefined {
  if (c.statusValues.musaned) return c.statusValues.musaned;
  if (c.sponsorName && c.sponsorId) return "Linked";
  if (c.sponsorName) return "Pending";
  return undefined;
}

function rawForService(c: DemoCandidate, def: ClearanceServiceDef): string | undefined {
  if (def.id === "musaned") return musanedRaw(c);
  if (def.id === "visa") {
    // Prefer embassy track; fall back to tasheer Done as progress signal
    const embassy = c.statusValues.embassy;
    if (embassy) return embassy;
    if (c.statusValues.tasheer === "Done") return "Ready";
    if (c.visaNo) return "Ready";
    return c.statusValues.tasheer;
  }
  return c.statusValues[def.trackKey];
}

function sinceForService(c: DemoCandidate, def: ClearanceServiceDef): string | undefined {
  if (def.id === "musaned") {
    return c.statusValues.musaned_since || (c.sponsorName ? c.registeredAt : undefined);
  }
  return c.statusValues[`${def.trackKey}_since`];
}

export function getCandidateClearances(candidateId: string): CandidateClearance[] {
  const c = findCandidate(candidateId);
  if (!c) return [];

  return CLEARANCE_SERVICES.map((def) => {
    const raw = rawForService(c, def);
    const { status, blocking } = classifyStatus(raw, def);
    return {
      serviceId: def.id,
      label: def.label,
      easyenjazAlias: def.easyenjazAlias,
      description: def.description,
      status,
      statusLabel: raw || "Not started",
      since: sinceForService(c, def),
      blocking,
      href: `/workflow/${def.stageSlug}`,
      trackKey: def.trackKey,
      completeStatus: def.completeStatus,
    };
  });
}

export function getCandidateReadiness(candidateId: string): CandidateReadiness {
  const c = findCandidate(candidateId);
  if (!c) {
    return { percent: 0, completed: 0, total: 0, items: [] };
  }

  const medical = c.statusValues.medical;
  const visa = c.statusValues.embassy;
  const insurance = c.statusValues.insurance;
  const ticket = c.statusValues.ticket;

  const items: ReadinessItem[] = [
    {
      id: "passport",
      label: "Passport on file",
      done: !!c.passportNumber && c.passportNumber.length >= 6,
      detail: c.passportNumber,
    },
    {
      id: "photo",
      label: "Profile registered",
      done: !!c.fullName && !!c.labourId,
      detail: c.labourId,
    },
    {
      id: "medical",
      label: "Medical Fit (Wafid)",
      done: medical === "Fit",
      detail: medical,
    },
    {
      id: "visa",
      label: "Visa Issued",
      done: visa === "Issued" || !!c.visaNo,
      detail: visa || c.visaNo,
    },
    {
      id: "insurance",
      label: "Insurance Paid",
      done: insurance === "Paid" || insurance === "Verified",
      detail: insurance,
    },
    {
      id: "ticket",
      label: "Ticket Booked",
      done: ticket === "Booked",
      detail: ticket,
    },
    {
      id: "flight",
      label: "Flight date set",
      done: !!c.flightDate,
      detail: c.flightDate ? new Date(c.flightDate).toLocaleDateString() : undefined,
    },
  ];

  const completed = items.filter((i) => i.done).length;
  const total = items.length;
  const percent = total === 0 ? 0 : Math.round((completed / total) * 100);

  return { percent, completed, total, items };
}

export function getClearanceQueueCounts(): ClearanceQueueCount[] {
  return CLEARANCE_SERVICES.map((def) => {
    let waiting = 0;
    let blocked = 0;

    for (const c of DEMO_CANDIDATES) {
      if (c.stageSlug === "commissions" && c.statusValues.commission === "Paid") continue;

      const raw = rawForService(c, def);
      const { status, blocking } = classifyStatus(raw, def);

      if (blocking || status === "Blocked") {
        blocked += 1;
        continue;
      }
      if (status === "Complete") continue;
      if (status === "InProgress" || status === "Waiting" || status === "NotStarted") {
        waiting += 1;
      }
    }

    return {
      serviceId: def.id,
      label: def.label,
      easyenjazAlias: def.easyenjazAlias,
      waiting,
      blocked,
      href: `/workflow/${def.stageSlug}`,
    };
  });
}

/** Mark a clearance complete (or set status) via demo store. */
export function updateClearanceStatus(
  candidateId: string,
  serviceId: string,
  status?: string,
): { ok: boolean; message: string } {
  const def = CLEARANCE_SERVICES.find((s) => s.id === serviceId);
  if (!def) return { ok: false, message: "Unknown clearance service" };

  const value = status ?? def.completeStatus;
  const trackKey = def.id === "musaned" ? "musaned" : def.trackKey;
  return applyDemoCandidateAction(candidateId, `status:${trackKey}:${value}`);
}
