/**
 * Whether the flight on a ticket has come round yet.
 *
 * The server refuses a departure or an arrival confirmed ahead of its flight date, and that is
 * where the rule belongs — a board is not the only way into those endpoints. But a menu item
 * that looks available and then fails is a worse way to learn a rule than one that is already
 * greyed out with the date on it, so the same question is asked here to decide how the item is
 * drawn. The answer is advisory: the server decides.
 *
 * The day of the flight counts as due. Addis to the Gulf lands the same day, so a desk
 * confirming the departure in the morning and the arrival that afternoon is the ordinary case.
 */
export function flightNotYetDue(
  statusValues: Record<string, string> | null | undefined,
): { date: string } | null {
  const raw = statusValues?.flight_date?.trim();
  if (!raw) return null;

  // Stored as yyyy-MM-dd. Compared as a plain date rather than a moment, so a flight this
  // afternoon is not "in the future" at nine in the morning.
  const flight = new Date(`${raw}T00:00:00Z`);
  if (Number.isNaN(flight.getTime())) return null;

  const today = new Date();
  const midnightUtcToday = Date.UTC(
    today.getUTCFullYear(),
    today.getUTCMonth(),
    today.getUTCDate(),
  );
  if (flight.getTime() <= midnightUtcToday) return null;

  return {
    date: flight.toLocaleDateString(undefined, {
      day: "numeric",
      month: "long",
      year: "numeric",
      timeZone: "UTC",
    }),
  };
}
