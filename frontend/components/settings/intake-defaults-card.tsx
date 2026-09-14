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
export function IntakeDefaultsCard() {
  const { defaults, mutate } = useIntakeDefaults(true);
  const [gender, setGender] = useState("1");
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
    setGender(defaults.gender || "1");
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
          <Select value={gender} onValueChange={setGender}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="1">Female</SelectItem>
              <SelectItem value="0">Male</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label>Occupation</Label>
          <Select value={occupation || undefined} onValueChange={setOccupation}>
            <SelectTrigger id="def-occupation">
              <SelectValue placeholder="Select occupation" />
            </SelectTrigger>
            <SelectContent>
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
          <Select value={religion || undefined} onValueChange={setReligion}>
            <SelectTrigger id="def-religion">
              <SelectValue placeholder="Select religion" />
            </SelectTrigger>
            <SelectContent>
              {RELIGIONS.map((r) => (
                <SelectItem key={r} value={r}>{r}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label>Marital status</Label>
          <Select value={maritalStatus || undefined} onValueChange={setMaritalStatus}>
            <SelectTrigger id="def-marital">
              <SelectValue placeholder="Select status" />
            </SelectTrigger>
            <SelectContent>
              {MARITAL_STATUSES.map((m) => (
                <SelectItem key={m} value={m}>{m}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label>Passport type</Label>
          <Select value={passportType || undefined} onValueChange={setPassportType}>
            <SelectTrigger id="def-passport-type">
              <SelectValue placeholder="Select type" />
            </SelectTrigger>
            <SelectContent>
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

      <Button type="submit" disabled={saving} className="bg-green-800 text-white hover:bg-green-900">
        {saving ? "Saving…" : "Save defaults"}
      </Button>
    </form>
  );
}
