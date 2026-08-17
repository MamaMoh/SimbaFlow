"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

/** Legacy route — redirects to the full-page New Application form. */
export default function RegisterCandidateRedirectPage() {
  const router = useRouter();
  useEffect(() => {
    router.replace("/candidates/new");
  }, [router]);
  return null;
}
