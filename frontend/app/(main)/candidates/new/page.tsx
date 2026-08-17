"use client";

import { CandidateApplicationForm } from "@/components/candidates/create-candidate-sheet";
import { AccessDenied } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";

export default function NewCandidateApplicationPage() {
  const { hasPermission, isLoading } = usePermissions();
  const canWrite =
    hasPermission("candidate.write") ||
    hasPermission("candidate.create") ||
    hasPermission("system.admin");

  if (!isLoading && !canWrite) {
    return (
      <div className="p-6">
        <AccessDenied resource="candidates" />
      </div>
    );
  }

  return <CandidateApplicationForm />;
}
