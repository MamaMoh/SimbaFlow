import useSWR from "swr";

export type PartnerRow = {
  id: string;
  name: string;
  country: string;
  countryCode: string;
  contactEmail: string | null;
  status: string;
  capacityTier: string;
  maxEthiopianAgencies: number;
  foreignLicenseId: string | null;
  linkId?: string;
  agreementStart?: string;
  agreementEnd?: string;
  /** Computed server-side: Active | ExpiringSoon | Expired | NotStarted | Suspended */
  agreementState?: string;
  daysRemaining?: number;
  agreementLabel?: string;
  isUsable?: boolean;
};

export type PartnerCapacity = {
  agencyLevel: number;
  levelDescription: string;
  maxPartnersPerCountry: number;
  maxLicensedCountries: number | null;
  licensedCountries: string[];
  licensedCountriesUsed: number;
  byCountry: { country: string; used: number; max: number; remaining: number }[];
};

export type PartnerCandidateRow = {
  id: string;
  fullName: string;
  passportNumber: string;
  stage: string | null;
  countryOfTravel: string | null;
  registeredAt: string;
  status: string;
};

export type PartnerBilling = {
  partnerName: string;
  country: string;
  commissionCount: number;
  totalFees: number;
  totalPaid: number;
  outstanding: number;
  byStatus: { status: string; count: number; balance: number }[];
  items: {
    id: string;
    candidateId: string;
    candidateName: string;
    passportNumber: string;
    status: string;
    totalFeesAmount: number;
    totalPaidAmount: number;
    balanceAmount: number;
    openedAt: string;
  }[];
};

/** Maps the server's agreement state to a canonical UI tone. */
export function agreementTone(state?: string): "success" | "warning" | "danger" | "neutral" {
  switch (state) {
    case "Active": return "success";
    case "ExpiringSoon": return "warning";
    case "Expired":
    case "Suspended": return "danger";
    default: return "neutral";
  }
}

type ApiEnvelope = {
  isSuccess?: boolean;
  data?: PartnerRow[];
  error?: string;
};

const fetcher = async (url: string): Promise<PartnerRow[]> => {
  const res = await fetch(url);
  const body: ApiEnvelope = await res.json().catch(() => ({}));
  if (!res.ok || body?.isSuccess === false) {
    throw new Error(body?.error || `Request failed (${res.status})`);
  }
  return body.data ?? [];
};

export function usePartners(opts?: {
  linkedOnly?: boolean;
  enabled?: boolean;
}) {
  const linkedOnly = opts?.linkedOnly === true;
  const enabled = opts?.enabled !== false;
  const url = linkedOnly
    ? "/api/proxy/partners?linkedOnly=true"
    : "/api/proxy/partners";
  return useSWR(enabled ? url : null, fetcher, { revalidateOnFocus: false });
}

export const partnersApi = {
  async createCatalog(body: Record<string, unknown>) {
    const res = await fetch("/api/proxy/partners", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return res.json();
  },
  async updateLinkStatus(linkId: string, status: string) {
    const res = await fetch(`/api/proxy/partners/links/${linkId}/status`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ status }),
    });
    return res.json();
  },
};

const jsonFetcher = async <T>(url: string): Promise<T> => {
  const res = await fetch(url);
  const body = await res.json().catch(() => ({}));
  if (!res.ok || body?.isSuccess === false) {
    throw new Error(body?.error || `Request failed (${res.status})`);
  }
  return body.data as T;
};

/** Partner slots allowed by the agency's MoLS level, and how many are used per country. */
export function usePartnerCapacity(enabled = true) {
  return useSWR<PartnerCapacity>(
    enabled ? "/api/proxy/partners/capacity" : null,
    jsonFetcher,
    { revalidateOnFocus: false }
  );
}

/** Candidates this agency placed through a partner — "where did the person go". */
export function usePartnerCandidates(partnerId?: string, enabled = true) {
  return useSWR<{
    partnerName: string;
    country: string;
    totalCandidates: number;
    items: PartnerCandidateRow[];
  }>(
    enabled && partnerId ? `/api/proxy/partners/${partnerId}/candidates` : null,
    jsonFetcher,
    { revalidateOnFocus: false }
  );
}

/** Commission rollup for a partner. */
export function usePartnerBilling(partnerId?: string, enabled = true) {
  return useSWR<PartnerBilling>(
    enabled && partnerId ? `/api/proxy/partners/${partnerId}/billing` : null,
    jsonFetcher,
    { revalidateOnFocus: false }
  );
}
