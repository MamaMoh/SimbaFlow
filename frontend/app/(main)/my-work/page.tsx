"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { ArrowRight, Loader2 } from "lucide-react";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import {
  MY_WORK_PERMISSIONS,
  totalNeedsAction,
  useMyWorkQueues,
} from "@/lib/api/work-queues";
import { ANY, MyWorkBoard } from "@/components/my-work/my-work-board";
import { PageHeader } from "@/components/ui/page-header";
import { Button } from "@/components/ui/button";

const TAB_KEY = "simbaflow:my-work-tab";

function readTab(): string | null {
  try {
    return window.localStorage.getItem(TAB_KEY);
  } catch {
    return null;
  }
}

function saveTab(key: string) {
  try {
    window.localStorage.setItem(TAB_KEY, key);
  } catch {
    // Private windows and blocked storage: the page still works, it just opens on the first tab.
  }
}

export default function MyWorkPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canRead = MY_WORK_PERMISSIONS.some((p) => hasPermission(p));

  const [partner, setPartner] = useState(ANY);
  const [office, setOffice] = useState(ANY);
  const [tab, setTab] = useState<string | null>(null);

  useEffect(() => {
    setTab(readTab());
  }, []);

  const { data, error, isLoading, mutate } = useMyWorkQueues(
    {
      // Partner and office are asked of the API because they narrow the whole tab; the search
      // box on each table is answered in the browser, over rows it already holds.
      partner: partner === ANY ? undefined : partner,
      office: office === ANY ? undefined : office,
    },
    !permsLoading && canRead
  );

  const queues = useMemo(() => data?.queues ?? [], [data]);
  const active = queues.find((q) => q.key === tab) ?? queues[0];

  const selectTab = (key: string) => {
    setTab(key);
    saveTab(key);
  };

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  if (!canRead) return <AccessDenied resource="your work queues" />;

  return (
    <div className="flex flex-col gap-6">
      {/* The count is everything this desk owes across every tab — the same number the sidebar
          badge carries, so the page confirms what brought the person to it. The board the open
          tab came from is the page's one action, which is where every other page puts it. */}
      <PageHeader
        title="My work"
        count={totalNeedsAction(data)}
        actions={
          active && (
            <Button asChild variant="outline" size="sm" className="gap-1.5">
              <Link href={active.boardHref}>
                Open {active.label} board
                <ArrowRight className="h-3.5 w-3.5" />
              </Link>
            </Button>
          )
        }
      />

      {error && <LoadError message={error.message} onRetry={() => mutate()} />}

      {isLoading && !data ? (
        <div className="flex items-center justify-center rounded-lg border bg-card p-12 text-sm text-muted-foreground shadow-sm">
          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          Loading your queues…
        </div>
      ) : !active ? (
        <div className="rounded-lg border bg-card p-12 text-center text-sm text-muted-foreground shadow-sm">
          Your role has no workflow boards yet. Ask an administrator to add one to your role.
        </div>
      ) : (
        <MyWorkBoard
          queues={queues}
          active={active}
          onSelectTab={selectTab}
          partners={data?.partners ?? []}
          partner={partner}
          onPartner={setPartner}
          offices={data?.offices ?? []}
          office={office}
          onOffice={setOffice}
          refreshing={isLoading && !!data}
        />
      )}
    </div>
  );
}
