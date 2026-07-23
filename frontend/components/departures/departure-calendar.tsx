"use client";

import { useMemo, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import FullCalendar from "@fullcalendar/react";
import type { CalendarApi, EventClickArg, EventContentArg } from "@fullcalendar/core";
import dayGridPlugin from "@fullcalendar/daygrid";
import timeGridPlugin from "@fullcalendar/timegrid";
import listPlugin from "@fullcalendar/list";
import interactionPlugin from "@fullcalendar/interaction";
import {
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Circle,
  ClipboardList,
  FileBarChart2,
  LayoutGrid,
  LayoutDashboard,
  MapPin,
  Plane,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { getDepartureCalendarEvents } from "@/lib/demo/demo-data";
import { cn } from "@/lib/utils";

const FLIGHT_COMMON_IMAGE = "/images/flight-common.svg";

const DEST_LABEL: Record<string, string> = {
  KSA: "Riyadh, KSA",
  UAE: "Dubai, UAE",
  Kuwait: "Kuwait City",
  Qatar: "Doha, Qatar",
  Bahrain: "Manama, Bahrain",
  Oman: "Muscat, Oman",
};

const STATUS_LABEL: Record<string, string> = {
  scheduled: "Scheduled",
  pending: "Pending ticket",
  completed: "Departed",
};

function maskPassport(p?: string) {
  if (!p || p.length < 6) return p || "—";
  return `${p.slice(0, 4)}***${p.slice(-2)}`;
}

function initials(name: string) {
  const parts = name.trim().split(/\s+/);
  if (parts.length >= 2) return `${parts[0]![0]}${parts[parts.length - 1]![0]}`.toUpperCase();
  return name.slice(0, 2).toUpperCase();
}

function formatFlightDateTime(iso: string | Date) {
  const d = new Date(iso);
  return {
    date: d.toLocaleDateString(undefined, {
      weekday: "short",
      year: "numeric",
      month: "short",
      day: "numeric",
    }),
    time: d.toLocaleTimeString(undefined, {
      hour: "2-digit",
      minute: "2-digit",
      timeZoneName: "short",
    }),
    full: d.toLocaleString(undefined, {
      dateStyle: "full",
      timeStyle: "short",
    }),
  };
}

function EventContent({ event }: EventContentArg) {
  const dest = event.extendedProps.destination as string | undefined;
  const stage = event.extendedProps.stageName as string | undefined;
  const time = new Date(event.start!).toLocaleTimeString(undefined, {
    hour: "2-digit",
    minute: "2-digit",
  });
  return (
    <div className="overflow-hidden px-1.5 py-1">
      <div className="truncate text-[11px] font-bold leading-tight tracking-wide">{event.title}</div>
      <div className="truncate text-[10px] opacity-90">
        {time} · {[dest, stage].filter(Boolean).join(" · ")}
      </div>
    </div>
  );
}

const SIDE_LINKS = [
  { id: "overview", label: "Overview", icon: LayoutDashboard, href: "/overview" },
  { id: "grid", label: "Monthly Grid", icon: LayoutGrid, href: "/departures/calendar" },
  { id: "manifests", label: "Flight Manifests", icon: ClipboardList, href: "/workflow/departures" },
  { id: "reports", label: "Status Reports", icon: FileBarChart2, href: "/reports" },
  { id: "arrivals", label: "Arrivals", icon: MapPin, href: "/workflow/arrivals" },
] as const;

export default function DepartureCalendar() {
  const router = useRouter();
  const calApiRef = useRef<CalendarApi | null>(null);
  const events = useMemo(() => getDepartureCalendarEvents(), []);
  const [selectedId, setSelectedId] = useState<string | null>(events[0]?.id ?? null);
  const [view, setView] = useState<"dayGridMonth" | "timeGridWeek" | "listWeek">("dayGridMonth");
  const [title, setTitle] = useState("");

  const selected = events.find((e) => e.id === selectedId) ?? null;
  const upcoming = events.filter((e) => e.extendedProps.status !== "completed").length;
  const departed = events.filter((e) => e.extendedProps.status === "completed").length;

  const onEventClick = (info: EventClickArg) => {
    info.jsEvent.preventDefault();
    info.jsEvent.stopPropagation();
    setSelectedId(info.event.id);
  };

  const destBanner =
    selected &&
    (DEST_LABEL[selected.extendedProps.destination] ||
      selected.extendedProps.destination ||
      "Gulf corridor");

  const flightWhen = selected ? formatFlightDateTime(selected.start) : null;

  return (
    <div className="-m-4 md:-m-6 lg:-m-8 flex min-h-[calc(100vh-4.5rem)] bg-muted/30">
      <aside className="hidden w-56 shrink-0 flex-col border-e border-border bg-card lg:flex">
        <div className="border-b border-border px-4 py-5">
          <div className="flex items-center gap-2">
            <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary text-primary-foreground">
              <Plane className="h-4 w-4" />
            </span>
            <div>
              <div className="text-[11px] font-bold uppercase tracking-wide">Departure Manager</div>
              <div className="text-[10px] text-muted-foreground">Labor export pipeline</div>
            </div>
          </div>
        </div>
        <nav className="flex-1 space-y-1 p-3">
          {SIDE_LINKS.map((link) => {
            const active = link.id === "grid";
            return (
              <Link
                key={link.id}
                href={link.href}
                className={cn(
                  "flex items-center gap-2.5 rounded-lg px-3 py-2.5 text-sm font-medium transition",
                  active
                    ? "bg-primary text-primary-foreground shadow-sm"
                    : "text-muted-foreground hover:bg-muted hover:text-foreground",
                )}
              >
                <link.icon className="h-4 w-4" />
                {link.label}
              </Link>
            );
          })}
        </nav>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-card px-4 py-4 md:px-6">
          <div>
            <h1 className="text-xl font-bold tracking-tight md:text-2xl">Departure schedule</h1>
            <p className="text-xs text-muted-foreground md:text-sm">
              Global departure schedule & logistics management
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <div className="inline-flex rounded-lg border border-border bg-background p-0.5 text-xs font-semibold">
              {(
                [
                  ["dayGridMonth", "Month"],
                  ["timeGridWeek", "Week"],
                  ["listWeek", "List"],
                ] as const
              ).map(([id, label]) => (
                <button
                  key={id}
                  type="button"
                  onClick={() => {
                    setView(id);
                    calApiRef.current?.changeView(id);
                  }}
                  className={cn(
                    "rounded-md px-3 py-1.5 transition",
                    view === id ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:text-foreground",
                  )}
                >
                  {label}
                </button>
              ))}
            </div>
            <div className="rounded-md bg-secondary/80 px-2.5 py-1.5 text-[11px] font-bold uppercase tracking-wide text-secondary-foreground">
              Upcoming: {String(upcoming).padStart(2, "0")}
            </div>
            <div className="rounded-md bg-primary px-2.5 py-1.5 text-[11px] font-bold uppercase tracking-wide text-primary-foreground">
              Departed: {String(departed).padStart(2, "0")}
            </div>
          </div>
        </div>

        <div className="grid min-h-0 flex-1 gap-0 xl:grid-cols-[minmax(0,1fr)_360px]">
          <div className="departure-fc flex min-h-[520px] flex-col overflow-hidden bg-card p-3 md:p-4">
            {/* Custom nav — Lucide arrows (FC icon font often fails under theme CSS) */}
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
              <div className="flex items-center gap-1">
                <Button
                  type="button"
                  variant="outline"
                  size="icon"
                  className="h-9 w-9"
                  aria-label="Previous"
                  onClick={() => calApiRef.current?.prev()}
                >
                  <ChevronLeft className="h-5 w-5" />
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="icon"
                  className="h-9 w-9"
                  aria-label="Next"
                  onClick={() => calApiRef.current?.next()}
                >
                  <ChevronRight className="h-5 w-5" />
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="ms-1 h-9"
                  onClick={() => calApiRef.current?.today()}
                >
                  Today
                </Button>
              </div>
              <h2 className="text-lg font-bold tracking-tight">{title || "…"}</h2>
            </div>

            <FullCalendar
              ref={(el) => {
                if (el) calApiRef.current = el.getApi();
              }}
              plugins={[dayGridPlugin, timeGridPlugin, listPlugin, interactionPlugin]}
              initialView={view}
              headerToolbar={false}
              height="auto"
              contentHeight={600}
              events={events}
              eventClick={onEventClick}
              eventContent={(arg) => <EventContent {...arg} />}
              datesSet={(arg) => setTitle(arg.view.title)}
              nowIndicator
              dayMaxEvents={3}
              navLinks={false}
              editable={false}
              selectable={false}
            />
          </div>

          <aside className="border-t border-border bg-card xl:border-s xl:border-t-0">
            <div className="border-b border-border px-4 py-3">
              <h2 className="font-semibold">Flight Details</h2>
            </div>

            {!selected || !flightWhen ? (
              <p className="p-6 text-sm text-muted-foreground">
                Select a departure on the calendar to inspect flight details.
              </p>
            ) : (
              <div className="flex flex-col gap-4 p-4">
                <div className="relative overflow-hidden rounded-xl">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={FLIGHT_COMMON_IMAGE}
                    alt="Flight"
                    className="aspect-[16/9] w-full object-cover"
                  />
                  <div className="absolute inset-0 bg-gradient-to-t from-foreground/70 to-transparent" />
                  <div className="absolute bottom-3 start-3 space-y-0.5 text-primary-foreground">
                    <div className="text-xs font-bold uppercase tracking-wide">
                      Destination · {destBanner}
                    </div>
                    <div className="text-[11px] font-medium opacity-90">
                      {flightWhen.date} · {flightWhen.time}
                    </div>
                  </div>
                </div>

                <div className="flex items-center gap-3">
                  <span className="flex h-11 w-11 items-center justify-center rounded-full bg-primary text-sm font-bold text-primary-foreground">
                    {initials(selected.extendedProps.fullName || selected.title)}
                  </span>
                  <div className="min-w-0">
                    <div className="truncate font-semibold">
                      {selected.extendedProps.fullName || selected.title}
                    </div>
                    <div className="font-mono text-xs text-muted-foreground">
                      {maskPassport(selected.extendedProps.passportNumber)}
                    </div>
                  </div>
                </div>

                <div className="rounded-lg border border-border bg-muted/40 px-3 py-3">
                  <div className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                    Departure date & time
                  </div>
                  <div className="mt-1 text-sm font-bold">{flightWhen.full}</div>
                </div>

                <div className="grid grid-cols-2 gap-2">
                  <div className="rounded-lg bg-muted/80 px-3 py-2.5">
                    <div className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                      Flight no.
                    </div>
                    <div className="mt-0.5 font-bold">{selected.extendedProps.flightNo}</div>
                  </div>
                  <div className="rounded-lg bg-muted/80 px-3 py-2.5">
                    <div className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                      Terminal
                    </div>
                    <div className="mt-0.5 font-bold">{selected.extendedProps.terminal}</div>
                  </div>
                  <div className="rounded-lg bg-muted/80 px-3 py-2.5">
                    <div className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                      Date
                    </div>
                    <div className="mt-0.5 text-sm font-bold leading-snug">{flightWhen.date}</div>
                  </div>
                  <div className="rounded-lg bg-muted/80 px-3 py-2.5">
                    <div className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                      Time
                    </div>
                    <div className="mt-0.5 text-sm font-bold leading-snug">{flightWhen.time}</div>
                  </div>
                </div>

                <dl className="space-y-2 text-sm">
                  {[
                    ["Status", STATUS_LABEL[selected.extendedProps.status] ?? selected.extendedProps.status],
                    ["Stage", selected.extendedProps.stageName],
                    ["App #", selected.extendedProps.applicationNo],
                    ["Labour ID", selected.extendedProps.labourId],
                    ["Destination", destBanner],
                    ["Sponsor", selected.extendedProps.sponsorName || "—"],
                    ["Office", selected.extendedProps.officeName],
                  ].map(([k, v]) => (
                    <div
                      key={k as string}
                      className="flex items-start justify-between gap-3 border-b border-border/60 pb-1.5"
                    >
                      <dt className="text-muted-foreground">{k}</dt>
                      <dd className="max-w-[60%] text-end font-medium">{(v as string) || "—"}</dd>
                    </div>
                  ))}
                </dl>

                <div>
                  <div className="mb-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
                    Process timeline
                  </div>
                  <ol className="space-y-4">
                    {[
                      {
                        label: "Visa Issued",
                        at: selected.extendedProps.visaIssuedAt,
                        done: true,
                      },
                      {
                        label: "Ticket Confirmed",
                        at: selected.extendedProps.ticketConfirmedAt,
                        done: selected.extendedProps.status !== "pending",
                      },
                      {
                        label: "Departure",
                        at: selected.start,
                        done: selected.extendedProps.status === "completed",
                      },
                    ].map((step) => {
                      const when = formatFlightDateTime(step.at);
                      return (
                        <li key={step.label} className="flex gap-3">
                          {step.done ? (
                            <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
                          ) : (
                            <Circle className="mt-0.5 h-5 w-5 shrink-0 text-muted-foreground/50" />
                          )}
                          <div>
                            <div className="text-sm font-semibold">{step.label}</div>
                            <div className="text-xs text-muted-foreground">
                              {when.date} · {when.time}
                            </div>
                          </div>
                        </li>
                      );
                    })}
                  </ol>
                </div>

                <div className="mt-auto pt-2">
                  <Button className="w-full" onClick={() => router.push(`/candidates/${selected.id}`)}>
                    Open candidate
                  </Button>
                </div>
              </div>
            )}
          </aside>
        </div>
      </div>

      <style jsx global>{`
        .departure-fc .fc {
          --fc-border-color: hsl(var(--border));
          --fc-today-bg-color: color-mix(in oklab, var(--primary) 8%, transparent);
          --fc-list-event-hover-bg-color: color-mix(in oklab, var(--primary) 14%, var(--card));
          font-family: inherit;
        }
        .departure-fc .fc .fc-toolbar {
          display: none;
        }
        .departure-fc .fc .fc-daygrid-day-number {
          font-weight: 600;
          font-size: 0.75rem;
          padding: 0.35rem;
        }
        .departure-fc .fc .fc-event {
          border: none !important;
          border-radius: 0.4rem;
          padding: 0;
          cursor: pointer;
        }
        .departure-fc .fc .fc-event.status-scheduled {
          background: color-mix(in oklab, var(--primary) 88%, black);
          color: white;
        }
        .departure-fc .fc .fc-event.status-pending {
          background: color-mix(in oklab, var(--secondary) 85%, white);
          color: var(--secondary-foreground);
        }
        .departure-fc .fc .fc-event.status-completed {
          background: color-mix(in oklab, var(--primary) 25%, white);
          color: var(--foreground);
        }

        /* List view: keep text readable on hover (avoid white-on-white) */
        .departure-fc .fc .fc-list-event {
          cursor: pointer;
        }
        .departure-fc .fc .fc-list-event td,
        .departure-fc .fc .fc-list-event .fc-list-event-time,
        .departure-fc .fc .fc-list-event .fc-list-event-title,
        .departure-fc .fc .fc-list-event .fc-list-event-graphic {
          color: var(--foreground) !important;
        }
        .departure-fc .fc .fc-list-event:hover td {
          background-color: color-mix(in oklab, var(--primary) 14%, var(--card)) !important;
          color: var(--foreground) !important;
        }
        .departure-fc .fc .fc-list-event:hover .fc-list-event-time,
        .departure-fc .fc .fc-list-event:hover .fc-list-event-title,
        .departure-fc .fc .fc-list-event:hover a {
          color: var(--foreground) !important;
        }
        .departure-fc .fc .fc-list-event .fc-list-event-dot {
          border-color: var(--primary) !important;
        }
        .departure-fc .fc .fc-list-event.status-pending .fc-list-event-dot {
          border-color: var(--secondary) !important;
        }
        .departure-fc .fc .fc-list-event.status-completed .fc-list-event-dot {
          border-color: color-mix(in oklab, var(--primary) 55%, #888) !important;
        }
      `}</style>
    </div>
  );
}
