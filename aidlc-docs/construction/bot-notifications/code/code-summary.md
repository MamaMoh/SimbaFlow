# Unit 7 Code Gen — Bot & Notifications (Batch 4 summary)

## Scope (phased)

Telegram-first bot + stage push + SignalR mount. **Deferred:** WhatsApp, `/medical`, `/arrived`, full notification rule/template CRUD, in-app inbox.

## What was implemented

### Backend
- **Telegram options** — env/appsettings binding (`TelegramOptions`); token never UI-managed
- **Platform entities** — `BotRegistrationChallenge`, `NotificationDelivery` + indexes
- **Long-poll host** — `TelegramPollingService` + HTTP `TelegramGateway` (no Telegram NuGet)
- **Commands** — `/link`, `/status`, `/lang`, `/cv`; deferred write commands return help only
- **Push** — `NotificationPushService` hooked from `CandidateStageChangedHandler`; `TelegramCandidateNotifier` replaces NoOp
- **APIs** — `BotModule`: status, test, link-code, unlink, deliveries
- **`BotNotificationRules`** — shared pure helpers (lang, sanitize, tenant scope, deferred cmds)

### Frontend
- `/admin/bot` — connection status, test, WhatsApp stub, recent deliveries
- `/settings` — Telegram link code + unlink UX
- SignalR — `SignalRProvider` + `NotificationListener` mounted in `AuthProvider`
- Nav — “Bot & Notifications”; client `lib/api/bot.ts`

## Test coverage
- Example: `BotNotificationServiceTests.cs` (TEST-70–78 + unlink)
- FsCheck: `BotNotificationProperties.cs` (TEST-70–78)
- SignalR handler tests updated for push dependency
- Playwright: `/admin/bot`, settings Telegram section, SignalR mount smoke (suite non-crash)
- **Results (2026-07-30)**: **135/135** backend; **32/32** Playwright

## Files of interest
- `Domain/Services/BotNotificationRules.cs`
- `Infrastructure/Services/Bot/*`
- `API/Features/Bot/BotModule.cs`
- `frontend/app/(main)/admin/bot/page.tsx`
- `frontend/app/(main)/settings/page.tsx`
- `frontend/lib/api/bot.ts`
- `frontend/components/auth/auth-provider.tsx`
