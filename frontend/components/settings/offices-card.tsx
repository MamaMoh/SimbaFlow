"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Loader2, Pencil, Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  createOffice,
  deleteOffice,
  updateOffice,
  useOffices,
  type Office,
} from "@/lib/api/offices";

type Draft = { name: string; city: string; phone: string };

const blank: Draft = { name: "", city: "", phone: "" };

/**
 * The agency's branches.
 *
 * Each candidate is registered by one of these, and the Office comparison report groups on it.
 * An office with candidates cannot be deleted, only closed: deleting it would move all of them to
 * "No office" in every report, while closing it keeps the history and stops new registrations.
 */
export function OfficesCard({ canEdit }: { canEdit: boolean }) {
  const { offices, mutate, isLoading, error } = useOffices();
  const [adding, setAdding] = useState<Draft>(blank);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editing, setEditing] = useState<Draft>(blank);
  const [busy, setBusy] = useState<string | null>(null);

  const run = async (key: string, action: () => Promise<void>, done: string) => {
    setBusy(key);
    try {
      await action();
      await mutate();
      toast.success(done);
      return true;
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not save");
      return false;
    } finally {
      setBusy(null);
    }
  };

  const add = async () => {
    const ok = await run(
      "add",
      () => createOffice({ name: adding.name, city: adding.city || null, phone: adding.phone || null }),
      `${adding.name.trim()} added`,
    );
    if (ok) setAdding(blank);
  };

  const startEdit = (office: Office) => {
    setEditingId(office.id);
    setEditing({ name: office.name, city: office.city ?? "", phone: office.phone ?? "" });
  };

  const saveEdit = async (office: Office) => {
    const ok = await run(
      office.id,
      () =>
        updateOffice(office.id, {
          name: editing.name,
          city: editing.city || null,
          phone: editing.phone || null,
        }),
      "Office saved",
    );
    if (ok) setEditingId(null);
  };

  const toggleOpen = (office: Office) =>
    run(
      office.id,
      () =>
        updateOffice(office.id, {
          name: office.name,
          city: office.city,
          phone: office.phone,
          isActive: !office.isActive,
        }),
      office.isActive ? `${office.name} closed` : `${office.name} reopened`,
    );

  const remove = (office: Office) =>
    run(office.id, () => deleteOffice(office.id), `${office.name} removed`);

  return (
    <section className="space-y-4 rounded-lg border bg-card p-5 shadow-sm">
      <div>
        <h2 className="text-sm font-semibold">Offices</h2>
        <p className="text-muted-foreground mt-0.5 text-xs">
          The branches that register candidates. Each candidate is filed under one, and the Office
          comparison report is grouped by them.
        </p>
      </div>

      {error ? (
        <p className="text-destructive text-xs">{error.message}</p>
      ) : isLoading ? (
        <p className="text-muted-foreground text-xs">Loading…</p>
      ) : (
        <>
          {offices && offices.length > 0 ? (
            <ul className="divide-y rounded-md border">
              {offices.map((office) => (
                <li key={office.id} className="flex flex-wrap items-center gap-3 px-3 py-2.5">
                  {editingId === office.id ? (
                    <>
                      <Input
                        className="h-8 w-full sm:w-48"
                        value={editing.name}
                        aria-label="Office name"
                        onChange={(e) => setEditing((d) => ({ ...d, name: e.target.value }))}
                      />
                      <Input
                        className="h-8 w-full sm:w-36"
                        value={editing.city}
                        placeholder="City"
                        aria-label="City"
                        onChange={(e) => setEditing((d) => ({ ...d, city: e.target.value }))}
                      />
                      <Input
                        className="h-8 w-full sm:w-36"
                        value={editing.phone}
                        placeholder="Phone"
                        aria-label="Phone"
                        onChange={(e) => setEditing((d) => ({ ...d, phone: e.target.value }))}
                      />
                      <div className="ml-auto flex gap-2">
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => setEditingId(null)}
                          disabled={busy === office.id}
                        >
                          Cancel
                        </Button>
                        <Button
                          size="sm"
                          onClick={() => saveEdit(office)}
                          disabled={busy === office.id || !editing.name.trim()}
                        >
                          {busy === office.id ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : null}
                          Save
                        </Button>
                      </div>
                    </>
                  ) : (
                    <>
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-medium">
                          {office.name}
                          {office.isActive ? null : (
                            <span className="text-muted-foreground ml-2 text-xs font-normal">
                              Closed
                            </span>
                          )}
                        </p>
                        <p className="text-muted-foreground truncate text-xs">
                          {[office.city, office.phone].filter(Boolean).join(" · ") || "No details"}
                          {" · "}
                          {office.candidates === 1
                            ? "1 active candidate"
                            : `${office.candidates} active candidates`}
                        </p>
                      </div>
                      {canEdit ? (
                        <div className="flex items-center gap-1">
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() => startEdit(office)}
                            disabled={busy !== null}
                            aria-label={`Edit ${office.name}`}
                          >
                            <Pencil className="h-3.5 w-3.5" />
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => toggleOpen(office)}
                            disabled={busy !== null}
                          >
                            {office.isActive ? "Close" : "Reopen"}
                          </Button>
                          {office.candidates === 0 ? (
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => remove(office)}
                              disabled={busy !== null}
                              aria-label={`Delete ${office.name}`}
                            >
                              <Trash2 className="h-3.5 w-3.5" />
                            </Button>
                          ) : null}
                        </div>
                      ) : null}
                    </>
                  )}
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-muted-foreground text-xs">
              No offices yet. Until one is added, every candidate is reported under &quot;No
              office&quot;.
            </p>
          )}

          {canEdit ? (
            <div className="grid gap-3 sm:grid-cols-[1fr_1fr_1fr_auto] sm:items-end">
              <div className="space-y-1.5">
                <Label className="text-xs">New office</Label>
                <Input
                  value={adding.name}
                  placeholder="e.g. Dessie branch"
                  onChange={(e) => setAdding((d) => ({ ...d, name: e.target.value }))}
                />
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs">City</Label>
                <Input
                  value={adding.city}
                  onChange={(e) => setAdding((d) => ({ ...d, city: e.target.value }))}
                />
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs">Phone</Label>
                <Input
                  value={adding.phone}
                  onChange={(e) => setAdding((d) => ({ ...d, phone: e.target.value }))}
                />
              </div>
              <Button
                type="button"
                onClick={add}
                disabled={busy === "add" || !adding.name.trim()}
                className="gap-1.5"
              >
                {busy === "add" ? (
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                ) : (
                  <Plus className="h-3.5 w-3.5" />
                )}
                Add office
              </Button>
            </div>
          ) : null}
        </>
      )}
    </section>
  );
}
