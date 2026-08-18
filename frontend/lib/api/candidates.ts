import useSWR from "swr";
import type { Candidate, CandidateDocument, CandidateListDto, TimelineEntry } from "@/types/candidate";

type ApiResult<T> = {
  isSuccess?: boolean;
  data?: T;
  error?: string;
  statusCode?: number;
};

type PaginatedCandidates = {
  items: CandidateListDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

const fetcher = async <T>(url: string): Promise<T> => {
  const res = await fetch(url);
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error(body?.error || `Request failed (${res.status})`);
  }
  return res.json();
};

export function useCandidates(params?: {
  page?: number;
  pageSize?: number;
  search?: string;
  stageId?: string;
  countryOfTravel?: string;
}) {
  const page = params?.page ?? 1;
  const pageSize = params?.pageSize ?? 20;
  const qs = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  if (params?.search) qs.set("search", params.search);
  if (params?.stageId) qs.set("stageId", params.stageId);
  if (params?.countryOfTravel) qs.set("countryOfTravel", params.countryOfTravel);

  const key = `/api/proxy/candidates?${qs.toString()}`;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<PaginatedCandidates>>(key, fetcher, {
    revalidateOnFocus: false,
  });

  return {
    candidates: data?.data?.items ?? [],
    totalCount: data?.data?.totalCount ?? 0,
    page: data?.data?.page ?? page,
    pageSize: data?.data?.pageSize ?? pageSize,
    totalPages: data?.data?.totalPages ?? 0,
    isLoading,
    error,
    mutate,
    key,
  };
}

export function useCandidate(id: string | undefined) {
  const key = id ? `/api/proxy/candidates/${id}` : null;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<Candidate>>(key, fetcher, {
    revalidateOnFocus: false,
  });

  return {
    candidate: data?.data,
    isLoading,
    error,
    mutate,
  };
}

export function useCandidateDocuments(candidateId: string | undefined) {
  const key = candidateId ? `/api/proxy/candidates/${candidateId}/documents` : null;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<CandidateDocument[]>>(key, fetcher, {
    revalidateOnFocus: false,
  });

  return {
    documents: data?.data ?? [],
    isLoading,
    error,
    mutate,
  };
}

export function useCandidateTimeline(candidateId: string | undefined) {
  const key = candidateId ? `/api/proxy/candidates/${candidateId}/timeline` : null;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<TimelineEntry[]>>(key, fetcher, {
    revalidateOnFocus: false,
  });

  return {
    events: data?.data ?? [],
    isLoading,
    error,
    mutate,
  };
}

export async function uploadCandidateDocument(
  candidateId: string,
  file: File,
  documentType: number
): Promise<string> {
  const form = new FormData();
  form.append("file", file);
  form.append("documentType", String(documentType));
  const res = await fetch(
    `/api/proxy/candidates/${candidateId}/documents?documentType=${documentType}`,
    { method: "POST", body: form }
  );
  const body = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(body?.error || "Upload failed");
  return body?.data as string;
}

async function readPdfBlob(res: Response, fallbackError: string): Promise<Blob> {
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error(body?.error || fallbackError);
  }

  const blob = await res.blob();
  const header = await blob.slice(0, 5).text();
  if (!header.startsWith("%PDF")) {
    throw new Error(`${fallbackError}: invalid PDF response`);
  }
  return new Blob([blob], { type: "application/pdf" });
}

export async function generateCandidateCv(candidateId: string): Promise<Blob> {
  const res = await fetch(`/api/proxy/candidates/${candidateId}/cv`, { method: "POST" });
  return readPdfBlob(res, "CV generation failed");
}

export async function generateBulkCandidateCvs(candidateIds: string[]): Promise<Blob> {
  const res = await fetch("/api/proxy/candidates/cv/bulk", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ candidateIds }),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error(body?.error || "Bulk CV generation failed");
  }
  const blob = await res.blob();
  const type = blob.type || "";
  if (type.includes("json")) {
    const text = await blob.text();
    try {
      const parsed = JSON.parse(text);
      throw new Error(parsed?.error || "Bulk CV generation failed");
    } catch (e) {
      if (e instanceof Error && e.message !== "Bulk CV generation failed") throw e;
      throw new Error("Bulk CV generation failed");
    }
  }
  return blob;
}

export async function generateCandidateVisaForm(candidateId: string): Promise<Blob> {
  const res = await fetch(`/api/proxy/candidates/${candidateId}/visa-form`, { method: "POST" });
  return readPdfBlob(res, "Visa form generation failed");
}

export async function deleteCandidate(id: string): Promise<void> {
  const res = await fetch(`/api/proxy/candidates/${id}`, { method: "DELETE" });
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error(body?.error || "Delete failed");
  }
}
