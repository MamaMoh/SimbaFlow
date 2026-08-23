/**
 * Destination cities per receiving country.
 *
 * Flight destination used to be free text, which meant "RIYADH", "Riyadh" and "riyad" all coexisted
 * and nothing could be grouped or reported on. The list is scoped to the candidate's country of
 * travel, so a candidate going to a Saudi partner only ever sees Saudi cities.
 */
export const DESTINATION_CITIES: Record<string, string[]> = {
  "saudi arabia": ["Riyadh", "Jeddah", "Mecca", "Medina", "Dammam", "Khobar", "Taif", "Tabuk", "Abha", "Buraidah"],
  "united arab emirates": ["Dubai", "Abu Dhabi", "Sharjah", "Ajman", "Al Ain", "Ras Al Khaimah", "Fujairah"],
  "kuwait": ["Kuwait City", "Hawalli", "Salmiya", "Farwaniya", "Jahra", "Ahmadi"],
  "qatar": ["Doha", "Al Rayyan", "Al Wakrah", "Al Khor"],
  "bahrain": ["Manama", "Riffa", "Muharraq", "Isa Town"],
  "oman": ["Muscat", "Salalah", "Sohar", "Nizwa"],
  "jordan": ["Amman", "Zarqa", "Irbid", "Aqaba"],
  "lebanon": ["Beirut", "Tripoli", "Sidon", "Tyre"],
};

/** Cities for a country name, or an empty list when we don't have that country mapped. */
export function citiesFor(country?: string | null): string[] {
  if (!country) return [];
  return DESTINATION_CITIES[country.trim().toLowerCase()] ?? [];
}

/** Today as yyyy-mm-dd, for date inputs that must not accept the past. */
export function todayIso(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}
