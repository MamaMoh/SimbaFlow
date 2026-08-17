# NFR Design — Unit 7: Bot & Notifications

Builds on Unit 1 SignalR, Unit 4 `ICandidateNotifier`, and Unit 7 NFR Requirements (PERF/SEC/RES/TEST-70–78). Specifies Telegram long-polling, lightweight push, delivery logging, bot admin/link APIs, and frontend SignalR mount.

---

## 1. Module architecture

```
TelegramPollingService (BackgroundService)
        │ getUpdates
        ▼
TelegramCommandDispatcher
  /link /status /lang /cv  (+ reject /medical /arrived)
        │
        ├── ITenantDbContext (status/CV)
        ├── UserManager / PlatformDbContext (chat link)
        └── ITelegramBotClient (send)

CandidateStageChangedHandler (existing)
        │ after SignalR broadcast
        ▼
INotificationPushService.PushStageChangedAsync  → best-effort Telegram
        │
        ▼
NotificationDelivery rows

MarkNotified (Travel) → ICandidateNotifier → TelegramCandidateNotifier

BotModule (Carter)
  GET  /api/bot/status
  POST /api/bot/test
  POST /api/bot/link-code
  DELETE /api/bot/link
  GET  /api/bot/deliveries (optional)

Frontend layout → SignalRProvider + NotificationListener
```

---

## 2. Configuration & secrets (SEC-70/71)

```json
"Telegram": {
  "BotToken": "",
  "Enabled": true,
  "LongPollTimeoutSeconds": 25,
  "BotUsername": ""
}
```

- Bind via `IOptions<TelegramOptions>`; token from env `Telegram__BotToken`
- Status API returns `{ configured: bool, enabled, connectionStatus, botUsername?, lastError? }` — **never** token
- WhatsApp section present but unused (`Enabled: false`)

---

## 3. Polling service (PERF-73, RES-72–74, SCALE-73)

```csharp
class TelegramPollingService : BackgroundService {
  // if !Enabled || string.IsNullOrEmpty(token) → idle loop sleep 5s
  // else TelegramBotClient.ReceiveAsync / GetUpdates with offset
  // persist last UpdateId in memory (v1) or SystemConfiguration
  // on exception: log, set BotChannelConfig.Error, exponential backoff (1s→30s)
}
```

**SCALE-73:** Document single-replica assumption; if scaled out, disable poller on all but one instance via config `Telegram:PollingEnabled`.

---

## 4. BotModule API map

| Method | Path | Auth | NFR |
|--------|------|------|-----|
| GET | `/api/bot/status` | `bot.configure` or `bot.use` (subset) | PERF-74 |
| POST | `/api/bot/test` | `bot.configure` | PERF-75 `getMe` |
| POST | `/api/bot/link-code` | authenticated + `bot.use` | PERF-76; SEC-75 |
| DELETE | `/api/bot/link` | `bot.use` | Clear TelegramChatId |
| GET | `/api/bot/deliveries` | `notification.configure` | Paginated recent |

Link-code response: `{ code, expiresAt }` — store `BotRegistrationChallenge`.

Bot inbound: `/link CODE` consumes challenge, sets `ApplicationUser.TelegramChatId`.

---

## 5. Command handlers (PERF-70/71, SEC-72/77)

| Command | Behavior |
|---------|----------|
| `/start` | Help + link instructions |
| `/link <code>` | Consume challenge |
| `/status <passport\|name>` | Tenant lookup; localized reply |
| `/lang en\|am` | Persist preference |
| `/cv <passport>` | Generate PDF + sendDocument |
| `/medical`, `/arrived` | Static “use web app” (TEST-77) |

Unresolved chatId → instructions only (TEST-70). Cross-tenant impossible by design (TEST-71).

---

## 6. Push & notifier (RES-70/71, PERF-72)

```csharp
// After successful stage transition commit:
_ = push.PushStageChangedAsync(...); // fire-and-forget or IHostedService queue

// TelegramCandidateNotifier.NotifyDepartureAsync:
try { send; delivery=Sent; }
catch { delivery=Failed; /* do not throw to caller */ }
```

Recipients: users in tenant with non-null `TelegramChatId` and `bot.use` (optional office filter later).

---

## 7. Data model (platform)

| Entity | Notes |
|--------|-------|
| `BotRegistrationChallenge` | UserId, Code, ExpiresAt, ConsumedAt |
| `NotificationDelivery` | TenantId, UserId?, Channel, EventType, Status, Error (sanitized) |
| `BotChannelConfig` | Optional; may use SystemConfiguration JSON for Telegram status |

Migration: platform schema only. Index `(TelegramChatId)` on users if not present; `(TenantId, SentAt)` on deliveries.

---

## 8. Frontend (USAB-70–74, PERF-77)

| Route | Purpose |
|-------|---------|
| `/admin/bot` | Status, enable display, Test, WhatsApp stub, deliveries link |
| `/settings/bot` | Link code + unlink |

Layout (authenticated): wrap with `SignalRProvider`; render `NotificationListener`.

---

## 9. PBT architecture (TEST-70–78)

| Property | Assert |
|----------|--------|
| UnlinkedNoData | No candidate fields in reply |
| TenantScopedStatus | Passport in other tenant → not found |
| LinkCodeSingleUse | Second consume fails |
| DeliveryEnum | Status always valid |
| PushDoesNotRollback | Simulated push fail after commit OK |
| LangEnum | Only en/am stored |
| ErrorNoToken | Error strings scrub token substring |
| WriteCommandsNoOp | Medical/arrived → no DB candidate change |
| NotifierRegistered | DI resolves non-NoOp in production config |

Example tests: `BotNotificationServiceTests.cs`; properties: `BotNotificationProperties.cs`.

---

## 10. Observability

- Log: poller start/stop, command type, tenantId, userId, duration — not message body PII dumps
- Metric placeholders: `bot_commands_total`, `bot_push_failed_total`
- ConnectionStatus for admin UI

---

## 11. Out of scope

- WhatsApp Business API
- Webhook mode
- NotificationRule/Template admin
- `/medical`, `/arrived` mutations
- Multi-instance poller leader election
- In-app notification inbox
