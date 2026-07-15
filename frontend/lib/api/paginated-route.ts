/**
 * Helpers for Next.js API routes that proxy paginated backend GET list endpoints.
 * Reads page/pageNumber and per_page/pageSize from request; returns normalized PaginatedResponse.
 */

import { NextRequest } from "next/server";
import { apiClient } from "@/lib/api/client";
import { normalizePaginatedResponse, type PaginatedResponse } from "@/lib/types/pagination";

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 10;
const MAX_PAGE_SIZE = 100;

export function getPageParams(request: NextRequest): { pageNumber: number; pageSize: number } {
  const { searchParams } = new URL(request.url);
  const pageParam = searchParams.get("pageNumber") ?? searchParams.get("page");
  const perPageParam = searchParams.get("pageSize") ?? searchParams.get("per_page");
  const pageNumber = Math.max(1, parseInt(pageParam ?? String(DEFAULT_PAGE), 10) || DEFAULT_PAGE);
  const pageSize = Math.min(MAX_PAGE_SIZE, Math.max(1, parseInt(perPageParam ?? String(DEFAULT_PAGE_SIZE), 10) || DEFAULT_PAGE_SIZE));
  return { pageNumber, pageSize };
}

export async function fetchPaginated<T>(
  path: string,
  request: NextRequest
): Promise<PaginatedResponse<T>> {
  const { pageNumber, pageSize } = getPageParams(request);
  const raw = await apiClient.get(path, { pageNumber, pageSize });
  return normalizePaginatedResponse(raw as PaginatedResponse<T>);
}
