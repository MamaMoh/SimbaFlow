"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { CountrySelect } from "@/components/ui/country-select";
import { saveIntakeDefaults, useIntakeDefaults } from "@/lib/api/intake-defaults";

/** Must match the registration form's list, or the default is a value its dropdown cannot show. */
const OCCUPATIONS = ["HOUSE MAID", "NANNY", "COOK", "DRIVER", "CLEANER", "CAREGIVER", "OTHER"];
const RELIGIONS = ["Orthodox", "Muslim", "Non-Muslim", "Protestant", "Catholic", "Other"];
const MARITAL_STATUSES = ["Single", "Married", "Divorced", "Widowed"];
const PASSPORT_TYPES = ["Normal", "Official", "Diplomatic", "Service"];

/**
 * What a blank candidate form starts with.
 *
 * Agencies deploy to one corridor and one job for months at a time, so the same three answers were
 * being typed into every registration. These pre-fill a new form only — an edit always shows what
 * was saved.
 */
/** Radix Select cannot hold an empty string, so "no default" needs a stand-in value. */
const NONE = "__none__";

export function IntakeDefaultsCard() {
  const { defaults, mutate } = useIntakeDefaults(true);
  const [gender, setGender] = useState("");
  const [cvTemplate, setCvTemplate] = useState("enjaz");
  const [occupation, setOccupation] = useState("");
  const [religion, setReligion] = useState("");
  const [nationality, setNationality] = useState("");
  const [passportType, setPassportType] = useState("");
  const [maritalStatus, setMaritalStatus] = useState("");
  const [countryOfTravel, setCountryOfTravel] = useState("");
  const [contractPeriod, setContractPeriod] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!defaults) return;
    setGender(defaults.gender || "");
    setCvTemplate(defaults.cvTemplate || "enjaz");
    setOccupation(defaults.occupation || "");
    setReligion(defaults.religion || "");
    setNationality(defaults.nationality || "");
    setPassportType(defaults.passportType || "");
    setMaritalStatus(defaults.maritalStatus || "");
    setCountryOfTravel(defaults.countryOfTravel || "");
    setContractPeriod(defaults.contractPeriod || "");
  }, [defaults]);

  const onSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await saveIntakeDefaults({
        gender,
        occupation,
        religion,
        nationality,
        passportType,
        maritalStatus,
        countryOfTravel,
        contractPeriod,
        cvTemplate,
      });
      toast.success("New candidate forms will start with these");
      void mutate();
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
          Pre-filled on a new registration. Editing an existing candidate is never affected.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label>Gender</Label>
          <Select
            value={gender || NONE}
            onValueChange={(v) => setGender(v === NONE ? "" : v)}
          >
            <SelectTrigger>
              <SelectValue />
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
            value={occupation || NONE}
            onValueChange={(v) => setOccupation(v === NONE ? "" : v)}
          >
            <SelectTrigger id="def-occupation">
              <SelectValue placeholder="Select occupation" />
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
            value={religion || NONE}
            onValueChange={(v) => setReligion(v === NONE ? "" : v)}
          >
            <SelectTrigger id="def-religion">
              <SelectValue placeholder="Select religion" />
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
            value={maritalStatus || NONE}
            onValueChange={(v) => setMaritalStatus(v === NONE ? "" : v)}
          >
            <SelectTrigger id="def-marital">
              <SelectValue placeholder="Select status" />
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
            value={passportType || NONE}
            onValueChange={(v) => setPassportType(v === NONE ? "" : v)}
          >
            <SelectTrigger id="def-passport-type">
              <SelectValue placeholder="Select type" />
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
          <CountrySelect value={nationality} onChange={setNationality} />
        </div>

        <div className="space-y-1.5">
          <Label>Country of travel</Label>
          <CountrySelect value={countryOfTravel} onChange={setCountryOfTravel} />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="def-period">Contract period</Label>
          <Input
            id="def-period"
            value={contractPeriod}
            onChange={(e) => setContractPeriod(e.target.value)}
            placeholder="2 Years"
          />
        </div>
      </div>

      <div className="space-y-2 border-t pt-4">
        <div>
          <Label>CV layout</Label>
          <p className="mt-0.5 text-xs text-muted-foreground">
            Which form a generated CV is printed on. The one a partner will accept depends on who
            you are sending it to, so it is set here rather than chosen on every download.
          </p>
        </div>
        <div className="grid gap-2 sm:grid-cols-2">
          {(defaults?.cvTemplates ?? []).map((tpl) => {
            const selected = cvTemplate === tpl.value;
            return (
              <button
                key={tpl.value}
                type="button"
                aria-pressed={selected}
                onClick={() => setCvTemplate(tpl.value)}
                className={`rounded-lg border p-3 text-left transition-colors ${
                  selected
                    ? "border-green-700 bg-green-50 ring-1 ring-green-700"
                    : "hover:bg-muted/50"
                }`}
              >
                <span className="flex items-center gap-2">
                  <span
                    className={`flex h-3.5 w-3.5 items-center justify-center rounded-full border ${
                      selected ? "border-green-700" : "border-muted-foreground/40"
                    }`}
                  >
                    {selected && <span className="h-1.5 w-1.5 rounded-full bg-green-700" />}
                  </span>
                  <span className="text-sm font-medium">{tpl.name}</span>
                </span>
                <span className="mt-1 block text-xs leading-relaxed text-muted-foreground">
                  {tpl.description}
                </span>
              </button>
            );
          })}
        </div>
      </div>

      <Button type="submit" disabled={saving} className="bg-green-800 text-white hover:bg-green-900">
        {saving ? "Saving…" : "Save defaults"}
      </Button>
    </form>
  );
}
