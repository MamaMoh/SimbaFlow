# Business Rules — Unit 7: Bot & Notifications

## Bot access

| ID | Rule |
|----|------|
| BR-BOT01 | Only users with `bot.use` (or SuperAdmin) may complete Telegram link |
| BR-BOT02 | Commands require linked `TelegramChatId`; else reply with link instructions |
| BR-BOT03 | All candidate lookups are **tenant-scoped** to the linked user’s tenant |
| BR-BOT04 | Write commands (`/medical`, `/arrived`) **out of scope** for v1 — bot replies “use web app” if received |
| BR-BOT05 | CV send requires `candidate.read` (or bot.use implying field read) on linked user |
| BR-BOT06 | Language preference persists; default `en` |

## Telegram ops

| ID | Rule |
|----|------|
| BR-TG01 | Polling runs only when channel Enabled and token configured |
| BR-TG02 | Bot token never returned in API responses (masked status only) |
| BR-TG03 | Duplicate `/link` code → reject; codes expire (e.g. 10 minutes) |
| BR-TG04 | Failed sends recorded as DeliveryStatus.Failed with error snippet (no token) |

## Notifications

| ID | Rule |
|----|------|
| BR-N01 | Stage-change push is best-effort; failure must not fail workflow transition |
| BR-N02 | Skip push if user has no TelegramChatId (DeliveryStatus.Skipped) |
| BR-N03 | Departure Mark Notified still succeeds if Telegram send fails (log Failed; optional warn) |
| BR-N04 | WhatsApp sends disabled in v1 (Skipped if somehow invoked) |

## SignalR

| ID | Rule |
|----|------|
| BR-SR01 | Frontend connects only when authenticated |
| BR-SR02 | Existing `candidateUpdated` payload shape preserved |

## Permissions

| Permission | Use |
|------------|-----|
| `bot.configure` | Bot config page, test connection |
| `bot.use` | Link Telegram, use commands |
| `notification.configure` | Enable/disable push channel flags |
| `notification.send` | Manual test notification (optional) |
| `candidate.read` | Status/CV data |

Existing seed already includes these codes.
