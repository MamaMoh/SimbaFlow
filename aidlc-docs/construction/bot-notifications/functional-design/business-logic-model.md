# Business Logic Model — Unit 7: Bot & Notifications

## Components

```
BotModule (Carter)                    ← config status, test ping, registration helper APIs
TelegramPollingService (hosted)       ← long-poll getUpdates; dispatch commands
TelegramBotClient (Infrastructure)    ← sendMessage, sendDocument
ICandidateNotifier → Telegram…        ← Departure Mark Notified
NotificationPushService               ← stage-change → Telegram (lightweight map)
SignalR (existing) + Frontend mount   ← toasts
```

---

## BL-B01: Connect Telegram (US-9.01)

```
1. Admin (bot.configure) opens Bot Configuration
2. Token supplied via env/appsettings (or one-time secure set — v1: env only)
3. "Test connection" → getMe; update BotChannelConfig status
4. Hosted service starts polling only when Telegram enabled + token present
```

## BL-B02: Register bot user (US-9.09)

```
1. Field agent opens web: Settings/Bot link → generates one-time code
   OR sends /register <username> and completes verify in web
2. Preferred v1 flow:
   a. Agent: /start or /register
   b. Bot asks for work email / username
   c. System sends 6-digit code to… (web toast or email) OR agent enters code shown in web “Link Telegram” page
3. On success: ApplicationUser.TelegramChatId = chatId; language default en
4. Require bot.use (or FieldAgent role) to complete link
```

**Recommended UX**: `/admin/bot` or `/settings/bot` “Link my Telegram” shows code; agent messages `/link CODE` to bot.

## BL-B03: /status (US-9.03)

```
1. Resolve chatId → User + TenantId
2. Parse passport or name query
3. Tenant-scoped Candidate lookup (active)
4. Reply: name, stage, status, last update — in PreferredBotLanguage
5. Audit: log delivery row (optional) + existing audit if available
6. SLA: respond < 3s p95 (NFR later)
```

## BL-B04: /lang (US-9.08)

```
/lang am | /lang en → persist preference; confirm in that language
```

## BL-B05: /cv (US-9.07)

```
1. Resolve candidate by passport
2. Reuse existing CV generation service (Unit 2)
3. sendDocument PDF to chat
4. Fail gracefully if CV service unavailable
```

## BL-B06: Stage-change push (US-9.04 lightweight)

```
On CandidateStageChanged (existing handler):
1. SignalR broadcast (unchanged)
2. Find users with TelegramChatId in same tenant (+ optional office filter)
3. Send short Telegram message (EN/AM template strings in code)
4. Write NotificationDelivery Sent/Failed
```

## BL-B07: Departure notify (replace NoOp)

```
MarkNotified command → ICandidateNotifier.NotifyDepartureAsync
TelegramCandidateNotifier sends to candidate’s linked agents / configured recipients
Status “Notified” still set by travel command as today
```

## BL-B08: SignalR UI (US-11.03)

```
1. Mount SignalRProvider + NotificationListener in (main) layout
2. Toast on candidateUpdated / notification events
3. No new hub methods required for v1
```

---

## Deferred logic

- WhatsApp send/receive
- `/medical`, `/arrived` write commands
- Full NotificationRule/Template admin
- Webhook mode
