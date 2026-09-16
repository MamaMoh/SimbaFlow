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
import { CandidateDocumentItems } from "@/components/workflow/candidate-document-items";
import { travelApi, type TravelBoardRow } from "@/lib/api/travel";
import { useAvailableActions } from "@/lib/api/workflow";
import { citiesFor, todayIso } from "@/lib/data/destination-cities";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { toast } from "sonner";
import {
  BellRing,
  CircleSlash,
  Eye,
  MoreHorizontal,
  Plane,
  PlaneTakeoff,
} from "lucide-react";
import Link from "next/link";

type Mode = "book-ticket" | "not-departed" | null;

type Props = {
  candidate: TravelBoardRow;
  onMutate: () => void;
  board: "ticket" | "departure";
  /** Stage this board represents; scopes the workflow buttons to it. */
  stageId?: string;
};

export function TravelRowActions({ candidate, onMutate, board, stageId }: Props) {
  const cities = citiesFor(candidate.countryOfTravel);
  const { hasPermission } = usePermissions();
  const canUpdate = hasPermission("travel.update") || hasPermission("system.admin");
  // The candidate's own page, and the paperwork below it, are read under candidate.read. Reaching
  // this board is a different permission, and holding one does not imply the other, so the link has
  // to ask for what the page it points at requires.
  const canRead = hasPermission("candidate.read");
  const { actions, mutate: mutateActions } = useAvailableActions(candidate.id, stageId);
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
        <DropdownMenu modal={false}>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" className="h-8 w-8 p-0" aria-label="Row actions">
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="z-[200] w-56">
            {canRead && (
              <>
                <DropdownMenuItem asChild>
                  <Link href={`/candidates/${candidate.id}`}>
                    <Eye className="mr-2 h-4 w-4" />
                    View details
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
              </>
            )}
            <CandidateDocumentItems candidateId={candidate.id} />
            {canUpdate && ticketStatus !== "Booking Complete" && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={() => setMode("book-ticket")}>
                  <Plane className="mr-2 h-4 w-4" />
                  Book ticket
                </DropdownMenuItem>
              </>
            )}
            <WorkflowActionItems
              candidateId={candidate.id}
              actions={actions}
              onExecuted={refresh}
              separatorBefore
            />
          </DropdownMenuContent>
        </DropdownMenu>
        <StatusUpdateSheet
          open={mode === "book-ticket"}
          onOpenChange={(o) => !o && setMode(null)}
          title="Book ticket"
          description={candidate.fullName}
          fields={[
            // Free text produced "RIYADH"/"Riyadh"/"riyad" for the same place, so offer the cities
            // of the candidate's destination country. Unmapped countries keep a text box rather
            // than blocking the booking.
            cities.length > 0
              ? {
                  name: "destination",
                  label: `Destination city (${candidate.countryOfTravel})`,
                  type: "select" as const,
                  required: true,
                  options: cities.map((c) => ({ value: c, label: c })),
                }
              : { name: "destination", label: "Destination", type: "text" as const, required: true },
            // A flight cannot be booked into the past.
            { name: "flightDate", label: "Flight date", type: "date" as const, required: true, min: todayIso() },
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
            {canRead && (
              <>
                <DropdownMenuItem asChild>
                  <Link href={`/candidates/${candidate.id}`}>
                    <Eye className="mr-2 h-4 w-4" />
                    View details
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
              </>
            )}
            <CandidateDocumentItems candidateId={candidate.id} />
            {canRead && <DropdownMenuSeparator />}
            {notification !== "Notified" && (
              <DropdownMenuItem
                onClick={() =>
                  run(() => travelApi.markNotified(candidate.id), "Marked notified")
                }
              >
                <BellRing className="mr-2 h-4 w-4" />
                Mark notified
              </DropdownMenuItem>
            )}
            {notification === "Notified" && departureStatus !== "Departed" && (
              <DropdownMenuItem
                onClick={() =>
                  run(() => travelApi.confirmDeparted(candidate.id), "Confirmed departed")
                }
              >
                <PlaneTakeoff className="mr-2 h-4 w-4" />
                Confirm departed
              </DropdownMenuItem>
            )}
            {departureStatus !== "Departed" && (
              <DropdownMenuItem onClick={() => setMode("not-departed")}>
                <CircleSlash className="mr-2 h-4 w-4" />
                Not departed…
              </DropdownMenuItem>
            )}
            <WorkflowActionItems
              candidateId={candidate.id}
              actions={actions}
              onExecuted={refresh}
              separatorBefore
            />
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
