/**
 * Pagination types aligned with backend Paginated<T> (StandardApiResponse unwraps to this).
 * Supports both camelCase and PascalCase from API.
 */
export interface PaginatedResponse<T> {
  data: T[];
  totalCount: number;
  totalPage: number;
  currentPage: number;
  pageSize: number;
  firstPage: number;
  lastPage: number;
}

/** Raw API shape (PascalCase or mixed). */
type RawPaginated<T> = {
  data?: T[];
  Data?: T[];
  totalCount?: number;
  TotalCount?: number;
  totalPage?: number;
  TotalPage?: number;
  currentPage?: number;
  CurrentPage?: number;
  pageSize?: number;
  PageSize?: number;
  firstPage?: number;
  FirstPage?: number;
  lastPage?: number;
  LastPage?: number;
};

/** Normalize paginated API response to camelCase. */
export function normalizePaginatedResponse<T>(raw: RawPaginated<T> | null | undefined): PaginatedResponse<T> {
  const data = raw?.data ?? raw?.Data ?? [];
  const totalCount = raw?.totalCount ?? raw?.TotalCount ?? 0;
  const totalPage = raw?.totalPage ?? raw?.TotalPage ?? 0;
  const currentPage = raw?.currentPage ?? raw?.CurrentPage ?? 1;
  const pageSize = raw?.pageSize ?? raw?.PageSize ?? 10;
  const firstPage = raw?.firstPage ?? raw?.FirstPage ?? 1;
  const lastPage = raw?.lastPage ?? raw?.LastPage ?? 1;
  return {
    data: Array.isArray(data) ? data : [],
    totalCount,
    totalPage,
    currentPage,
    pageSize,
    firstPage,
    lastPage,
  };
}

/** Build query string from params; skips undefined/null. */
export function buildQueryString(params: Record<string, string | number | undefined | null>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") {
      search.set(key, String(value));
    }
  }
  const qs = search.toString();
  return qs ? `?${qs}` : "";
}
