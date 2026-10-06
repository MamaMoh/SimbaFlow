import { cn } from "@/lib/utils";

export type PageHeaderProps = {
  title: React.ReactNode;
  /**
   * Only when it carries something the title cannot: which record this is, a
   * date, a status. Never a restatement of the title — the sidebar already said
   * where we are, and a second sentence saying it again is noise on every page.
   */
  description?: React.ReactNode;
  /** Record count for a list page. Shown as a number, not a sentence. */
  count?: number;
  /** Right-aligned action slot — primary action goes here, on every page. */
  actions?: React.ReactNode;
  className?: string;
};

/**
 * Standard page header: title on the left, primary actions on the right.
 *
 * A list page passes `count` rather than writing "· 142 candidates" into the
 * description. The number is the only part a person reads, and as a prose
 * fragment it had to be re-pluralised and re-worded on every board.
 */
export function PageHeader({
  title,
  description,
  count,
  actions,
  className,
}: PageHeaderProps) {
  return (
    <div className={cn("flex flex-wrap items-center justify-between gap-3", className)}>
      <div className="min-w-0">
        <div className="flex items-baseline gap-2">
          <h1 className="truncate text-xl font-semibold tracking-tight">{title}</h1>
          {count !== undefined && (
            <span className="shrink-0 text-sm font-normal tabular-nums text-muted-foreground">
              {count.toLocaleString()}
            </span>
          )}
        </div>
        {description && (
          <p className="mt-0.5 truncate text-sm text-muted-foreground">{description}</p>
        )}
      </div>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </div>
  );
}
