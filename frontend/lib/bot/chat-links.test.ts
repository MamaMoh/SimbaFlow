import assert from "node:assert/strict";
import { test } from "node:test";
import {
  DORMANT_DAYS,
  daysSinceSeen,
  describeLink,
  lastSeenLabel,
  linkSubtitle,
  staffName,
} from "./chat-links";
import type { AgencyBotChatLink, BotChatLink } from "@/lib/api/bot";

function link(over: Partial<AgencyBotChatLink> = {}): AgencyBotChatLink {
  return {
    id: "link-1",
    userId: "user-1",
    userName: "staff.sample",
    firstName: "Staff",
    lastName: "Sample",
    isActive: true,
    muted: false,
    telegramUsername: null,
    displayName: null,
    linkedAt: "2026-01-01T00:00:00Z",
    lastSeenAt: null,
    ...over,
  };
}

test("a handle is what identifies a link when there is one", () => {
  assert.equal(describeLink(link({ telegramUsername: "office_phone" })), "@office_phone");
});

test("without a handle, the name Telegram reports stands in", () => {
  assert.equal(describeLink(link({ displayName: "Office Phone" })), "Office Phone");
});

test("a link with neither still reads as something", () => {
  // Links made before the handle was recorded have no handle and no name, and the row still has
  // to say what it is rather than render blank.
  assert.equal(describeLink(link()), "Telegram account");
});

test("the name is the second line only when the handle took the first", () => {
  assert.equal(
    linkSubtitle(link({ telegramUsername: "office_phone", displayName: "Office Phone" })),
    "Office Phone",
  );
  assert.equal(linkSubtitle(link({ displayName: "Office Phone" })), null);
});

test("a person without a full name falls back to their sign-in name", () => {
  assert.equal(staffName(link({ firstName: null, lastName: null })), "staff.sample");
  assert.equal(staffName(link()), "Staff Sample");
});

test("never used is not the same as used today", () => {
  // Null read as 0 would have shown "Today" for a link that has never sent the bot anything,
  // which is exactly backwards for the column that is there to find dead links.
  assert.equal(daysSinceSeen(link()), null);
  assert.equal(lastSeenLabel(link()), "Never used");
});

test("days are counted, not timestamps shown", () => {
  const now = new Date("2026-03-10T12:00:00Z");
  assert.equal(lastSeenLabel(link({ lastSeenAt: "2026-03-10T08:00:00Z" }), now), "Today");
  assert.equal(lastSeenLabel(link({ lastSeenAt: "2026-03-09T08:00:00Z" }), now), "Yesterday");
  assert.equal(lastSeenLabel(link({ lastSeenAt: "2026-03-01T12:00:00Z" }), now), "9 days ago");
});

test("a clock skewed into the future does not read as negative days", () => {
  const now = new Date("2026-03-10T12:00:00Z");
  assert.equal(daysSinceSeen(link({ lastSeenAt: "2026-03-11T12:00:00Z" }), now), 0);
});

test("an unparseable date is treated as unknown rather than as NaN days", () => {
  assert.equal(daysSinceSeen(link({ lastSeenAt: "not a date" })), null);
  assert.equal(lastSeenLabel(link({ lastSeenAt: "not a date" })), "Never used");
});

test("the dormant threshold is a month", () => {
  const now = new Date("2026-03-10T12:00:00Z");
  const old = new Date(now.getTime() - DORMANT_DAYS * 86_400_000).toISOString();
  assert.ok(daysSinceSeen(link({ lastSeenAt: old }), now)! >= DORMANT_DAYS);
});
