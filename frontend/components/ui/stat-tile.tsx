import Link from "next/link";
import { cn } from "@/lib/utils";

export type StatTone = "plain" | "good" | "warn" | "bad";

const TONE: Record<StatTone, string> = {
  plain: "",
  good: "text-emerald-700 dark:text-emerald-400",
  warn: "text-amber-700 dark:text-amber-400",
  bad: "text-red-700 dark:text-red-400",
};

export type StatTileProps = {
  label: string;
  value: React.ReactNode;
  /** One short line under the value. Not a sentence about the feature. */
  detail?: React.ReactNode;
  icon?: React.ElementType;
  /** Tailwind classes for the icon chip, when a tile wants a colour. */
  accent?: string;
  /** Colours the number itself — reserved for when it is bad news. */
  tone?: StatTone;
  /** Makes the whole tile a link to the list it counts. */
  href?: string;
  /** Pulses the value while the number is still arriving. */
  isLoading?: boolean;
  className?: string;
};

/**
 * One number, on a card, the same everywhere.
 *
 * There were five of these: the dashboard, My work, Compliance, a partner's
 * page and Subscriptions each drew their own, so the label sat above the value
 * on one page and below it on the next, and the same count was bold here and
 * semibold there. Label, number, then at most one line under it.
 */
export function StatTile({
  label,
  value,
  detail,
  icon: Icon,
  accent,
  tone = "plain",
  href,
  isLoading,
  className,
}: StatTileProps) {
  const body = (
    <>
      <div className="flex items-start justify-between gap-2">
        <p className="text-xs text-muted-foreground">{label}</p>
        {Icon && (
          <span
            className={cn(
              "inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-md",
              accent ?? "bg-muted text-muted-foreground"
            )}
          >
            <Icon className="h-4 w-4" />
          </span>
        )}
      </div>
      <p
        className={cn(
          "mt-1 text-2xl font-semibold tabular-nums tracking-tight",
          TONE[tone],
          isLoading && "animate-pulse text-muted-foreground"
        )}
      >
        {value}
      </p>
      {detail && <p className="mt-0.5 text-xs text-muted-foreground">{detail}</p>}
    </>
  );

  const shell = cn("rounded-lg border bg-card p-4 shadow-sm", className);

  return href ? (
    <Link href={href} className={cn(shell, "block transition hover:border-primary/40 hover:shadow-md")}>
      {body}
    </Link>
  ) : (
    <div className={shell}>{body}</div>
  );
}
