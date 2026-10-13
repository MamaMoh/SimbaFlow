import type { AgencyBotChatLink, BotChatLink } from "@/lib/api/bot";

/**
 * How one linked Telegram account reads on screen.
 *
 * Telegram does not disclose a phone number to a bot, so there is no number to show. What there
 * is: the @handle, and the first/last name on the Telegram account. Either can be absent — a
 * handle is optional on Telegram, and a link made before this was recorded has neither.
 */
export function describeLink(link: BotChatLink): string {
  if (link.telegramUsername) return `@${link.telegramUsername}`;
  if (link.displayName) return link.displayName;
  return "Telegram account";
}

/** The second line: the name, when the first line is already the handle. */
export function linkSubtitle(link: BotChatLink): string | null {
  return link.telegramUsername && link.displayName ? link.displayName : null;
}

/** "Amina Yusuf", falling back to the sign-in name. */
export function staffName(link: AgencyBotChatLink): string {
  const full = [link.firstName, link.lastName].filter(Boolean).join(" ").trim();
  return full || link.userName || "Unknown";
}

/**
 * Days since this chat last sent the bot anything, or null when it never has.
 *
 * The point of the column is spotting links nobody uses — a handset that was replaced, somebody
 * who has left — so it is a count of days and not a timestamp.
 */
export function daysSinceSeen(link: BotChatLink, now = new Date()): number | null {
  if (!link.lastSeenAt) return null;
  const seen = new Date(link.lastSeenAt);
  if (Number.isNaN(seen.getTime())) return null;
  return Math.max(0, Math.floor((now.getTime() - seen.getTime()) / 86_400_000));
}

/** A chat silent this long is worth an owner's attention. */
export const DORMANT_DAYS = 30;

export function lastSeenLabel(link: BotChatLink, now = new Date()): string {
  const days = daysSinceSeen(link, now);
  if (days === null) return "Never used";
  if (days === 0) return "Today";
  if (days === 1) return "Yesterday";
  return `${days} days ago`;
}
