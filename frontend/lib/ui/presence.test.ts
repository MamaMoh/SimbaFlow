import assert from "node:assert/strict";
import { test } from "node:test";
import { RETURNING_MS, hereAndLooking } from "./presence";

const here = { focused: true, sinceReturn: Infinity };

test("sitting in front of the page, a click away is a click away", () => {
  assert.equal(hereAndLooking(here), true);
});

test("a page behind another tab or application is not being clicked away from", () => {
  // Going to another site for an E number and coming back used to find the form gone and the
  // typing with it, with nothing on screen to say what had happened.
  assert.equal(hereAndLooking({ ...here, focused: false }), false);
});

test("the click that brings the window back belongs to the window", () => {
  assert.equal(hereAndLooking({ ...here, sinceReturn: 0 }), false);
  assert.equal(hereAndLooking({ ...here, sinceReturn: RETURNING_MS }), false);
});

test("a second, deliberate click a moment later does dismiss", () => {
  assert.equal(hereAndLooking({ ...here, sinceReturn: RETURNING_MS + 1 }), true);
  assert.equal(hereAndLooking({ ...here, sinceReturn: 2000 }), true);
});

test("a page that never left has no grace period to sit inside", () => {
  // Infinity rather than 0 for "never returned", or every dialog would ignore its first
  // outside click for a quarter of a second after mounting.
  assert.equal(hereAndLooking({ focused: true, sinceReturn: Infinity }), true);
});

test("the grace period is short enough not to swallow a real click", () => {
  assert.ok(RETURNING_MS > 0 && RETURNING_MS <= 400);
});
