import useSWR from "swr";
import type {
  AvailableAction,
  WorkflowDefinition,
  WorkflowStage,
  WorkflowState,
} from "@/types/workflow";

type ApiResult<T> = {
  isSuccess?: boolean;
  data?: T;
  error?: string;
  statusCode?: number;
};

export type ViewCandidateDto = {
  id: string;
  fullName: string;
  passportNumber: string;
  labourId?: string | null;
  currentStageName?: string | null;
  currentStageId?: string | null;
  statusValues: Record<string, string>;
  countryOfTravel?: string | null;
  partnerName?: string | null;
  registeredAt: string;
  isMirror: boolean;
};

type PaginatedView = {
  items: ViewCandidateDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

export type WorkflowEventDto = {
  id: string;
  sequenceNumber: number;
  eventType: number;
  fromStageId?: string | null;
  fromStageName?: string | null;
  toStageId?: string | null;
  toStageName?: string | null;
  userName: string;
  timestamp: string;
  notes?: string | null;
};

const fetcher = async <T>(url: string): Promise<T> => {
  const res = await fetch(url);
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error(body?.error || `Request failed (${res.status})`);
  }
  return res.json();
};

/** Map nav slugs → seed stage names (handles plural nav paths). */
const STAGE_SLUG_TO_NAMES: Record<string, string[]> = {
  "new-contracts": ["New Contracts"],
  embassy: ["Embassy"],
  lmis: ["LMIS"],
  tickets: ["Ticket", "Tickets"],
  ticket: ["Ticket"],
  departures: ["Departure", "Departures"],
  departure: ["Departure"],
  arrivals: ["Arrival", "Arrivals"],
  arrival: ["Arrival"],
  commissions: ["Commission", "Commissions"],
  commission: ["Commission"],
  intake: ["Intake"],
};

export function resolveStageFromSlugOrId(
  stages: WorkflowStage[],
  stageIdOrSlug: string
): WorkflowStage | undefined {
  const byId = stages.find((s) => s.id === stageIdOrSlug);
  if (byId) return byId;

  const names = STAGE_SLUG_TO_NAMES[stageIdOrSlug.toLowerCase()] ?? [
    stageIdOrSlug.replace(/-/g, " "),
  ];

  return stages.find((s) =>
    names.some((n) => s.name.toLowerCase() === n.toLowerCase())
  );
}

export function useWorkflowDefinition() {
  const { data, error, isLoading, mutate } = useSWR<ApiResult<WorkflowDefinition>>(
    "/api/proxy/workflow/config",
    fetcher,
    { revalidateOnFocus: false }
  );

  return {
    definition: data?.data,
    stages: data?.data?.stages ?? [],
    isLoading,
    error,
    mutate,
  };
}

export function useViewCandidates(
  stageId: string | undefined,
  params?: { page?: number; pageSize?: number; search?: string }
) {
  const page = params?.page ?? 1;
  const pageSize = params?.pageSize ?? 50;
  const qs = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  if (params?.search) qs.set("search", params.search);

  const key = stageId
    ? `/api/proxy/workflow/views/${stageId}/candidates?${qs.toString()}`
    : null;

  const { data, error, isLoading, mutate } = useSWR<ApiResult<PaginatedView>>(key, fetcher, {
    revalidateOnFocus: false,
  });

  return {
    candidates: data?.data?.items ?? [],
    totalCount: data?.data?.totalCount ?? 0,
    isLoading,
    error,
    mutate,
  };
}

/**
 * Available workflow steps for a candidate.
 *
 * Pass `stageId` when calling from a board: a candidate is visible on several boards at once via
 * mirror stages, and without it every board offers every other board's buttons.
 */
export function useAvailableActions(candidateId: string | undefined, stageId?: string) {
  const key = candidateId
    ? `/api/proxy/workflow/${candidateId}/actions${stageId ? `?stageId=${stageId}` : ""}`
    : null;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<AvailableAction[]>>(key, fetcher, {
    revalidateOnFocus: false,
  });

  return {
    actions: data?.data ?? [],
    isLoading,
    error,
    mutate,
  };
}

export function useWorkflowState(candidateId: string | undefined) {
  const key = candidateId ? `/api/proxy/workflow/${candidateId}/state` : null;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<WorkflowState>>(key, fetcher, {
    revalidateOnFocus: false,
  });

  return {
    state: data?.data,
    isLoading,
    error,
    mutate,
  };
}

export function useWorkflowEvents(candidateId: string | undefined) {
  const key = candidateId ? `/api/proxy/workflow/${candidateId}/events` : null;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<WorkflowEventDto[]>>(key, fetcher, {
    revalidateOnFocus: false,
  });

  return {
    events: data?.data ?? [],
    isLoading,
    error,
    mutate,
  };
}

export async function executeTransition(
  candidateId: string,
  transitionRuleId: string,
  notes?: string
): Promise<void> {
  const res = await fetch(`/api/proxy/workflow/${candidateId}/transition`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ transitionRuleId, notes }),
  });
  const body = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(body?.error || "Transition failed");
}

/** What undoing a candidate's last move between stages would do. */
export type MoveBackPreview = {
  fromStageName: string;
  toStageName: string;
  clears: { track: string; value: string }[];
};

/**
 * Asks what moving back would do, without doing it. Throws with the reason when there is no
 * earlier stage to go to, which is what the dialog shows instead of a confirm button.
 */
/**
 * `stageId` is the board the question is being asked from.
 *
 * Boards mirror, so the same candidate appears on more than one of them while standing in only
 * one. Naming the board lets the server refuse a move asked for from a board the candidate is
 * merely shown on. The candidates list and the candidate's own page are not boards and name none.
 */
const moveBackUrl = (candidateId: string, stageId?: string) =>
  `/api/proxy/workflow/${candidateId}/move-back` +
  (stageId ? `?stageId=${encodeURIComponent(stageId)}` : "");

export async function previewMoveBack(
  candidateId: string,
  stageId?: string,
): Promise<MoveBackPreview> {
  const res = await fetch(moveBackUrl(candidateId, stageId));
  const body = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(body?.error || "Could not work out where they would go back to");
  return body?.data as MoveBackPreview;
}

export async function moveBackStage(
  candidateId: string,
  reason?: string,
  stageId?: string,
): Promise<MoveBackPreview> {
  const res = await fetch(moveBackUrl(candidateId, stageId), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ reason }),
  });
  const body = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(body?.error || "Could not move the candidate back");
  return body?.data as MoveBackPreview;
}

export async function updateWorkflowStatus(
  candidateId: string,
  trackName: string,
  newValue: string,
  notes?: string
): Promise<void> {
  const res = await fetch(`/api/proxy/workflow/${candidateId}/status`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ trackName, newValue, notes }),
  });
  const body = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(body?.error || "Status update failed");
}
