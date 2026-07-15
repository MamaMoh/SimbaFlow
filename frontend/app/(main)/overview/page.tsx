"use client";

export default function OverviewPage() {
  return (
    <div className="flex flex-col gap-6 p-6">
      <h1 className="text-2xl font-semibold">Dashboard</h1>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="rounded-lg border p-6">
          <p className="text-sm text-muted-foreground">Total Candidates</p>
          <p className="text-3xl font-bold">0</p>
        </div>
        <div className="rounded-lg border p-6">
          <p className="text-sm text-muted-foreground">In Pipeline</p>
          <p className="text-3xl font-bold">0</p>
        </div>
        <div className="rounded-lg border p-6">
          <p className="text-sm text-muted-foreground">Departures This Month</p>
          <p className="text-3xl font-bold">0</p>
        </div>
        <div className="rounded-lg border p-6">
          <p className="text-sm text-muted-foreground">Revenue This Month</p>
          <p className="text-3xl font-bold">ETB 0</p>
        </div>
      </div>
      <div className="rounded-md border p-8 text-center text-muted-foreground">
        Pipeline funnel chart and analytics — coming with Unit 6 (Agency ERP)
      </div>
    </div>
  );
}
