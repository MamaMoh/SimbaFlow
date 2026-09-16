"use client";

import { AlertTriangle, CalendarClock, Lock } from "lucide-react";
import { useMySubscription } from "@/lib/api/subscriptions";
import { usePermissions } from "@/lib/tenant/tenant-provider";

function money(amount: number, currency: string) {
  return `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 0 })}`;
}

function onDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, {
    day: "numeric",
    month: "long",
    year: "numeric",
  });
}

/**
 * Tells an agency where it stands on paying for the system.
 *
 * Nothing is shown until there is something to act on — a banner that is always there is one
 * nobody reads, which is the whole reason for the warning window rather than a permanent notice.
 * Suspension is the exception: that one has to be visible on every page, because every other page
 * has stopped working and this is the only thing that says why.
 */
export function RenewalNotice() {
  const { isSuperAdmin } = usePermissions();
  // A platform admin has no subscription of their own to renew.
  const { subscription } = useMySubscription(!isSuperAdmin);

  if (!subscription) return null;

  const { status, notice, nextPaymentDue, daysUntilDue, amount, currency, outstanding } =
    subscription;

  if (status !== "Active") {
    return (
      <div
        role="alert"
        className="flex items-start gap-3 rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-900"
      >
        <Lock className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
        <div className="min-w-0">
          <p className="font-medium">
            {status === "Suspended"
              ? "This agency is suspended"
              : "This agency has been deactivated"}
          </p>
          <p className="mt-0.5 text-red-800">
            Candidate records and the workflow boards are unavailable until it is reactivated.
            {outstanding.length > 0 ? (
              <>
                {" "}
                {outstanding.length === 1 ? "An invoice is" : `${outstanding.length} invoices are`}{" "}
                outstanding — {money(
                  outstanding.reduce((sum, i) => sum + i.amount, 0),
                  outstanding[0].currency,
                )}
                .
              </>
            ) : null}{" "}
            Contact your administrator to restore access.
          </p>
        </div>
      </div>
    );
  }

  if (notice === "None" || !nextPaymentDue) return null;

  const overdue = notice === "Overdue";
  const late = daysUntilDue !== null ? Math.abs(daysUntilDue) : 0;

  return (
    <div
      role="status"
      className={`flex items-start gap-3 rounded-lg border px-4 py-3 text-sm ${
        overdue
          ? "border-red-300 bg-red-50 text-red-900"
          : "border-amber-300 bg-amber-50 text-amber-900"
      }`}
    >
      {overdue ? (
        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
      ) : (
        <CalendarClock className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
      )}
      <div className="min-w-0">
        <p className="font-medium">
          {overdue
            ? `Subscription payment is ${late} ${late === 1 ? "day" : "days"} overdue`
            : notice === "DueToday"
              ? "Subscription payment is due today"
              : `Subscription renews in ${daysUntilDue} ${daysUntilDue === 1 ? "day" : "days"}`}
        </p>
        <p className="mt-0.5 opacity-90">
          {money(amount, currency)} due {onDate(nextPaymentDue)}.
          {outstanding.length > 0 ? ` Invoice ${outstanding[0].number}.` : ""}
          {overdue ? " Access continues for now, but settle this to avoid interruption." : ""}
        </p>
      </div>
    </div>
  );
}
