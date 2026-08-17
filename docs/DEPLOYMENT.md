# SimbaFlow deployment — 91.99.100.0

SimbaFlow runs as its **own isolated Docker Compose project** alongside two unrelated stacks on this
server. It deliberately does not use ports 80/443 and makes no change to `simba-nginx`, which
terminates TLS for every `laba.et` subdomain.

| | Value |
|---|---|
| App URL | http://91.99.100.0:3020 |
| Location | `/opt/simbaflow` (compose + `.env`), code in `/opt/simbaflow/repo` |
| Compose project | `simbaflow` |
| Containers | `simbaflow-web` (3020→3000, public), `simbaflow-api` (127.0.0.1:8100→8080), `simbaflow-db` (127.0.0.1:5435→5432) |
| Restart policy | `unless-stopped` (survives reboot) |

## Do not disturb — other stacks on this box
| Project | Dir | Serves |
|---|---|---|
| `deployment` | `/opt/simba-rental/deployment` | laba.et, www, owner., admin., api. + **simba-nginx (80/443)** + SQL Server + certbot |
| `deploy` | `/opt/visaassist/deploy` | visa.laba.et, api.visa.laba.et |

Ports already taken: 80, 443, 1433, 3000, 3001, 3002, 3010, 5434, 8080, 8090.
SimbaFlow uses **3020 / 8100 / 5435** to avoid all of them.

## Secrets
`/opt/simbaflow/.env` (chmod 600, never committed) holds `DB_PASSWORD`, `JWT_KEY`,
`NEXTAUTH_SECRET`, `PUBLIC_URL` — all generated on the server with `openssl rand`.
The Telegram bot is **disabled** in this deployment (`Telegram__Enabled=false`); supply
`Telegram__BotToken` via the environment if you enable it.

## Operations
```bash
ssh -i ~/.ssh/simba_deploy_key root@91.99.100.0

cd /opt/simbaflow
docker compose ps                 # status
docker compose logs -f api        # logs
docker compose restart web        # restart one service

# Deploy a new version
cd /opt/simbaflow/repo && git pull
cd /opt/simbaflow && docker compose build && docker compose up -d
```

Database migrations run on startup because `Database__MigrateOnStartup=true` is set in the compose
file for this deployment.

## Still to do before real production use
1. **HTTPS + domain.** Currently plain HTTP on a port, so credentials cross the network in clear
   text. Add a DNS A record (e.g. `app.laba.et → 91.99.100.0`), then a vhost in `simba-nginx` plus a
   certbot cert. That edits the config shared with laba.et — back it up and `nginx -t` before reload.
2. **Change the seeded admin password** (`admin` / `Admin@123!`) immediately.
3. **Decide `Mfa:Enforce`** — MFA is implemented but off by default, and the enrolment UI is not built.
4. **Backups** for the `simbaflow-db` volume.
5. **Disk** is at 85%; prune old images periodically (`docker image prune`).
