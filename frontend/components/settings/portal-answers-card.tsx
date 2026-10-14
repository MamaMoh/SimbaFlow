"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { formsWithStandingAnswers } from "@/lib/site-bridge/manifest";
import {
  answersFor,
  savePortalAnswers,
  usePortalAnswers,
  type PortalAnswer,
} from "@/lib/api/portal-answers";

/**
 * The answers that are the agency's rather than the candidate's.
 *
 * A third of the visa application is the same on every one this office has ever filed — the
 * nearest mission, the validity, the duration of stay, the transport mode. It used to be typed
 * out each time, and then it was written into the code, which was only correct for as long as
 * one agency used it: the nearest Saudi mission is only Addis Ababa from Addis Ababa.
 *
 * Every box here already has an answer. Leaving one empty is not leaving the box blank on the
 * form — it means "use the one that comes with the form", which is what the placeholder shows.
 * That distinction is why clearing a box deletes the row rather than saving an empty string.
 */
export function PortalAnswersCard({ canEdit }: { canEdit: boolean }) {
  const forms = formsWithStandingAnswers();
  const { answers, mutate, isLoading, error } = usePortalAnswers();
  const [draft, setDraft] = useState<Record<string, Record<string, string>>>({});
  const [saving, setSaving] = useState(false);

  // Re-seeded whenever the saved answers change, so a save elsewhere does not leave this screen
  // showing an older set as though it were current.
  useEffect(() => {
    if (!answers) return;
    const next: Record<string, Record<string, string>> = {};
    for (const { site } of forms) next[site.id] = answersFor(answers, site.id);
    setDraft(next);
  }, [answers]); // eslint-disable-line react-hooks/exhaustive-deps

  const valueOf = (siteId: string, label: string) => draft[siteId]?.[label] ?? "";

  const set = (siteId: string, label: string) => (value: string) =>
    setDraft((d) => ({ ...d, [siteId]: { ...d[siteId], [label]: value } }));

  const save = async () => {
    setSaving(true);
    try {
      const rows: PortalAnswer[] = [];
      for (const { site, fields } of forms)
        for (const field of fields)
          rows.push({ siteId: site.id, label: field.label, value: valueOf(site.id, field.label) });

      await savePortalAnswers(rows);
      await mutate();
      toast.success("Applications will be filled with these from now on");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not save");
    } finally {
      setSaving(false);
    }
  };

  if (forms.length === 0) return null;

  return (
    <section className="space-y-4 rounded-lg border bg-card p-5 shadow-sm">
      <div>
        <h2 className="text-sm font-semibold">Standing answers on other portals</h2>
        <p className="text-muted-foreground mt-0.5 text-xs">
          The boxes this agency answers the same way on every application. Leave one empty to use
          the answer shown in grey.
        </p>
      </div>

      {error ? (
        <p className="text-destructive text-xs">{error.message}</p>
      ) : isLoading ? (
        <p className="text-muted-foreground text-xs">Loading…</p>
      ) : (
        <>
          {forms.map(({ site, fields }) => (
            <div key={site.id} className="space-y-3">
              <h3 className="text-xs font-medium">{site.title}</h3>
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                {fields.map((field) => (
                  <div key={field.label} className="space-y-1.5">
                    <Label className="text-xs">{field.label}</Label>
                    <Input
                      value={valueOf(site.id, field.label)}
                      placeholder={field.fixed}
                      disabled={!canEdit}
                      onChange={(e) => set(site.id, field.label)(e.target.value)}
                    />
                  </div>
                ))}
              </div>
            </div>
          ))}

          {canEdit ? (
            <Button type="button" onClick={save} disabled={saving} className="gap-1.5">
              {saving ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : null}
              {saving ? "Saving…" : "Save answers"}
            </Button>
          ) : null}
        </>
      )}
    </section>
  );
}
