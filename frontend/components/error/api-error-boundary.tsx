"use client";

import React, { Component, ReactNode } from "react";
import { ApiError } from "@/lib/api/helpers";

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ApiErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    if (error instanceof ApiError && typeof window !== "undefined") {
      const status = error.status;
      if (status === 401) {
        window.location.href = "/error/401";
        return;
      }
      if (status === 403) {
        window.location.href = "/error/403";
        return;
      }
      if (status === 404) {
        window.location.href = "/error/404";
        return;
      }
      if (status >= 500) {
        window.location.href = "/error/500";
        return;
      }
    }
}

  render() {
    if (this.state.hasError && this.state.error instanceof ApiError) {
      const status = this.state.error.status;
      if (status === 401 || status === 403 || status === 404 || status >= 500) {
        return (
          <div className="flex min-h-[200px] items-center justify-center bg-background text-foreground">
            <p className="text-sm text-muted-foreground">Redirecting...</p>
          </div>
        );
      }
    }
    if (this.state.hasError) {
      return this.props.fallback || <div>Something went wrong.</div>;
    }
    return this.props.children;
  }
}
