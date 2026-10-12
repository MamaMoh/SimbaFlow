import assert from "node:assert/strict";
import { test } from "node:test";
import { appointmentCountdown, daysUntil } from "./status";

/** A local date string for a day offset from today, which is what these boards store. */
function offset(days: number): string {
  const now = new Date();
  const d = new Date(now.getFullYear(), now.getMonth(), now.getDate() + days);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

test("days are counted by the calendar, not by the clock", () => {
  assert.equal(daysUntil(offset(0)), 0);
  assert.equal(daysUntil(offset(1)), 1);
  assert.equal(daysUntil(offset(-4)), -4);
  // A date with a time on it still counts whole days.
  assert.equal(daysUntil(`${offset(2)}T23:45:00Z`), 2);
  assert.equal(daysUntil("not a date"), null);
  assert.equal(daysUntil(null), null);
});

test("an appointment gets louder as it approaches", () => {
  assert.deepEqual(appointmentCountdown(offset(0)), { tone: "danger", label: "Today" });
  assert.deepEqual(appointmentCountdown(offset(1)), { tone: "warning", label: "Tomorrow" });
  assert.deepEqual(appointmentCountdown(offset(3)), { tone: "warning", label: "3d left" });
  assert.deepEqual(appointmentCountdown(offset(7)), { tone: "info", label: "7d left" });
  assert.deepEqual(appointmentCountdown(offset(20)), { tone: "neutral", label: "20d left" });
});

test("a date that has passed is only an alarm while something is still owed", () => {
  // The result was never recorded: somebody missed it, and the board should say so.
  assert.deepEqual(appointmentCountdown(offset(-2), true), { tone: "danger", label: "2d ago" });
  // The result is in. The date is history, and a board of red for finished work trains people
  // to stop reading red.
  assert.deepEqual(appointmentCountdown(offset(-2), false), { tone: "neutral", label: "2d ago" });
});

test("no date is not a countdown at all — the cell shows its own warning instead", () => {
  assert.equal(appointmentCountdown(null), null);
  assert.equal(appointmentCountdown(""), null);
});
