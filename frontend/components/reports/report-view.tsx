"use client";

import { Loader2, FileSpreadsheet, FileText, BarChart3 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoadError } from "@/components/ui/page-alert";
import {
  useReport,
  reportExportUrl,
  formatCell,
  type ReportColumnType,
} from "@/lib/api/reports";
import { ReportChart } from "@/components/reports/report-chart";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

function download(url: string) {
  const a = document.createElement("a");
  a.href = url;
  a.rel = "noopener";
  document.body.appendChild(a);
  a.click();
  a.remove();
}

export function ReportView({
  reportKey,
  canExport,
}: {
  reportKey: string;
  canExport: boolean;
}) {
  const { data, error, isLoading, mutate } = useReport(reportKey);

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center rounded-xl border bg-card text-sm text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading report…
      </div>
    );
  }

  if (error) {
    return <LoadError message={error.message} onRetry={() => mutate()} />;
  }

  if (!data) return null;

  const numeric: ReportColumnType[] = ["Number", "Money", "Percent"];

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">{data.title}</h2>
          {data.subtitle && (
            <p className="text-sm text-muted-foreground">{data.subtitle}</p>
          )}
        </div>
        {canExport && (
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => download(reportExportUrl(reportKey, "excel"))}
            >
              <FileSpreadsheet className="mr-1.5 h-4 w-4" />
              Excel
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => download(reportExportUrl(reportKey, "pdf"))}
            >
              <FileText className="mr-1.5 h-4 w-4" />
              PDF
            </Button>
          </div>
        )}
      </div>

      {data.chartLabelKey && data.chartValueKey && data.rows.length > 0 && (
        <div className="rounded-xl border bg-card p-5 shadow-sm">
          <ReportChart report={data} />
        </div>
      )}

      <div className="overflow-x-auto rounded-xl border bg-card shadow-sm">
        <Table>
          <TableHeader>
            <TableRow>
              {data.columns.map((c) => (
                <TableHead key={c.key}
                  className={`px-4 py-2.5 font-medium text-muted-foreground ${
                    numeric.includes(c.type) ? "text-right" : ""
                  }`}>
                  {c.label}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.rows.length === 0 ? (
              <TableRow>
                <TableCell colSpan={data.columns.length} className="py-10 text-center text-muted-foreground">
                  <BarChart3 className="mx-auto mb-2 h-6 w-6 opacity-40" />
                  No data for this report yet.
                </TableCell>
              </TableRow>
            ) : (
              data.rows.map((row, i) => (
                <TableRow key={i}>
                  {data.columns.map((c) => (
                    <TableCell key={c.key}
                      className={`px-4 py-2.5 ${
                        numeric.includes(c.type)
                          ? "text-right tabular-nums"
                          : ""
                      }`}>
                      {formatCell(row[c.key], c.type)}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
