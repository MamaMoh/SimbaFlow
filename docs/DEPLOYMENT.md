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

## Shared TLS: the `laba-multi` certificate

`www.laba.et` and `api.laba.et` used to fail TLS, and the `laba.et` certificate had **no certbot
renewal config** — it would have silently expired on 2026-09-22 and taken `laba.et`, `admin.`,
`owner.` and `api.` down with it. Both were pre-existing, and both are fixed.

Cause of the unrenewable cert: `live/laba.et/` contained **real files** where certbot expects
symlinks into `archive/`, so certbot refused to write there (`archive directory exists for laba.et`).
Rather than repair that in place on a live site, a fresh cert was issued under a **new name**:

| | |
|---|---|
| Cert name | `laba-multi` (certbot-managed, renewal config present, webroot auth) |
| Names | `laba.et`, `www.laba.et`, `admin.laba.et`, `owner.laba.et`, `api.laba.et` |
| Referenced by | 4 `ssl_certificate` pairs in `nginx/conf.d/default.conf` |

`live/laba.et/` and `archive/laba.et/` are left in place, orphaned and unreferenced — they are the
**rollback path**: point the four `ssl_certificate` pairs in `default.conf` back at
`/etc/letsencrypt/live/laba.et/` and reload. Do not delete them without a replacement plan.

`server_name` already listed `www.laba.et`, so no vhost routing change was needed — only the cert.

### Outstanding gap: nothing reloads nginx after renewal
The `simba-certbot` entrypoint is `while :; do certbot renew; sleep 12h; done` with **no deploy
hook**. Renewed certs are written to disk but nginx keeps serving the old ones until it restarts, so
every cert on this box (`laba-multi`, `visa.laba.et`, `app.laba.et`) can expire in production despite
renewing correctly. Fix with a host cron, e.g. weekly:
```bash
0 4 * * 1 docker exec simba-nginx nginx -s reload
```

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

## The laba.et deploy deletes this app's vhost

`app.laba.et` went down on 2026-09-01 with `ERR_CERT_COMMON_NAME_INVALID`, taking `www.laba.et`,
`api.laba.et` and both VisaAssist hostnames with it. The cause was not a certificate change:

```
rsync -avz --delete  …  E:/Personal/LabaRental/Facility-Rental/  root@91.99.100.0:/opt/simba-rental/
```

That is how the laba.et stack is deployed. `--delete` removes anything on the server that is not in
the developer's local tree, and `simbaflow.conf` and `visaassist.conf` only ever existed on the
server — so **every deploy of that stack deletes them**. With no vhost for `app.laba.et`, requests
fall through to the first `server` block and are answered with the `laba.et` certificate, which does
not name them. The same run overwrites `default.conf` with their copy, which still points at
`live/laba.et/` — a certificate covering only `laba.et`, `admin.` and `owner.`, so `www.` and `api.`
break at the same moment. Their exclude list covers `deployment/certbot`, which is why the
certificates themselves survive.

Recovery is one idempotent command on the host, safe to run at any time and never touching
`cafe.laba.et.conf`:

```bash
/root/restore-vhosts.sh
```

It restores both vhosts from `/root/vhosts/`, repoints `default.conf` back to `laba-multi`, and
reloads nginx only if something actually changed.

**The durable fix belongs on their side** — either add these to their rsync excludes:
```
--exclude='deployment/nginx/conf.d/simbaflow.conf'
--exclude='deployment/nginx/conf.d/visaassist.conf'
--exclude='deployment/nginx/conf.d/cafe.laba.et.conf'
```
or commit the three added vhosts into their own repo so rsync stops treating them as strays. Until
one of those happens, `restore-vhosts.sh` has to be run after each of their deploys.

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

## Backups

`deployment/backup.sh` (installed at `/opt/simbaflow/backup.sh`) runs nightly from cron at
**02:30 UTC**. Each run dumps the database from the container, gzips it to
`/root/simbaflow-backups`, **restore-tests the fresh dump** by loading it into a throwaway database
and checking a known table came back (a backup that has never been restored is not a backup), drops
the scratch database, prunes copies older than 30 days, and logs to `/var/log/simbaflow-backup.log`.
It fails loudly — non-zero exit, ERROR in the log — if the dump is empty or will not restore.

```bash
/opt/simbaflow/backup.sh            # run one on demand
tail /var/log/simbaflow-backup.log  # see the last runs
crontab -l                          # the nightly schedule
```

**Off-server copy is not yet configured.** The dumps live on the same disk as the database, so they
survive a bad deploy or a dropped table but NOT the loss of this host. To ship them off-box, set a
destination and the script rsyncs each dump there too:

```bash
# in cron or a wrapper, before calling backup.sh:
export REMOTE_DEST="user@backup-host:/simbaflow"   # any rsync/scp target, or an rclone remote
```

Pick a destination (a second Hetzner box, an S3/B2 bucket via rclone, etc.) and this becomes a true
off-site backup. Until then it is local-only.

## Still to do before real production use
1. **Change the seeded admin password** (`admin` / `Admin@123!`) — now reachable from the public
   internet, so this is the top priority.
2. **Decide `Mfa:Enforce`** — MFA is implemented but off by default, and the enrolment UI is not built.
3. **Off-server backup destination** — nightly restore-tested backups run locally (see above); set
   `REMOTE_DEST` to get them off this disk.
4. **SMTP password** — email/password-reset is built and deployed; it needs the Zoho password in
   `/opt/simbaflow/.env` as `SMTP_PASSWORD` (and `SMTP_SENDER` if the authenticating mailbox is not
   `support@laba.et`).
5. **Disk** is at 83%; prune old images periodically (`docker image prune`).
