# NFR Requirements — Unit 7: Bot & Notifications

Inherits Unit 1–6 NFR baselines. Adds targets for Telegram long-polling bot, lightweight push, departure notifier, delivery logging, and SignalR UI mount. **Out of scope for this unit’s NFRs:** WhatsApp send/receive, full NotificationRule CRUD, `/medical`/`/arrived` bot writes, in-app notification center.

**FD decisions:** phased Telegram first; long-polling host; lightweight event→channel map; core commands; mount SignalR; **token via env/appsettings only** (masked admin status).

## NFR-PERF: Performance Requirements

| ID | Requirement | Target | Context |
|----|-------------|--------|---------|
| PERF-70 | Bot `/status` reply | < 3s p95 | Tenant candidate lookup + Telegram send |
| PERF-71 | Bot `/cv` reply (PDF) | < 10s p95 | Reuse Unit 2 CV gen + sendDocument |
| PERF-72 | Stage-change Telegram push (fan-out ≤20) | < 30s wall | After workflow commit; async best-effort |
| PERF-73 | Telegram `getUpdates` poll cycle | ≤ 2s idle wait | Long-poll timeout configurable |
| PERF-74 | Bot config status API | < 200ms p95 | No live Telegram call unless Test |
| PERF-75 | Test connection (`getMe`) | < 5s p95 | One outbound HTTPS |
| PERF-76 | Link-code generate | < 150ms p95 | Challenge row insert |
| PERF-77 | SignalR reconnect / toast | < 2s after event | Existing hub; layout mount |

## NFR-SCALE: Scalability Requirements

| ID | Requirement | Target | Strategy |
|----|-------------|--------|----------|
| SCALE-70 | Linked Telegram users per tenant | 500+ | Index TelegramChatId |
| SCALE-71 | Concurrent bot commands | 20+ | Stateless handlers; tenant DB |
| SCALE-72 | NotificationDelivery retention | 90 days / 100k+ rows | Append-only; optional purge job later |
| SCALE-73 | Single poller process | 1 per deployment | Hosted service; no multi-instance race without offset lock (v1 single replica) |
| SCALE-74 | Stage push recipients | Office or tenant FieldAgents with chat id | Cap batch send; skip unlinkeds |

## NFR-SEC: Security Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| SEC-70 | Bot token never in API responses | Masked configured/missing only |
| SEC-71 | Token only from env/appsettings | No browser paste in v1 |
| SEC-72 | Commands tenant-scoped via linked user | Resolve chatId → User → TenantId |
| SEC-73 | Link requires `bot.use` | Challenge + consume |
| SEC-74 | Config/test requires `bot.configure` | Admin route |
| SEC-75 | Link codes short-lived | ≤ 10 min; single use |
| SEC-76 | No PII in delivery logs beyond need | Name + passport last4 max in summary |
| SEC-77 | Write bot commands rejected in v1 | `/medical`/`/arrived` → help text |

## NFR-RES: Resiliency Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| RES-70 | Telegram outage must not fail workflow transitions | Push after commit; catch/log |
| RES-71 | Mark Notified succeeds if Telegram send fails | Log Failed; travel status still Notified |
| RES-72 | Poller survives transient Telegram errors | Backoff + continue; set ConnectionStatus Error |
| RES-73 | Missing token → poller idle | Status Disconnected; no crash loop |
| RES-74 | Duplicate update offset handling | Persist last update_id |
| RES-75 | SignalR disconnect does not block pages | Listener best-effort |
| RES-76 | WhatsApp path Skipped if invoked | No throw |

## NFR-TEST: PBT Requirements (Unit-Specific)

| ID | Requirement | PBT Rule | Implementation |
|----|-------------|----------|----------------|
| TEST-70 | Unlinked chat cannot run /status | PBT-04 | Reply instructions; no data leak |
| TEST-71 | Status lookup always tenant-scoped | PBT-03 | No cross-tenant passport hit |
| TEST-72 | Link code single-use + expiry | PBT-04 | Second consume fails |
| TEST-73 | Delivery status ∈ {Pending,Sent,Failed,Skipped} | PBT-03 | Enum invariant |
| TEST-74 | Stage push failure ≠ workflow rollback | PBT-03 | Transition committed independently |
| TEST-75 | Lang preference only en\|am | PBT-04 | Invalid → keep previous |
| TEST-76 | Token never appears in Delivery.Error | PBT-03 | Sanitize |
| TEST-77 | /medical and /arrived do not mutate | PBT-03 | No side effects in v1 |
| TEST-78 | Notifier NoOp replaced — interface contract | PBT-03 | Telegram impl registered |

## NFR-USAB: Usability Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| USAB-70 | Admin shows Connected/Disconnected/Error | Badge + last error snippet |
| USAB-71 | Field agent link page: code + steps | `/settings/bot` |
| USAB-72 | Bot replies in preferred language | Templates en/am |
| USAB-73 | WhatsApp labeled deferred | Stub card |
| USAB-74 | SignalR toasts non-blocking | sonner |
| USAB-75 | Clear “use web app” for deferred commands | Bot text |

## Tech Stack Additions (Unit 7-Specific)

| Package / piece | Purpose |
|-----------------|---------|
| `Telegram.Bot` (or raw HttpClient) | Official client preferred for getUpdates/sendMessage/sendDocument |
| Hosted `BackgroundService` | Long-polling |
| Platform tables | `BotChannelConfig` and/or `BotRegistrationChallenge`, `NotificationDelivery` |
| `Telegram:BotToken` config | Env binding |
| Frontend mount | `SignalRProvider` + `NotificationListener` |

## Testable Properties Summary

1. Tenant isolation on every bot read  
2. Link code expiry / single-use  
3. Workflow unaffected by Telegram failure  
4. Token never leaked via API or delivery errors  
5. Deferred write commands are no-ops  
