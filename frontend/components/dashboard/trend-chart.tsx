"use client";

import {
  Area,
  AreaChart,
  CartesianGrid,
  XAxis,
  YAxis,
} from "recharts";
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  ChartLegend,
  ChartLegendContent,
  type ChartConfig,
} from "@/components/ui/chart";
import type { TrendPoint } from "@/lib/api/dashboard";
import { Loader2 } from "lucide-react";

const config = {
  registered: { label: "Registered", color: "var(--chart-1)" },
  commissions: { label: "Commissions", color: "var(--chart-2)" },
  exceptions: { label: "Exceptions", color: "var(--chart-5)" },
} satisfies ChartConfig;

export function TrendChart({
  data,
  isLoading,
}: {
  data?: TrendPoint[];
  isLoading?: boolean;
}) {
  return (
    <div className="rounded-xl border bg-card p-5 shadow-sm">
      <div className="mb-4">
        <h2 className="text-base font-semibold">Intake & outcomes</h2>
        <p className="text-sm text-muted-foreground">
          Registrations, commissions and exceptions over the last 12 months
        </p>
      </div>

      {isLoading ? (
        <div className="flex h-[260px] items-center justify-center text-sm text-muted-foreground">
          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          Loading trends…
        </div>
      ) : !data || data.length === 0 ? (
        <div className="flex h-[260px] items-center justify-center text-sm text-muted-foreground">
          No trend data yet.
        </div>
      ) : (
        <ChartContainer config={config} className="h-[260px] w-full">
          <AreaChart data={data} margin={{ left: 4, right: 8, top: 8 }}>
            <defs>
              {Object.entries(config).map(([key, v]) => (
                <linearGradient
                  key={key}
                  id={`fill-${key}`}
                  x1="0"
                  y1="0"
                  x2="0"
                  y2="1"
                >
                  <stop offset="5%" stopColor={v.color} stopOpacity={0.35} />
                  <stop offset="95%" stopColor={v.color} stopOpacity={0.04} />
                </linearGradient>
              ))}
            </defs>
            <CartesianGrid vertical={false} strokeDasharray="3 3" opacity={0.4} />
            <XAxis
              dataKey="month"
              tickLine={false}
              axisLine={false}
              tickMargin={8}
              fontSize={11}
              tickFormatter={(v: string) => v.split(" ")[0]}
            />
            <YAxis
              tickLine={false}
              axisLine={false}
              width={28}
              fontSize={11}
              allowDecimals={false}
            />
            <ChartTooltip content={<ChartTooltipContent />} />
            <ChartLegend content={<ChartLegendContent />} />
            {Object.entries(config).map(([key, v]) => (
              <Area
                key={key}
                dataKey={key}
                type="monotone"
                stroke={v.color}
                fill={`url(#fill-${key})`}
                strokeWidth={2}
                stackId={undefined}
              />
            ))}
          </AreaChart>
        </ChartContainer>
      )}
    </div>
  );
}
