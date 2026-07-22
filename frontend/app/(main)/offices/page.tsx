"use client";

import useSWR from "swr";
import { officesApi } from "@/lib/api/candidates-api";
import { DEMO_OFFICES } from "@/lib/demo/demo-data";

export default function OfficesPage() {
  const { data } = useSWR("offices", () => officesApi.list());
  const offices = data?.data?.length ? data.data : DEMO_OFFICES;

  return (
    <div className="flex flex-col gap-5 p-4 md:p-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Offices</h1>
        <p className="text-sm text-muted-foreground">Branch offices within this agency</p>
      </div>
      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
        {offices.map((o) => (
          <div key={o.id} className="rounded-xl border bg-card p-4 shadow-sm">
            <div className="font-semibold">{o.name}</div>
            <div className="mt-1 text-xs font-mono text-muted-foreground">{o.code}</div>
            <div className="mt-3 space-y-1 text-sm text-muted-foreground">
              <div>{o.city}</div>
              <div>{o.phone}</div>
              <div>{o.email}</div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
