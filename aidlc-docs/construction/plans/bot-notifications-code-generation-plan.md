# Code Generation Plan — Unit 7: Bot & Notifications

## Unit Context
- **Unit**: Bot & Notifications (Unit 7) — phased Telegram-first delivery
- **Workspace Root**: `/Users/mama/Dev/simbaflow`
- **Stories**: US-9.01, US-9.03, US-9.04, US-9.07, US-9.08, US-9.09, US-11.03
- **Deferred**: US-9.02 WhatsApp connection, US-9.05 `/medical`, US-9.06 `/arrived`, full notification rule/template CRUD
- **Dependencies**: Unit 1 (SignalR), Unit 2 (candidate queries + CV), Unit 4 (`ICandidateNotifier` seam)
- **Design decisions (approved)**:
  - Telegram first; WhatsApp stub only
  - Long-polling `BackgroundService`
  - Token via env / appsettings only
  - Lightweight event-to-channel push map
  - Core commands only: `/link`, `/status`, `/lang`, `/cv`
  - Mount SignalR provider/listener in authenticated layout
  - No new Docker services

## Permission code alignment

Reuse existing `PermissionSeeder` codes:

| Use | Code |
|-----|------|
| Bot admin/config | `bot.configure` |
| Bot user linking / commands | `bot.use` |
| Delivery monitoring | `notification.configure` |
| Candidate read for `/status` / `/cv` | `candidate.read` |

---

## Code Generation Steps

### Phase A: Platform model + config

- [x] **Step 1**: Add Telegram options + settings binding — DONE (2026-07-30)
  - Update `backend/src/SimbaFlow.API/appsettings.json`
  - Add `Telegram` options class
  - Bind in infrastructure DI

- [x] **Step 2**: Add platform entities for bot link + delivery log — DONE (2026-07-30)
  - Create `BotRegistrationChallenge`
  - Create `NotificationDelivery`
  - Update `PlatformDbContext` / `IPlatformDbContext`

- [x] **Step 3**: Add migration + indexes — DONE (2026-07-30)
  - Platform migration for new tables
  - Index `AspNetUsers.TelegramChatId` if missing
  - Index `NotificationDeliveries (TenantId, SentAt)`

### Phase B: Telegram backend services

- [x] **Step 4**: Add Telegram gateway + bot service abstractions — DONE (2026-07-30)
  - `ITelegramCommandDispatcher`
  - poller state / options helpers
  - raw Telegram HTTP gateway (no extra package required)

- [x] **Step 5**: Implement link-code and command flow — DONE (2026-07-30)
  - `/link` consume challenge
  - `/status`
  - `/lang`
  - `/cv`
  - reject `/medical` and `/arrived` with help text

- [x] **Step 6**: Implement `TelegramPollingService` — DONE (2026-07-30)
  - long-poll `getUpdates`
  - dispatch commands
  - backoff + error state

### Phase C: Notifications + module APIs

- [x] **Step 7**: Replace `NoOpCandidateNotifier` with `TelegramCandidateNotifier` — DONE (2026-07-30)
  - keep travel “Mark Notified” non-blocking on send failure

- [x] **Step 8**: Add stage-change push service + delivery logging — DONE (2026-07-30)
  - hook existing `CandidateStageChangedHandler`
  - optional personal SignalR notification reuse

- [x] **Step 9**: Add `BotModule` API — DONE (2026-07-30)
  - `GET /api/bot/status`
  - `POST /api/bot/test`
  - `POST /api/bot/link-code`
  - `DELETE /api/bot/link`
  - `GET /api/bot/deliveries`

### Phase D: Frontend

- [x] **Step 10**: Add bot API client + types — DONE (2026-07-30)
  - `frontend/lib/api/bot.ts`

- [x] **Step 11**: Mount SignalR provider/listener in app layout — DONE (2026-07-30)
  - wrap authenticated UI with `SignalRProvider`
  - mount `NotificationListener`

- [x] **Step 12**: Build `/admin/bot` — DONE (2026-07-30)
  - connection status
  - test connection
  - WhatsApp deferred stub
  - delivery list / recent sends

- [x] **Step 13**: Build bot link UX under settings — DONE (2026-07-30)
  - link code
  - unlink
  - instructions for Telegram
  - optional route `/settings/bot` or section in existing settings page

- [x] **Step 14**: Add nav entry and polish toasts — DONE (2026-07-30)
  - “Bot & notifications”
  - personal notification toast behavior

### Phase E: Tests + docs

- [x] **Step 15**: Example-based tests — DONE (2026-07-30)
  - `BotNotificationServiceTests.cs`
  - link code, tenant scope, notifier non-rollback

- [x] **Step 16**: FsCheck properties TEST-70–78 — DONE (2026-07-30)
  - `BotNotificationProperties.cs`

- [x] **Step 17**: Playwright — DONE (2026-07-30)
  - bot admin page
  - settings bot link page
  - SignalR mount smoke (non-crash)

- [x] **Step 18**: Code summary — DONE (2026-07-30)
  - `aidlc-docs/construction/bot-notifications/code/code-summary.md`

---

## Recommended execution batches

| Batch | Steps | Rationale |
|-------|-------|-----------|
| 1 | 1–3 | Config, entities, migration foundation |
| 2 | 4–9 | Telegram services, notifier, push, BotModule |
| 3 | 10–14 | Frontend bot admin/settings + SignalR mount |
| 4 | 15–18 | Tests, Playwright, summary |

---

## Story Traceability

| Story | Steps |
|-------|-------|
| US-9.01 Telegram connection | 1, 4, 6, 9, 12 |
| US-9.03 Status lookup | 5, 9 |
| US-9.04 Stage push | 8, 12, 14 |
| US-9.07 CV via bot | 5 |
| US-9.08 Language | 5, 13 |
| US-9.09 Bot registration | 2, 5, 9, 13 |
| US-11.03 SignalR notifications | 11, 14 |
| US-9.02 / 9.05 / 9.06 | Deferred |

---

## Out of scope (explicit)

- WhatsApp receive/send implementation
- Telegram webhook mode
- Full notification rule/template CRUD
- In-app notification inbox / bell center
- Bot write commands for medical and arrival
- Multi-instance poller leader election beyond config flag

---

## Estimated artifacts

| Area | Create | Modify |
|------|--------|--------|
| Backend services / modules | ~10 | ~8 |
| Frontend | ~5 | ~4 |
| Tests | ~2 | ~1 |
| Docs | 1 | aidlc-state/audit |

**Total**: ~25–30 files touched (brownfield seams + new bot surfaces).
