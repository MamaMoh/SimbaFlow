"use client";

import Link from "next/link";
import { Button } from "@/components/ui/button";
import { toast } from "sonner";
import { getPipelineCounts, getCandidateList } from "@/lib/demo/demo-data";
import { USE_MOCKS } from "@/lib/api/candidates-api";
import { Download } from "lucide-react";

export default function ReportsPage() {
  const counts = USE_MOCKS ? getPipelineCounts() : [];
  const overdue = USE_MOCKS ? getCandidateList().filter((c) => c.isOverdue).length : 0;
  const total = USE_MOCKS ? getCandidateList().length : 0;

  const exportCsv = () => {
    const lines = ["Stage,Count,Overdue", ...counts.map((s) => `${s.name},${s.count},${s.overdue}`)];
    const blob = new Blob([lines.join("\n")], { type: "text/csv" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "pipeline-report.csv";
    a.click();
    URL.revokeObjectURL(url);
    toast.success("Pipeline report downloaded");
  };

  return (
    <div className="flex flex-col gap-5 p-4 md:p-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Reports & analytics</h1>
          <p className="text-sm text-muted-foreground">
            Live pipeline snapshot from demo data · {total} candidates
          </p>
        </div>
        <Button variant="outline" className="gap-1" onClick={exportCsv} disabled={!counts.length}>
          <Download className="h-4 w-4" /> Export CSV
        </Button>
      </div>
      <div className="grid gap-3 md:grid-cols-3">
        <div className="rounded-xl border bg-card p-4 shadow-sm">
          <div className="text-xs uppercase text-muted-foreground">Overdue</div>
          <div className="mt-1 text-3xl font-bold text-rose-700">{overdue}</div>
        </div>
        {counts.slice(0, 2).map((s) => (
          <Link
            key={s.id}
            href={`/workflow/${s.slug}`}
            className="rounded-xl border bg-card p-4 shadow-sm hover:bg-muted/30"
          >
            <div className="text-xs uppercase text-muted-foreground">{s.name}</div>
            <div className="mt-1 text-3xl font-bold tabular-nums">{s.count}</div>
          </Link>
        ))}
      </div>
      <div className="overflow-hidden rounded-xl border bg-card shadow-sm">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 text-left text-[11px] uppercase text-muted-foreground">
            <tr>
              <th className="p-3">Stage</th>
              <th className="p-3">Count</th>
              <th className="p-3">Overdue</th>
            </tr>
          </thead>
          <tbody>
            {counts.map((s) => (
              <tr key={s.id} className="border-t">
                <td className="p-3">
                  <Link href={`/workflow/${s.slug}`} className="font-medium hover:underline">
                    {s.name}
                  </Link>
                </td>
                <td className="p-3 tabular-nums font-semibold">{s.count}</td>
                <td className="p-3 tabular-nums text-rose-700">{s.overdue || "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
