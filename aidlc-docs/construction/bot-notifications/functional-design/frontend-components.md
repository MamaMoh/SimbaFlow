# Frontend Components — Unit 7: Bot & Notifications

## Pages

| Route | Purpose |
|-------|---------|
| `/admin/bot` | Telegram connection status, enable toggle, test ping, WhatsApp “later” stub |
| `/settings/bot` (or section on `/settings`) | **Link my Telegram** — show one-time code + instructions |
| Delivery list (optional v1) | `/admin/bot/deliveries` — recent NotificationDelivery rows |

**Deferred**: full notification rule builder, WhatsApp connect form, template editor.

Nav: under Administration — “Bot & notifications” (`bot.configure` / `notification.configure`).

---

## Bot config (`/admin/bot`)

| Section | Content |
|---------|---------|
| Telegram | Status badge, Last connected, Enable switch, Test connection button |
| Token | “Configured via server environment” (no plaintext input in v1 unless approved later) |
| WhatsApp | Disabled card: “Planned — credentials via env when enabled” |
| Help | Link instructions for field agents |

---

## Link Telegram (`/settings/bot`)

- Generate / refresh link code (expires)
- Steps: open Telegram bot → send `/link <code>`
- Shows current linked status (chat id masked) + Unlink

---

## SignalR

- Mount `SignalRProvider` + `NotificationListener` in authenticated layout
- Reuse sonner toasts; no new inbox UI in v1 (header bell may stay decorative)

---

## API clients

```
lib/api/bot.ts            // status, test, link-code, unlink, deliveries?
```

---

## Out of scope UI

- WhatsApp credential form
- Notification rule CRUD
- In-app notification center / bell drawer
