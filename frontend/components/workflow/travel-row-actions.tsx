"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { StatusUpdateSheet } from "@/components/workflow/status-update-sheet";
import { WorkflowActionItems } from "@/components/workflow/workflow-action-items";
import { travelApi, type TravelBoardRow } from "@/lib/api/travel";
import { useAvailableActions } from "@/lib/api/workflow";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { toast } from "sonner";
import { Eye, MoreHorizontal } from "lucide-react";
import Link from "next/link";

type Mode = "book-ticket" | "not-departed" | null;

type Props = {
  candidate: TravelBoardRow;
  onMutate: () => void;
  board: "ticket" | "departure";
};

export function TravelRowActions({ candidate, onMutate, board }: Props) {
  const { hasPermission } = usePermissions();
  const canUpdate = hasPermission("travel.update") || hasPermission("system.admin");
  const { actions, mutate: mutateActions } = useAvailableActions(candidate.id);
  const [mode, setMode] = useState<Mode>(null);

  const s = candidate.statusValues ?? {};
  const ticketStatus = s.ticket_status ?? "";
  const notification = s.notification_status ?? "";
  const departureStatus = s.departure_status ?? "";
  const canceled = candidate.isCanceled || s.canceled === "true";

  const refresh = () => {
    mutateActions();
    onMutate();
  };

  const run = async (fn: () => Promise<void>, ok: string) => {
    try {
      await fn();
      toast.success(ok);
      refresh();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Failed");
      throw e;
    }
  };

  if (board === "ticket") {
    return (
      <div className="flex justify-center">
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" className="h-8 w-8 p-0" aria-label="Row actions">
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="z-[200] w-56">
            <DropdownMenuItem asChild>
              <Link href={`/candidates/${candidate.id}`}>
                <Eye className="mr-2 h-4 w-4" />
                View details
              </Link>
            </DropdownMenuItem>
            {canUpdate && ticketStatus !== "Booking Complete" && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={() => setMode("book-ticket")}>
                  Book ticket
                </DropdownMenuItem>
              </>
            )}
            {actions.length > 0 && <DropdownMenuSeparator />}
            <WorkflowActionItems
              candidateId={candidate.id}
              actions={actions}
              onExecuted={refresh}
            />
          </DropdownMenuContent>
        </DropdownMenu>
        <StatusUpdateSheet
          open={mode === "book-ticket"}
          onOpenChange={(o) => !o && setMode(null)}
          title="Book ticket"
          description={candidate.fullName}
          fields={[
            { name: "destination", label: "Destination", type: "text", required: true },
            { name: "flightDate", label: "Flight date", type: "date", required: true },
            { name: "ticketRef", label: "Ticket ref", type: "text" },
          ]}
          submitLabel="Save booking"
          onSubmit={async (v) => {
            await run(
              () =>
                travelApi.bookTicket(
                  candidate.id,
                  v.destination,
                  v.flightDate,
                  v.ticketRef || undefined
                ),
              "Ticket booked"
            );
          }}
        />
      </div>
    );
  }

  return (
    <div className="flex justify-center">
      {canUpdate && !canceled && (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" className="h-8 w-8 p-0" aria-label="Row actions">
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="z-[200] w-56">
            <DropdownMenuItem asChild>
              <Link href={`/candidates/${candidate.id}`}>
                <Eye className="mr-2 h-4 w-4" />
                View details
              </Link>
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            {notification !== "Notified" && (
              <DropdownMenuItem
                onClick={() =>
                  run(() => travelApi.markNotified(candidate.id), "Marked notified")
                }
              >
                Mark notified
              </DropdownMenuItem>
            )}
            {notification === "Notified" && departureStatus !== "Departed" && (
              <DropdownMenuItem
                onClick={() =>
                  run(() => travelApi.confirmDeparted(candidate.id), "Confirmed departed")
                }
              >
                Confirm departed
              </DropdownMenuItem>
            )}
            {departureStatus !== "Departed" && (
              <DropdownMenuItem onClick={() => setMode("not-departed")}>
                Not departed…
              </DropdownMenuItem>
            )}
            {actions.length > 0 && <DropdownMenuSeparator />}
            <WorkflowActionItems candidateId={candidate.id} actions={actions} onExecuted={refresh} />
          </DropdownMenuContent>
        </DropdownMenu>
      )}
      <StatusUpdateSheet
        open={mode === "not-departed"}
        onOpenChange={(o) => !o && setMode(null)}
        title="Not departed"
        description="Choose reason, then Back to Ticket or Cancel departure"
        fields={[
          {
            name: "reason",
            label: "Reason",
            type: "select",
            required: true,
            options: [
              { value: "MissedFlight", label: "Missed flight" },
              { value: "Immigration", label: "Immigration" },
              { value: "Medical", label: "Medical" },
              { value: "CandidateNoShow", label: "Candidate no-show" },
              { value: "AirlineCancel", label: "Airline cancel" },
              { value: "Other", label: "Other" },
            ],
          },
          { name: "reasonOther", label: "Other details", type: "text" },
          {
            name: "outcome",
            label: "Outcome",
            type: "select",
            required: true,
            options: [
              { value: "BackToTicket", label: "Back to Ticket (rebook)" },
              { value: "CancelDeparture", label: "Cancel departure" },
            ],
          },
        ]}
        submitLabel="Confirm"
        onSubmit={async (v) => {
          if (v.reason === "Other" && !v.reasonOther?.trim()) {
            throw new Error("Other details required");
          }
          await run(
            () =>
              travelApi.recordNotDeparted(
                candidate.id,
                v.reason,
                v.outcome as "BackToTicket" | "CancelDeparture",
                v.reasonOther || undefined
              ),
            v.outcome === "CancelDeparture" ? "Departure canceled" : "Sent back to Ticket"
          );
        }}
      />
    </div>
  );
}
