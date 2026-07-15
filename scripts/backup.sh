#!/bin/bash
# SimbaFlow Nightly Backup Script
# Schedule via cron: 0 2 * * * /path/to/scripts/backup.sh
#
# Prerequisites:
#   - GPG key file at /etc/simbaflow/backup.key
#   - Docker container 'simbaflow-db' running

set -euo pipefail

BACKUP_DIR="/data/backups"
RETENTION_DAYS=30
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/simbaflow_${TIMESTAMP}.dump"
GPG_KEY_FILE="/etc/simbaflow/backup.key"

mkdir -p "${BACKUP_DIR}"

echo "[$(date)] Starting backup..."

# Dump all schemas (custom format for efficient restore)
docker exec simbaflow-db pg_dump -U simbaflow -Fc simbaflow > "${BACKUP_FILE}"

# Encrypt with GPG (symmetric AES-256)
if [ -f "${GPG_KEY_FILE}" ]; then
    gpg --symmetric --cipher-algo AES256 --batch --passphrase-file "${GPG_KEY_FILE}" "${BACKUP_FILE}"
    rm "${BACKUP_FILE}"
    echo "[$(date)] Backup encrypted: ${BACKUP_FILE}.gpg"
else
    echo "[$(date)] WARNING: GPG key not found at ${GPG_KEY_FILE}. Backup NOT encrypted: ${BACKUP_FILE}"
fi

# Remove old backups beyond retention period
find "${BACKUP_DIR}" -name "simbaflow_*.dump*" -mtime +${RETENTION_DAYS} -delete

echo "[$(date)] Backup complete. Retention: ${RETENTION_DAYS} days."
