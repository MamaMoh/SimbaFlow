"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";

/** Legacy sheet wrapper — edit now lives at /candidates/[id]/edit. */
export function EditCandidateSheet({
  candidate,
  open,
}: {
  candidate: { id: string; fullName: string } | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onUpdated: () => void;
}) {
  const router = useRouter();
  useEffect(() => {
    if (open && candidate?.id) {
      router.push(`/candidates/${candidate.id}/edit`);
    }
  }, [open, candidate?.id, router]);
  return null;
}
