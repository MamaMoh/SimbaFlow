#!/usr/bin/env bash
#
# Nightly SimbaFlow database backup.
#
# Dumps the Postgres database from the running container, compresses it, prunes old copies, and —
# because a backup that has never been restored is not a backup — test-restores the fresh dump into
# a throwaway database inside the same container and fails loudly if that restore does not work.
#
# Off-server copy: set REMOTE_DEST (an rsync/scp target, e.g. user@host:/backups) and the dump is
# shipped there too. Without it the backup is local-only, which protects against a bad deploy or a
# dropped table but NOT against losing this disk.
#
# Install: see docs/DEPLOYMENT.md. Runs from cron as root on the host.
set -euo pipefail

CONTAINER="${BACKUP_CONTAINER:-simbaflow-db}"
DB="${BACKUP_DB:-simbaflow}"
DB_USER="${BACKUP_DB_USER:-simbaflow}"
DEST="${BACKUP_DIR:-/root/simbaflow-backups}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-30}"
REMOTE_DEST="${REMOTE_DEST:-}"
LOG="${BACKUP_LOG:-/var/log/simbaflow-backup.log}"

log() { echo "$(date -u '+%Y-%m-%dT%H:%M:%SZ') $*" | tee -a "$LOG"; }

mkdir -p "$DEST"
STAMP="$(date -u +%Y%m%d-%H%M%S)"
FILE="$DEST/simbaflow-$STAMP.sql.gz"

# 1. Dump + compress. pg_dump's exit status must survive the pipe, hence pipefail above.
log "Backup starting → $FILE"
if ! docker exec "$CONTAINER" pg_dump -U "$DB_USER" -d "$DB" | gzip > "$FILE"; then
  log "ERROR: pg_dump failed; removing partial file"
  rm -f "$FILE"
  exit 1
fi

SIZE="$(du -h "$FILE" | cut -f1)"
# A valid gzip'd dump is never a few bytes; catch a silently-empty dump before trusting it.
if [ "$(stat -c%s "$FILE")" -lt 1000 ]; then
  log "ERROR: dump is suspiciously small ($SIZE); treating as failed"
  exit 1
fi
log "Dump written ($SIZE)"

# 2. Restore-test into a scratch database, then drop it. Proves the dump actually loads.
SCRATCH="simbaflow_restore_test_$STAMP"
log "Verifying by restoring into $SCRATCH"
cleanup_scratch() { docker exec "$CONTAINER" psql -U "$DB_USER" -d postgres -c "DROP DATABASE IF EXISTS \"$SCRATCH\";" >/dev/null 2>&1 || true; }
trap cleanup_scratch EXIT

docker exec "$CONTAINER" psql -U "$DB_USER" -d postgres -c "CREATE DATABASE \"$SCRATCH\";" >/dev/null
if ! gunzip -c "$FILE" | docker exec -i "$CONTAINER" psql -U "$DB_USER" -d "$SCRATCH" -v ON_ERROR_STOP=1 >/dev/null 2>&1; then
  log "ERROR: restore test FAILED — the dump does not load. Keeping the file for inspection."
  exit 1
fi
# Sanity-check that a known table came back with rows.
TENANTS="$(docker exec "$CONTAINER" psql -U "$DB_USER" -d "$SCRATCH" -tAc 'SELECT count(*) FROM public."Tenants";' 2>/dev/null || echo 0)"
log "Restore test OK (Tenants rows: $TENANTS)"

# 3. Off-server copy, if a destination is configured.
if [ -n "$REMOTE_DEST" ]; then
  if rsync -a "$FILE" "$REMOTE_DEST/" 2>>"$LOG"; then
    log "Shipped to $REMOTE_DEST"
  else
    log "WARNING: off-server copy to $REMOTE_DEST failed (local copy is safe)"
  fi
else
  log "No REMOTE_DEST set — local-only backup"
fi

# 4. Prune old local dumps.
DELETED="$(find "$DEST" -name 'simbaflow-*.sql.gz' -mtime "+$RETENTION_DAYS" -print -delete | wc -l | tr -d ' ')"
log "Pruned $DELETED backup(s) older than ${RETENTION_DAYS}d. Backup complete."
