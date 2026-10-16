import useSWR from "swr";

export { MY_WORK_PERMISSIONS } from "@/lib/workflow/my-work-permissions";

export type WorkQueueRow = {
  id: string;
  candidateId: string;
  fullName: string;
  passportNumber: string;
  partnerName?: string | null;
  officeName?: string | null;
  nextStep: string;
  /** True when it is this desk's turn; false when it waits on somebody else. */
  needsAction: boolean;
  daysInStage: number;
  overdue: boolean;
  warning?: string | null;
  href: string;
  /**
   * Which of the stage's parallel tracks this row is on now — Medical, Tasheer, Visa and so on.
   * More than one when the desk owes the same candidate two things at once.
   */
  tracks: string[];
};

export type WorkQueue = {
  key: string;
  label: string;
  boardHref: string;
  needsActionCount: number;
  waitingCount: number;
  overdueCount: number;
  items: WorkQueueRow[];
};

export type FilterOption = { value: string; label: string };

export type MyWorkQueues = {
  queues: WorkQueue[];
  partners: FilterOption[];
  offices: FilterOption[];
};

export type WorkQueueFilters = {
  search?: string;
  partner?: string;
  office?: string;
};

const fetcher = async (url: string): Promise<MyWorkQueues> => {
  const res = await fetch(url);
  const body = await res.json().catch(() => ({}));
  if (!res.ok || body?.isSuccess === false) {
    throw new Error(body?.error || `Request failed (${res.status})`);
  }
  return body.data as MyWorkQueues;
};

export function workQueuesKey(filters?: WorkQueueFilters): string {
  const qs = new URLSearchParams();
  if (filters?.search) qs.set("search", filters.search);
  if (filters?.partner) qs.set("partner", filters.partner);
  if (filters?.office) qs.set("office", filters.office);
  const query = qs.toString();
  return `/api/proxy/tasks/queues${query ? `?${query}` : ""}`;
}

/**
 * The tabs and their rows. Unfiltered, it is the same request the sidebar badge makes, so
 * opening the page with no filters set reuses what the badge already fetched.
 */
export function useMyWorkQueues(filters?: WorkQueueFilters, enabled = true) {
  return useSWR<MyWorkQueues>(enabled ? workQueuesKey(filters) : null, fetcher, {
    revalidateOnFocus: true,
    refreshInterval: 60_000,
    keepPreviousData: true,
  });
}

/** What the sidebar shows next to My work: rows across every tab that are this desk's turn. */
export function totalNeedsAction(data?: MyWorkQueues): number {
  return (data?.queues ?? []).reduce((sum, q) => sum + q.needsActionCount, 0);
}
