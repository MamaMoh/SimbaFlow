"use client";

import { useMemo, useState } from "react";
import {
  type ColumnDef,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  useReactTable,
} from "@tanstack/react-table";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { DataTable } from "@/components/data-table/data-table";
import { indexColumn } from "@/components/data-table/index-column";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { LoadError } from "@/components/ui/page-alert";
import { botApi, useAgencyBotLinks, type AgencyBotChatLink } from "@/lib/api/bot";
import {
  DORMANT_DAYS,
  daysSinceSeen,
  describeLink,
  lastSeenLabel,
  staffName,
} from "@/lib/bot/chat-links";
import { formatDateTime } from "@/lib/utils";
import { Loader2, MoreHorizontal, Unlink } from "lucide-react";

/**
 * Everyone the agency's bot talks to, and the owner's switch for cutting a link.
 *
 * The bot answers these phones with the agency's own candidate data, so the person who answers
 * for the agency is the one who has to be able to see the list. Before this nobody could: not
 * the owner, and not even the people themselves. A member of staff who left with the bot still
 * on their phone kept receiving every stage change, and there was no screen anywhere that would
 * have shown it.
 *
 * Telegram never gives a bot the phone number, so there is no number to list — the @handle and
 * the name on the Telegram account are what identify a link, with "last used" for finding the
 * ones nobody is using.
 */
export function AgencyBotLinks() {
  const { data, error, isLoading, mutate } = useAgencyBotLinks(true);
  const [pending, setPending] = useState<AgencyBotChatLink | null>(null);
  const [removing, setRemoving] = useState(false);

  const links = useMemo(() => data ?? [], [data]);

  const onConfirm = async () => {
    if (!pending) return;
    setRemoving(true);
    try {
      const result = await botApi.removeAgencyLink(pending.id);
      if (result?.isSuccess) {
        toast.success(`${describeLink(pending)} disconnected from ${staffName(pending)}`);
        setPending(null);
        mutate();
      } else {
        toast.error(result?.error || "Could not disconnect it");
      }
    } catch {
      toast.error("Could not disconnect it");
    } finally {
      setRemoving(false);
    }
  };

  const columns = useMemo<ColumnDef<AgencyBotChatLink>[]>(
    () => [
      indexColumn<AgencyBotChatLink>(),
      {
        id: "person",
        header: "Person",
        accessorFn: (link) => staffName(link),
        cell: ({ row }) => (
          <div className="min-w-0">
            <span className="font-medium">{staffName(row.original)}</span>
            {row.original.userName && (
              <p className="truncate text-xs text-muted-foreground">{row.original.userName}</p>
            )}
          </div>
        ),
      },
      {
        id: "telegram",
        header: "Telegram",
        accessorFn: (link) => describeLink(link),
        cell: ({ row }) => (
          <div className="min-w-0">
            <span className="text-sm">{describeLink(row.original)}</span>
            {row.original.telegramUsername && row.original.displayName && (
              <p className="truncate text-xs text-muted-foreground">
                {row.original.displayName}
              </p>
            )}
          </div>
        ),
      },
      {
        id: "linkedAt",
        header: "Linked",
        accessorFn: (link) => formatDateTime(link.linkedAt),
        cell: ({ getValue }) => (
          <span className="text-sm text-muted-foreground">{getValue() as string}</span>
        ),
      },
      {
        id: "lastSeen",
        header: "Last used",
        accessorFn: (link) => lastSeenLabel(link),
        cell: ({ row }) => {
          const days = daysSinceSeen(row.original);
          // A month of silence is the thing worth noticing: a replaced handset, or somebody who
          // has gone. Plain text otherwise, so the badge means something when it appears.
          const dormant = days === null || days >= DORMANT_DAYS;
          return dormant ? (
            <Badge variant="outline" className="text-amber-700 dark:text-amber-400">
              {lastSeenLabel(row.original)}
            </Badge>
          ) : (
            <span className="text-sm text-muted-foreground">{lastSeenLabel(row.original)}</span>
          );
        },
      },
      {
        id: "state",
        header: "Status",
        accessorFn: (link) =>
          !link.isActive ? "Account disabled" : link.muted ? "Muted" : "Receiving",
        cell: ({ row }) =>
          !row.original.isActive ? (
            // Still reachable by the bot while the sign-in is switched off, which is the pair of
            // facts an owner most needs to see next to each other.
            <Badge variant="destructive">Account disabled</Badge>
          ) : row.original.muted ? (
            <Badge variant="secondary">Muted</Badge>
          ) : (
            <Badge variant="outline">Receiving</Badge>
          ),
      },
      {
        id: "actions",
        header: () => <div className="text-center">Actions</div>,
        enableColumnFilter: false,
        cell: ({ row }) => (
          <div className="flex justify-center">
            <DropdownMenu modal={false}>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-8 w-8 p-0"
                  aria-label="Row actions"
                >
                  <MoreHorizontal className="h-4 w-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="z-[200] w-56">
                <DropdownMenuItem
                  className="text-destructive"
                  onClick={() => setPending(row.original)}
                >
                  <Unlink className="mr-2 h-4 w-4" /> Disconnect
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        ),
      },
    ],
    [],
  );

  const table = useReactTable({
    data: links,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    initialState: { pagination: { pageSize: 20 } },
  });

  return (
    <section className="space-y-3 rounded-lg border bg-card p-4 shadow-sm">
      <div>
        <h2 className="flex items-baseline gap-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          Linked to the bot
          <span className="tabular-nums font-normal">{links.length}</span>
        </h2>
        <p className="mt-0.5 text-xs text-muted-foreground">
          Every phone the agency&rsquo;s bot answers. One person may have several. Disconnecting
          one stops its notifications and its candidate lookups; the person can link again with a
          fresh code.
        </p>
      </div>

      {error ? (
        <LoadError message={error.message} onRetry={() => mutate()} />
      ) : isLoading ? (
        <div className="flex items-center justify-center p-8 text-muted-foreground">
          <Loader2 className="mr-2 h-4 w-4 animate-spin" /> Loading…
        </div>
      ) : (
        <DataTable
          table={table}
          paginated
          exportFileName="telegram-links"
          emptyMessage="Nobody has linked the bot yet."
        />
      )}

      <ConfirmDialog
        open={!!pending}
        onOpenChange={(open) => !open && setPending(null)}
        title={pending ? `Disconnect ${staffName(pending)}?` : "Disconnect?"}
        description={
          pending
            ? `${describeLink(pending)} stops receiving notifications and can no longer look candidates up from Telegram. ${staffName(pending)} can link again with a fresh code from their own settings.`
            : ""
        }
        actionLabel="Disconnect"
        isPending={removing}
        onConfirm={() => void onConfirm()}
      />
    </section>
  );
}
