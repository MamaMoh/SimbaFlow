"use client";

export default function AccountingPage() {
  return (
    <div className="flex flex-col gap-5 p-4 md:p-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Accounting</h1>
        <p className="text-sm text-muted-foreground">
          Double-entry finance module lands in Unit 5 — commission stage is live in the pipeline.
        </p>
      </div>
      <div className="rounded-xl border border-dashed bg-muted/20 p-10 text-center text-sm text-muted-foreground">
        Journal entries, multi-currency ledgers, and statements will appear here.
      </div>
    </div>
  );
}
