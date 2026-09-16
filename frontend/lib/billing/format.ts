/** Shared formatting for anything that shows money or invoice state. */

export function money(amount: number, currency: string) {
  return `${currency} ${amount.toLocaleString(undefined, {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  })}`;
}

export function onDate(iso: string | null | undefined) {
  if (!iso) return "—";
  return new Date(iso).toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

/** Colour for an invoice's state. Void is deliberately quiet — it is history, not a problem. */
export function statusTone(status: string) {
  switch (status) {
    case "Paid":
      return { className: "border-green-300 bg-green-50 text-green-800" };
    case "Issued":
      return { className: "border-amber-300 bg-amber-50 text-amber-800" };
    case "Void":
      return { className: "border-muted bg-muted/50 text-muted-foreground line-through" };
    default:
      return { className: "border-muted bg-muted/50 text-muted-foreground" };
  }
}
