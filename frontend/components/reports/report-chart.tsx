"use client";

import { Bar, BarChart, CartesianGrid, XAxis, YAxis, Cell } from "recharts";
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart";
import type { ReportTable } from "@/lib/api/reports";

const PALETTE = [
  "var(--chart-1)",
  "var(--chart-2)",
  "var(--chart-3)",
  "var(--chart-4)",
  "var(--chart-5)",
];

export function ReportChart({ report }: { report: ReportTable }) {
  if (!report.chartLabelKey || !report.chartValueKey) return null;

  const labelKey = report.chartLabelKey;
  const valueKey = report.chartValueKey;

  const data = report.rows.map((r) => ({
    label: String(r[labelKey] ?? "—"),
    value: Number(r[valueKey] ?? 0),
  }));

  if (data.length === 0) return null;

  const valueLabel =
    report.columns.find((c) => c.key === valueKey)?.label ?? "Value";

  const config = {
    value: { label: valueLabel, color: "var(--chart-1)" },
  } satisfies ChartConfig;

  return (
    <ChartContainer config={config} className="h-[260px] w-full">
      <BarChart data={data} margin={{ left: 4, right: 8, top: 8 }}>
        <CartesianGrid vertical={false} strokeDasharray="3 3" opacity={0.4} />
        <XAxis
          dataKey="label"
          tickLine={false}
          axisLine={false}
          tickMargin={8}
          fontSize={11}
          interval={0}
          angle={data.length > 6 ? -20 : 0}
          textAnchor={data.length > 6 ? "end" : "middle"}
          height={data.length > 6 ? 56 : 24}
        />
        <YAxis
          tickLine={false}
          axisLine={false}
          width={40}
          fontSize={11}
          allowDecimals={false}
        />
        <ChartTooltip content={<ChartTooltipContent />} />
        <Bar dataKey="value" radius={[4, 4, 0, 0]}>
          {data.map((_, i) => (
            <Cell key={i} fill={PALETTE[i % PALETTE.length]} />
          ))}
        </Bar>
      </BarChart>
    </ChartContainer>
  );
}
