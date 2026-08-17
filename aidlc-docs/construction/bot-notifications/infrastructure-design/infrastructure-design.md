# Infrastructure Design — Unit 7: Bot & Notifications

## Deployment context

Same Docker Compose stack (api + frontend + postgres). **No new containers.**

Telegram Bot API is outbound HTTPS from the API process (long-polling). Requires:

| Env | Purpose |
|-----|---------|
| `Telegram__BotToken` | Bot token (secret) |
| `Telegram__Enabled` | `true`/`false` (default false until configured) |
| `Telegram__PollingEnabled` | Default `true` when Enabled; set `false` on non-leader replicas |
| `Telegram__LongPollTimeoutSeconds` | Default `25` |

WhatsApp env keys remain placeholders only.

Infrastructure work:

1. Platform entities + migration (challenges, deliveries; optional channel config)
2. `Telegram.Bot` package + options binding
3. Hosted long-poll service + command dispatcher
4. Replace `NoOpCandidateNotifier` with Telegram impl
5. Hook stage-change → best-effort push
6. `BotModule` Carter endpoints
7. Frontend admin/settings + SignalR layout mount
8. Tests TEST-70–78

---

## 1. Database schema (platform)

### 1a. Existing (no change required for chat ids)

| Column | Table | Status |
|--------|-------|--------|
| `TelegramChatId` | AspNetUsers | Exists |
| `WhatsAppNumber` | AspNetUsers | Exists |
| `PreferredLanguage` | AspNetUsers | Exists (`en` default) — reuse for bot `/lang` |

**Index** (add if missing):

```sql
CREATE INDEX IF NOT EXISTS ix_users_telegram_chat
  ON "AspNetUsers" ("TelegramChatId")
  WHERE "TelegramChatId" IS NOT NULL;
```

### 1b. New tables

```
BotRegistrationChallenges
├── Id, CreatedAt, …
├── UserId : uuid (FK users)
├── Code : varchar(12)  // uppercase alphanumeric
├── ExpiresAt : timestamptz
├── ConsumedAt : timestamptz?
└── Index: Code (unique where ConsumedAt is null) or unique Code

NotificationDeliveries
├── Id, CreatedAt, …
├── TenantId : uuid
├── UserId : uuid?
├── Channel : int  // Telegram=0
├── EventType : varchar(64)
├── PayloadSummary : varchar(512)
├── Status : int  // Pending/Sent/Failed/Skipped
├── ExternalMessageId : varchar(128)?
├── Error : varchar(1024)?  // sanitized
├── SentAt : timestamptz?
└── IX (TenantId, SentAt DESC)
```

Optional `BotChannelConfigs` — **prefer** deriving status from in-memory poller + SystemConfiguration key `telegram.poller` JSON to avoid extra table. **v1 choice: skip BotChannelConfig table**; expose status from singleton poller state + options.

### 1c. Tenant schema

**No tenant migrations** for Unit 7 v1 (candidate reads use existing TenantDbContext).

---

## 2. Packages & DI

```xml
<!-- Infrastructure or API csproj -->
<PackageReference Include="Telegram.Bot" Version="22.*" />
```

```csharp
services.Configure<TelegramOptions>(config.GetSection("Telegram"));
services.AddSingleton<ITelegramBotClientFactory, TelegramBotClientFactory>();
services.AddSingleton<ITelegramPollerState, TelegramPollerState>();
services.AddScoped<ITelegramCommandDispatcher, TelegramCommandDispatcher>();
services.AddScoped<INotificationPushService, NotificationPushService>();
services.AddScoped<ICandidateNotifier, TelegramCandidateNotifier>(); // replaces NoOp
services.AddHostedService<TelegramPollingService>();
```

`appsettings.json`:

```json
"Telegram": {
  "BotToken": "",
  "Enabled": false,
  "PollingEnabled": true,
  "LongPollTimeoutSeconds": 25,
  "BotUsername": ""
}
```

---

## 3. Application services

| Service | Responsibility |
|---------|----------------|
| `TelegramPollingService` | Long-poll loop; dispatch updates |
| `TelegramCommandDispatcher` | Parse commands; authorize via chat→user |
| `TelegramCandidateNotifier` | Departure notify |
| `NotificationPushService` | Stage-change fan-out + delivery rows |
| `BotLinkService` | Create/consume link codes; unlink |

### Stage-change hook

In existing `CandidateStageChangedHandler` (after SignalR):

```csharp
try { await _push.PushStageChangedAsync(...); }
catch (Exception ex) { _logger.LogWarning(ex, "Telegram push failed"); }
```

Must not throw to MediatR pipeline in a way that rolls back (handler runs post-commit today — verify; if in-transaction, use fire-and-forget after commit).

---

## 4. API (BotModule)

| Method | Path | Permission |
|--------|------|------------|
| GET | `/api/bot/status` | `bot.configure` (full) / authenticated subset for link page |
| POST | `/api/bot/test` | `bot.configure` |
| POST | `/api/bot/link-code` | `bot.use` |
| DELETE | `/api/bot/link` | `bot.use` |
| GET | `/api/bot/deliveries?page=` | `notification.configure` |

No Telegram webhook route in v1.

---

## 5. Permissions

Reuse seeded codes — **no new PermissionSeeder rows**:

```
bot.configure / bot.use
notification.configure / notification.send
```

| Surface | Permission |
|---------|------------|
| `/admin/bot` | `bot.configure` |
| `/settings/bot` | authenticated + `bot.use` |
| Deliveries | `notification.configure` |

---

## 6. Frontend

| Path | Notes |
|------|-------|
| `app/(main)/admin/bot/page.tsx` | Status, test, WhatsApp stub |
| `app/(main)/settings/bot/page.tsx` or settings section | Link code UI |
| `lib/api/bot.ts` | Client |
| `nav-items.ts` | “Bot & notifications” |
| Authenticated layout | Mount `SignalRProvider` + `NotificationListener` |

---

## 7. Docker / ops

- Document token in `.env.example` (already has `TELEGRAM_BOT_TOKEN`)
- Compose already passes `Telegram__BotToken` — verify mapping matches options
- Health: API `/health` unchanged; bot status is separate admin API
- Multi-replica: set `Telegram__PollingEnabled=false` on secondary API instances

---

## 8. Tests & artifacts

| Artifact | Path |
|----------|------|
| Example | `Services/BotNotificationServiceTests.cs` |
| PBT | `Properties/BotNotificationProperties.cs` |
| Code summary | `construction/bot-notifications/code/code-summary.md` |

---

## 9. Risk register

| Risk | Mitigation |
|------|------------|
| Token leak | Env-only; scrub errors; never log token |
| Poller duplicate messages | Persist/update offset; idempotent /link |
| Telegram downtime | Best-effort push; Mark Notified still OK |
| Multi-instance double poll | `PollingEnabled` flag |
| CV send size/time | PERF-71 budget; timeout sendDocument |

---

## 10. Out of scope

- WhatsApp containers / Meta cloud API
- Webhook ingress / ngrok
- Redis queue for pushes (v1 inline/best-effort)
- New Docker services
