# Domain Entities — Unit 7: Bot & Notifications

## Design posture (approved plan answers)

| Topic | Choice |
|-------|--------|
| Scope | **Phased** — Telegram + real notifier + SignalR mount; defer WhatsApp + full rule CRUD |
| Channels | **Telegram first**; WhatsApp config stub only |
| Host | **Long-polling `BackgroundService`** |
| Rules | **Lightweight v1** — code event→channel map; admin for token + enable flags |
| Commands | **Core**: `/register`, `/status`, `/lang`, `/cv` + stage-change push; defer `/medical`, `/arrived` |
| SignalR | **Mount + expand** provider/listener in layout |

---

## Existing (extend — no new tables for chat IDs)

```
ApplicationUser (platform)
├── TelegramChatId : string?     // set via /register
├── WhatsAppNumber : string?     // reserved; unused in v1 send path
└── PreferredBotLanguage : string?  // "en" | "am" — add if missing; else store on BotLink
```

If `PreferredBotLanguage` is not on user yet, add nullable string (default `en`).

---

## New: BotChannelConfig (platform or tenant?)

**v1: platform singleton row(s)** stored in `SystemConfiguration` JSON **or** dedicated table:

```
BotChannelConfig : BaseEntity (public)
├── Channel : BotChannel  // Telegram | WhatsApp
├── IsEnabled : bool
├── ConnectionStatus : BotConnectionStatus  // Disconnected | Connecting | Connected | Error
├── LastConnectedAt : DateTime?
├── LastError : string?
└── // Token NOT stored in this entity — see secrets
```

**Secrets**: Telegram bot token in `appsettings` / env (`Telegram:BotToken`) — never returned by API; admin UI shows masked “configured / missing” + Connect/Test.

WhatsApp fields in config for future: `WhatsApp:ApiUrl`, `WhatsApp:ApiToken` — UI read-only stub “Coming in later batch”.

---

## New: BotUserLink (optional if ApplicationUser.TelegramChatId sufficient)

v1 can use **only** `ApplicationUser.TelegramChatId` + verify OTP/code flow without a separate table.

If verification needs pending state:

```
BotRegistrationChallenge : BaseEntity (public)
├── UserId : Guid
├── Channel : BotChannel
├── ChatId : string
├── Code : string  // short-lived
├── ExpiresAt : DateTime
└── ConsumedAt : DateTime?
```

---

## New: NotificationDelivery (tenant or public?)

**v1: public** (cross-tenant delivery log keyed by TenantId):

```
NotificationDelivery : BaseEntity (public)
├── TenantId : Guid
├── UserId : Guid?
├── Channel : BotChannel
├── EventType : string  // e.g. CandidateStageChanged, DepartureNotify
├── PayloadSummary : string  // no PII beyond name/passport last4 if needed
├── Status : DeliveryStatus  // Pending | Sent | Failed | Skipped
├── ExternalMessageId : string?
├── Error : string?
└── SentAt : DateTime?
```

**Deferred entities** (full rule engine): `NotificationRule`, `NotificationTemplate`, `BotSession`.

---

## Enums

```
BotChannel: Telegram = 0, WhatsApp = 1
BotConnectionStatus: Disconnected, Connecting, Connected, Error
DeliveryStatus: Pending, Sent, Failed, Skipped
```

---

## Brownfield seams

| Seam | Unit 7 action |
|------|----------------|
| `ICandidateNotifier` | Replace NoOp with `TelegramCandidateNotifier` (departure Mark Notified) |
| `ISignalRBroadcaster` | Keep; optionally `SendPersonalNotificationAsync` on bot events |
| Stage change handlers | After broadcast, enqueue Telegram push to linked FieldAgents (office-scoped) |
| Permissions | `bot.configure`, `bot.use`, `notification.configure`, `notification.send` (already seeded) |
