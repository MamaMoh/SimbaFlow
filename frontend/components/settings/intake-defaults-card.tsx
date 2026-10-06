"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Maximize2, Plus, X } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { CountrySelect } from "@/components/ui/country-select";
import {
  intakeSelectLabel,
  intakeSelectValue,
  saveIntakeDefaults,
  useIntakeDefaults,
  type AgencySkill,
  type IntakeDefaults,
} from "@/lib/api/intake-defaults";

/** Must match the registration form's list, or the default is a value its dropdown cannot show. */
const OCCUPATIONS = ["HOUSE MAID", "NANNY", "COOK", "DRIVER", "CLEANER", "CAREGIVER", "OTHER"];
const RELIGIONS = ["Orthodox", "Muslim", "Non-Muslim", "Protestant", "Catholic", "Other"];
const MARITAL_STATUSES = ["Single", "Married", "Divorced", "Widowed"];
const PASSPORT_TYPES = ["Normal", "Official", "Diplomatic", "Service"];
const COOKING_LEVELS = ["None", "Fair", "Good", "Excellent"];

const GENDER_OPTIONS = [
  { value: "1", label: "Female" },
  { value: "0", label: "Male" },
] as const;

/**
 * What a blank candidate form starts with.
 *
 * Agencies deploy to one corridor and one job for months at a time, so the same three answers were
 * being typed into every registration. These pre-fill a new form only — an edit always shows what
 * was saved.
 */
/** Radix Select cannot hold an empty string, so "no default" needs a stand-in value. */
const NONE = "__none__";

type Draft = Partial<
  Pick<
    IntakeDefaults,
    | "gender"
    | "occupation"
    | "religion"
    | "nationality"
    | "passportType"
    | "maritalStatus"
    | "countryOfTravel"
    | "contractPeriod"
    | "cookingLevel"
    | "cvTemplate"
    | "skills"
  >
>;

