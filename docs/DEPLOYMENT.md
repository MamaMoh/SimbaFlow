# SimbaFlow deployment — 91.99.100.0

SimbaFlow runs as its **own isolated Docker Compose project** alongside two unrelated stacks on this
server. It does not own ports 80/443; TLS is terminated by the existing `simba-nginx`, which we
extend **additively** with one new vhost file and never edit in place.

| | Value |
|---|---|
| App URL | **https://app.laba.et** |
| Fallback | http://91.99.100.0:3020 (still works; useful if nginx is down) |
| Location | `/opt/simbaflow` (compose + `.env`), code in `/opt/simbaflow/repo` |
| Compose project | `simbaflow` |
| Containers | `simbaflow-web` (3020→3000), `simbaflow-api` (127.0.0.1:8100→8080), `simbaflow-db` (127.0.0.1:5435→5432) |
| Restart policy | `unless-stopped` (survives reboot) |

## Do not disturb — other stacks on this box
| Project | Dir | Serves |
|---|---|---|
| `deployment` | `/opt/simba-rental/deployment` | laba.et, owner., admin., api. + **simba-nginx (80/443)** + SQL Server + certbot |
| `deploy` | `/opt/visaassist/deploy` | visa.laba.et, api.visa.laba.et |

Ports already taken: 80, 443, 1433, 3000, 3001, 3002, 3010, 5434, 8080, 8090.
SimbaFlow uses **3020 / 8100 / 5435** to avoid all of them.

Known pre-existing breakage on this box, unrelated to SimbaFlow: `www.laba.et` and `api.laba.et`
return TLS errors because the `laba.et` certificate's SAN covers only `laba.et`, `admin.laba.et`
and `owner.laba.et`.

## Domain and TLS

DNS: `app` A → `91.99.100.0` in the `laba.et` zone (Hetzner). The zone name is appended
automatically — the record's Name is `app`, **not** `app.laba.et`.

nginx: `/opt/simba-rental/deployment/nginx/conf.d/simbaflow.conf` — a **new file** added to the
shared `conf.d`. It declares `server_name app.laba.et` only, proxies to `simbaflow-web:3000`, sets
`client_max_body_size 25m`, serves `/.well-known/acme-challenge/` from the certbot webroot, and
301s HTTP→HTTPS. `default.conf` and every laba.et vhost are untouched.

Before any nginx change: back up the shared config and validate before reloading.
```bash
tar czf /root/nginx-confd-backup-$(date +%Y%m%d-%H%M%S).tar.gz -C /opt/simba-rental/deployment/nginx conf.d
docker exec simba-nginx nginx -t && docker exec simba-nginx nginx -s reload
```

Certificate: Let's Encrypt, `CN=app.laba.et`. **Do not** use `docker compose run certbot` — that
service's entrypoint is an infinite `certbot renew` loop, so it ignores the command and hangs
forever. Issue one-off certs with an explicit entrypoint instead:
```bash
docker run --rm --entrypoint certbot \
  -v /opt/simba-rental/deployment/certbot/conf:/etc/letsencrypt \
  -v /opt/simba-rental/deployment/certbot/www:/var/www/certbot \
  certbot/certbot certonly --webroot -w /var/www/certbot \
  -d app.laba.et --email <you@example.com> --agree-tos --no-eff-email
```
Renewal is automatic — the running `simba-certbot` loop renews `app.laba.et` along with the rest.

## Networking gotcha (cost an outage and a broken login)

`simbaflow-web` must sit on **two** networks: `simbaflow-net` (to reach the API and DB) and
`deployment_default` (so `simba-nginx` can reach it). Two things follow:

1. The second network is declared in the compose file as `external: true`. Attaching it by hand with
   `docker network connect` does **not** survive `docker compose up -d web`, which silently recreates
   the container without it → instant 502.
2. `deployment_default` is shared, and the VisaAssist stack has a service named `api`, so the
   hostname `api` resolves to **their** container from inside `simbaflow-web`. The frontend must
   address the backend as **`http://simbaflow-api:8080`** (container name), never `http://api:8080`.

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
1. **Change the seeded admin password** (`admin` / `Admin@123!`) — now reachable from the public
   internet, so this is the top priority.
2. **Decide `Mfa:Enforce`** — MFA is implemented but off by default, and the enrolment UI is not built.
3. **Backups** for the `simbaflow-db` volume.
4. **Disk** is at 85%; prune old images periodically (`docker image prune`).
