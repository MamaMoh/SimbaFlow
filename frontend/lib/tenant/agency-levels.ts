/**
 * Agency ደረጃ 1–5 caps, MoLS Directive 1126/2018 Arts. 18–22.
 *
 * The caps only. The levels also carry an occupation scope — domestic, labour, skilled — but
 * nothing in the system enforces one, and two levels share a scope, so naming them here would
 * describe a rule that does not exist.
 *
 * Mirrors AgencyLevelRules on the server, which is what actually enforces these when an agency is
 * created or edited and when a partner is linked. The server also publishes them at
 * GET /api/tenants/agency-levels; this copy exists so the forms can validate before submitting
 * rather than after. If the two ever disagree the server wins — it is the one that refuses.
 */
export const AGENCY_LEVELS = [
  { level: 1, maxPartnersPerCountry: 20, maxCountries: null as number | null },
  { level: 2, maxPartnersPerCountry: 20, maxCountries: 8 },
  { level: 3, maxPartnersPerCountry: 16, maxCountries: 8 },
  { level: 4, maxPartnersPerCountry: 8, maxCountries: 4 },
  { level: 5, maxPartnersPerCountry: 4, maxCountries: 2 },
] as const;

/** Where Ethiopian agencies place workers. */
export const DESTINATION_OPTIONS = [
  "Saudi Arabia",
  "United Arab Emirates",
  "Kuwait",
  "Qatar",
  "Bahrain",
  "Oman",
  "Jordan",
  "Lebanon",
] as const;