export function IntakeDefaultsCard() {
  const { defaults, mutate } = useIntakeDefaults(true);
  const [draft, setDraft] = useState<Draft>({});
  const [saving, setSaving] = useState(false);
  const [newSkill, setNewSkill] = useState("");

  // Read the GET on the same render it arrives. Copying into useState("") in an effect left the
  // card on the empty placeholders ("No default — ask each time") whenever that copy missed.
  const gender = draft.gender ?? defaults?.gender ?? "";
  const occupation = draft.occupation ?? defaults?.occupation ?? "";
  const religion = draft.religion ?? defaults?.religion ?? "";
  const nationality = draft.nationality ?? defaults?.nationality ?? "";
  const passportType = draft.passportType ?? defaults?.passportType ?? "";
  const maritalStatus = draft.maritalStatus ?? defaults?.maritalStatus ?? "";
  const countryOfTravel = draft.countryOfTravel ?? defaults?.countryOfTravel ?? "";
  const contractPeriod = draft.contractPeriod ?? defaults?.contractPeriod ?? "";
  const cookingLevel = draft.cookingLevel ?? defaults?.cookingLevel ?? "";
  const cvTemplate = draft.cvTemplate ?? defaults?.cvTemplate ?? "enjaz";
  const skills = draft.skills ?? defaults?.skills ?? [];

  const patch = (field: keyof Draft, value: string) =>
    setDraft((current) => ({ ...current, [field]: value }));

  const setSkills = (next: AgencySkill[]) =>
    setDraft((current) => ({ ...current, skills: next }));

  const addSkill = () => {
    const name = newSkill.trim();
    if (!name) return;
    if (skills.some((s) => s.name.toLowerCase() === name.toLowerCase())) {
      toast.error("That skill is already on the list");
      return;
    }
    setSkills([
      ...skills,
      {
        id: `new:${crypto.randomUUID()}`,
        name,
        builtInKey: null,
        isBuiltIn: false,
        isDefaultSelected: false,
        sortOrder: skills.length,
      },
    ]);
    setNewSkill("");
  };

  const onSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      const saved = await saveIntakeDefaults({
        gender,
        occupation,
        religion,
        nationality,
        passportType,
        maritalStatus,
        countryOfTravel,
        contractPeriod,
        cookingLevel,
        cvTemplate,
        skills,
      });
      // Keep the layouts list from the GET; the save echo does not repeat it.
      await mutate({ ...defaults, ...saved, cvTemplates: saved.cvTemplates ?? defaults?.cvTemplates, skills: saved.skills ?? skills }, { revalidate: true });
      setDraft({});
      toast.success("New candidate forms will start with these");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not save");
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={onSave} className="space-y-4 rounded-lg border bg-card p-4 shadow-sm">
      <div>
        <h2 className="text-sm font-semibold">New candidate defaults</h2>
        <p className="mt-0.5 text-xs text-muted-foreground">
          Pre-filled on a new registration only.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div className="space-y-1.5">
          <Label>Gender</Label>
          <Select
            value={intakeSelectValue(gender, NONE)}
            onValueChange={(v) => patch("gender", v === NONE ? "" : v)}
          >
            <SelectTrigger>
              <SelectValue>
                {intakeSelectLabel(gender, GENDER_OPTIONS, "No default — ask each time")}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>No default — ask each time</SelectItem>
              <SelectItem value="1">Female</SelectItem>
              <SelectItem value="0">Male</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label>Occupation</Label>
          <Select
            value={intakeSelectValue(occupation, NONE)}
            onValueChange={(v) => patch("occupation", v === NONE ? "" : v)}
          >
            <SelectTrigger id="def-occupation">
              <SelectValue>
                {intakeSelectLabel(
                  occupation,
                  OCCUPATIONS.map((o) => ({ value: o, label: o })),
                  "No default"
                )}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>No default</SelectItem>
              {OCCUPATIONS.map((o) => (
                <SelectItem key={o} value={o}>
                  {o}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label>Religion</Label>
          <Select
            value={intakeSelectValue(religion, NONE)}
            onValueChange={(v) => patch("religion", v === NONE ? "" : v)}
          >
            <SelectTrigger id="def-religion">
              <SelectValue>
                {intakeSelectLabel(
                  religion,
                  RELIGIONS.map((r) => ({ value: r, label: r })),
                  "No default"
                )}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>No default</SelectItem>
              {RELIGIONS.map((r) => (
                <SelectItem key={r} value={r}>{r}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label>Marital status</Label>
          <Select
            value={intakeSelectValue(maritalStatus, NONE)}
            onValueChange={(v) => patch("maritalStatus", v === NONE ? "" : v)}
          >
            <SelectTrigger id="def-marital">
              <SelectValue>
                {intakeSelectLabel(
                  maritalStatus,
                  MARITAL_STATUSES.map((m) => ({ value: m, label: m })),
                  "No default"
                )}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>No default</SelectItem>
              {MARITAL_STATUSES.map((m) => (
                <SelectItem key={m} value={m}>{m}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label>Passport type</Label>
          <Select
            value={intakeSelectValue(passportType, NONE)}
            onValueChange={(v) => patch("passportType", v === NONE ? "" : v)}
          >
            <SelectTrigger id="def-passport-type">
              <SelectValue>
                {intakeSelectLabel(
                  passportType,
                  PASSPORT_TYPES.map((x) => ({ value: x, label: x })),
                  "No default"
                )}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>No default</SelectItem>
              {PASSPORT_TYPES.map((x) => (
                <SelectItem key={x} value={x}>{x}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label>Nationality</Label>
          {/* The name, matching the registration form — "ET" is the picker key, not what a CV prints. */}
          <CountrySelect value={nationality} onChange={(_code, name) => patch("nationality", name)} />
        </div>

        <div className="space-y-1.5">
          <Label>Country of travel</Label>
          <CountrySelect value={countryOfTravel} onChange={(_code, name) => patch("countryOfTravel", name)} />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="def-period">Contract period</Label>
          <Input
            id="def-period"
            value={contractPeriod}
            onChange={(e) => patch("contractPeriod", e.target.value)}
            placeholder="2 Years"
          />
        </div>

        <div className="space-y-1.5">
          <Label>Cooking level</Label>
          <Select
            value={intakeSelectValue(cookingLevel, NONE)}
            onValueChange={(v) => patch("cookingLevel", v === NONE ? "" : v)}
          >
            <SelectTrigger id="def-cooking">
              <SelectValue>
                {intakeSelectLabel(
                  cookingLevel,
                  COOKING_LEVELS.map((l) => ({ value: l, label: l })),
                  "No default"
                )}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>No default</SelectItem>
              {COOKING_LEVELS.map((l) => (
                <SelectItem key={l} value={l}>{l}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      <div className="space-y-2 border-t pt-4">
        <div>
          <Label>Skills</Label>
          <p className="mt-0.5 text-xs text-muted-foreground">
            Ticked skills are pre-checked on a new registration.
          </p>
        </div>
        <div className="flex flex-wrap gap-x-5 gap-y-2">
          {skills.map((skill) => (
            <label key={skill.id} className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={skill.isDefaultSelected}
                onCheckedChange={(v) =>
                  setSkills(
                    skills.map((s) =>
                      s.id === skill.id ? { ...s, isDefaultSelected: v === true } : s
                    )
                  )
                }
              />
              <span>{skill.name}</span>
              {!skill.isBuiltIn ? (
                <button
                  type="button"
                  className="rounded p-0.5 text-muted-foreground hover:text-destructive"
                  aria-label={`Remove ${skill.name}`}
                  onClick={() => setSkills(skills.filter((s) => s.id !== skill.id))}
                >
                  <X className="h-3 w-3" />
                </button>
              ) : null}
            </label>
          ))}
        </div>
        <div className="flex max-w-sm items-center gap-2 pt-1">
          <Input
            value={newSkill}
            onChange={(e) => setNewSkill(e.target.value)}
            placeholder="Add a skill"
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                e.preventDefault();
                addSkill();
              }
            }}
          />
          <Button type="button" variant="outline" onClick={addSkill}>
            <Plus className="h-4 w-4" />
            Add
          </Button>
        </div>
      </div>

      <div className="space-y-2 border-t pt-4">
        <div>
          <Label>CV layout</Label>
          <p className="mt-0.5 text-xs text-muted-foreground">
            Which layout a generated CV is printed on.
          </p>
        </div>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
          {(defaults?.cvTemplates ?? []).map((tpl) => {
            const selected = cvTemplate === tpl.value;
            return (
              <div
                key={tpl.value}
                className={`overflow-hidden rounded-lg border transition-colors ${
                  selected ? "border-green-700 ring-1 ring-green-700" : ""
                }`}
              >
                {/* The page itself, at a glance. Choosing a form from a sentence means saving,
                    downloading a CV and looking, then coming back to change it. */}
                <button
                  type="button"
                  aria-pressed={selected}
                  onClick={() => patch("cvTemplate", tpl.value)}
                  className="block w-full text-left"
                  title={`Use the ${tpl.name}`}
                >
                  <span className="block overflow-hidden border-b bg-muted/30">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img
                      src={`/api/proxy/settings/intake-defaults/cv-preview/${tpl.value}/image`}
                      alt={`${tpl.name}, drawn with sample details`}
                      loading="lazy"
                      className="block h-auto w-full"
                    />
                  </span>
                  <span className="flex items-center gap-2 px-3 pt-2.5">
                    <span
                      className={`flex h-3.5 w-3.5 shrink-0 items-center justify-center rounded-full border ${
                        selected ? "border-green-700" : "border-muted-foreground/40"
                      }`}
                    >
                      {selected && <span className="h-1.5 w-1.5 rounded-full bg-green-700" />}
                    </span>
                    <span className="text-sm font-medium">{tpl.name}</span>
                  </span>
                  <span className="mt-1 block px-3 pb-2 text-xs leading-relaxed text-muted-foreground">
                    {tpl.description}
                  </span>
                </button>
                <a
                  href={`/api/proxy/settings/intake-defaults/cv-preview/${tpl.value}`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="flex items-center justify-center gap-1.5 border-t px-3 py-1.5 text-xs text-muted-foreground transition-colors hover:bg-muted/50 hover:text-foreground"
                >
                  <Maximize2 className="h-3 w-3" />
                  Open full size
                </a>
              </div>
            );
          })}
        </div>
      </div>

      <Button type="submit" disabled={saving || !defaults} className="bg-green-800 text-white hover:bg-green-900">
        {saving ? "Saving…" : "Save defaults"}
      </Button>
    </form>
  );
}
