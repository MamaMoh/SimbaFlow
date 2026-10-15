import { strict as assert } from "node:assert";
import test from "node:test";
import { flightNotYetDue } from "./flight-due";

const dayFromToday = (days: number) => {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};

test("a flight still to come holds the button", () => {
  const held = flightNotYetDue({ flight_date: dayFromToday(7) });
  assert.ok(held);
  assert.match(held.date, /\d{4}/);
});

test("the day of the flight is due", () => {
  // The flight leaves today: the desk confirms the departure in the morning and, on a Gulf
  // route, the arrival the same afternoon.
  assert.equal(flightNotYetDue({ flight_date: dayFromToday(0) }), null);
});

test("a flight already gone is due", () => {
  assert.equal(flightNotYetDue({ flight_date: dayFromToday(-3) }), null);
});

test("nothing to check against does not hold anything up", () => {
  assert.equal(flightNotYetDue({}), null);
  assert.equal(flightNotYetDue(null), null);
  assert.equal(flightNotYetDue({ flight_date: "" }), null);
  assert.equal(flightNotYetDue({ flight_date: "not a date" }), null);
});
