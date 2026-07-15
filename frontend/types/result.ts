export interface ApiResult<T = unknown> {
  isSuccess: boolean;
  data?: T;
  error?: string;
  statusCode: number;
}

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
