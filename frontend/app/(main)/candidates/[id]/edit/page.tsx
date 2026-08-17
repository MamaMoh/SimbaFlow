"use client";

import { useParams } from "next/navigation";
import { CandidateApplicationForm } from "@/components/candidates/create-candidate-sheet";
import { AccessDenied } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";

export default function EditCandidateApplicationPage() {
  const { id } = useParams<{ id: string }>();
  const { hasPermission, isLoading } = usePermissions();
  const canWrite =
    hasPermission("candidate.write") ||
    hasPermission("candidate.update") ||
    hasPermission("system.admin");

  if (!isLoading && !canWrite) {
    return (
      <div className="p-6">
        <AccessDenied resource="candidates" />
      </div>
    );
  }

  return <CandidateApplicationForm candidateId={id} />;
}
