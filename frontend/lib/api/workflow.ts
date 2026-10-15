import useSWR from "swr";
// The slug table and the resolver live in a leaf module so the sidebar can build addresses
// without pulling the data layer into the sign-in page. Re-exported: this is where the rest
// of the app has always imported it from.
export { resolveStageFromSlugOrId, stageHref } from "@/lib/workflow/stage-slug";
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

/**
 * The agency's pipeline: its stages, the steps between them, and the mirroring rules.
 *
 * @param enabled False holds the request back. A platform administrator with no agency chosen
 *   is not inside one, and asking for its workflow would be asking whose.
 */
export function useWorkflowDefinition(enabled = true) {
  const { data, error, isLoading, mutate } = useSWR<ApiResult<WorkflowDefinition>>(
    enabled ? "/api/proxy/workflow/config" : null,
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
  params?: { page?: number; pageSize?: number; search?: string; filters?: string }
) {
  const page = params?.page ?? 1;
  const pageSize = params?.pageSize ?? 50;
  const qs = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  if (params?.search) qs.set("search", params.search);
  if (params?.filters) qs.set("filters", params.filters);

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
