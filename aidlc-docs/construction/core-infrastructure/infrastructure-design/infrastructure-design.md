# Infrastructure Design — Unit 1: Core Infrastructure

## Deployment Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Host Machine (Linux/macOS)                                  │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Docker Compose                                         │ │
│  │                                                         │ │
│  │  ┌─────────────┐  ┌─────────────┐  ┌───────────────┐  │ │
│  │  │  api         │  │  frontend   │  │  postgres      │  │ │
│  │  │  .NET 10     │  │  Next.js 15 │  │  PostgreSQL 16 │  │ │
│  │  │  Port 8080   │  │  Port 3000  │  │  Port 5432     │  │ │
│  │  │  SignalR Hub  │  │  SSR + SPA  │  │  Multi-Schema  │  │ │
│  │  │  Bot Services │  │             │  │                │  │ │
│  │  └──────┬───────┘  └──────┬──────┘  └───────┬────────┘  │ │
│  │         │                  │                  │           │ │
│  │  ┌──────┴──────────────────┴──────────────────┴────────┐ │ │
│  │  │  Docker Network: simbaflow-net (bridge)              │ │ │
│  │  └─────────────────────────────────────────────────────┘ │ │
│  │                                                         │ │
│  │  Volumes:                                               │ │
│  │  ├── simbaflow-db    → /var/lib/postgresql/data         │ │
│  │  ├── simbaflow-files → /data                            │ │
│  │  └── simbaflow-logs  → /app/logs                        │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
│  Exposed Ports:                                              │
│  ├── 3000 → Frontend (user access)                          │
│  ├── 5000 → API (frontend proxy + direct API)               │
│  └── 5432 → PostgreSQL (local admin only, optional)         │
└─────────────────────────────────────────────────────────────┘
```

---

## Docker Compose Configuration

### File: `docker-compose.yml`

```yaml
version: "3.9"

services:
  postgres:
    image: postgres:16.4-alpine
    container_name: simbaflow-db
    restart: unless-stopped
    environment:
      POSTGRES_DB: simbaflow
      POSTGRES_USER: simbaflow
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    ports:
      - "5432:5432"
    volumes:
      - simbaflow-db:/var/lib/postgresql/data
      - ./scripts/init-db.sql:/docker-entrypoint-initdb.d/init.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U simbaflow"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - simbaflow-net

  api:
    image: simbaflow-api:${VERSION:-latest}
    build:
      context: ./backend
      dockerfile: Dockerfile
    container_name: simbaflow-api
    restart: unless-stopped
    ports:
      - "5000:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=simbaflow;Username=simbaflow;Password=${DB_PASSWORD};SSL Mode=Prefer"
      Jwt__Key: ${JWT_KEY}
      Jwt__Issuer: SimbaFlow
      Jwt__Audience: SimbaFlowApp
      Telegram__BotToken: ${TELEGRAM_BOT_TOKEN:-}
      WhatsApp__ApiUrl: ${WHATSAPP_API_URL:-}
      WhatsApp__ApiToken: ${WHATSAPP_API_TOKEN:-}
      FileStorage__BasePath: /data
      Cors__Origins__0: http://localhost:3000
    volumes:
      - simbaflow-files:/data
      - simbaflow-logs:/app/logs
    depends_on:
      postgres:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 30s
    networks:
      - simbaflow-net

  frontend:
    image: simbaflow-frontend:${VERSION:-latest}
    build:
      context: ./frontend
      dockerfile: Dockerfile
    container_name: simbaflow-frontend
    restart: unless-stopped
    ports:
      - "3000:3000"
    environment:
      NEXTAUTH_URL: http://localhost:3000
      NEXTAUTH_SECRET: ${NEXTAUTH_SECRET}
      NEXT_PUBLIC_API_URL: http://localhost:5000
      BACKEND_URL: http://api:8080
    depends_on:
      api:
        condition: service_healthy
    networks:
      - simbaflow-net

volumes:
  simbaflow-db:
    driver: local
  simbaflow-files:
    driver: local
  simbaflow-logs:
    driver: local

networks:
  simbaflow-net:
    driver: bridge
```

---

## Dockerfile — Backend API

### File: `backend/Dockerfile`

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.slnx .
COPY src/SimbaFlow.API/SimbaFlow.API.csproj src/SimbaFlow.API/
COPY src/SimbaFlow.Application/SimbaFlow.Application.csproj src/SimbaFlow.Application/
COPY src/SimbaFlow.Domain/SimbaFlow.Domain.csproj src/SimbaFlow.Domain/
COPY src/SimbaFlow.Infrastructure/SimbaFlow.Infrastructure.csproj src/SimbaFlow.Infrastructure/
COPY src/SimbaFlow.Shared/SimbaFlow.Shared.csproj src/SimbaFlow.Shared/
RUN dotnet restore
COPY . .
RUN dotnet publish src/SimbaFlow.API -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
RUN adduser -D -h /app appuser
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /data /app/logs && chown -R appuser:appuser /data /app/logs
USER appuser
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --retries=3 CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "SimbaFlow.API.dll"]
```

---

## Dockerfile — Frontend

### File: `frontend/Dockerfile`

```dockerfile
# Build stage
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts
COPY . .
RUN npm run build

# Runtime stage
FROM node:22-alpine AS runtime
RUN adduser -D -h /app appuser
WORKDIR /app
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public
USER appuser
EXPOSE 3000
ENV PORT=3000
ENV HOSTNAME="0.0.0.0"
CMD ["node", "server.js"]
```

**Note**: Next.js must be configured with `output: 'standalone'` in `next.config.mjs`.

---

## Database Initialization Script

### File: `scripts/init-db.sql`

```sql
-- Create public schema extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Ensure the application user has schema creation privileges
GRANT CREATE ON DATABASE simbaflow TO simbaflow;
```

---

## Backup Configuration

### File: `scripts/backup.sh`

```bash
#!/bin/bash
# Nightly backup script — run via cron: 0 2 * * * /path/to/backup.sh

BACKUP_DIR="/data/backups"
RETENTION_DAYS=30
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/simbaflow_${TIMESTAMP}.dump"

mkdir -p "${BACKUP_DIR}"

# Dump all schemas
docker exec simbaflow-db pg_dump -U simbaflow -Fc simbaflow > "${BACKUP_FILE}"

# Encrypt with GPG (symmetric)
gpg --symmetric --cipher-algo AES256 --batch --passphrase-file /etc/simbaflow/backup.key "${BACKUP_FILE}"
rm "${BACKUP_FILE}"

# Remove old backups
find "${BACKUP_DIR}" -name "*.dump.gpg" -mtime +${RETENTION_DAYS} -delete

echo "[$(date)] Backup completed: ${BACKUP_FILE}.gpg"
```

---

## Environment Variables

### File: `.env.example` (committed, template only)

```bash
# Database
DB_PASSWORD=change_me_in_production

# JWT (minimum 32 bytes)
JWT_KEY=change_me_minimum_32_characters_long_secret_key

# NextAuth
NEXTAUTH_SECRET=change_me_random_secret_for_nextauth

# Version tag for Docker images
VERSION=1.0.0

# Bot (optional — leave empty to disable)
TELEGRAM_BOT_TOKEN=
WHATSAPP_API_URL=
WHATSAPP_API_TOKEN=
```

---

## Network Security

| Source | Destination | Port | Protocol | Purpose |
|--------|-------------|------|----------|---------|
| Internet | Frontend | 3000 | HTTPS (via reverse proxy) | User access |
| Internet | API | 5000 | HTTPS (via reverse proxy) | Direct API / SignalR |
| Frontend (internal) | API | 8080 | HTTP | Server-side rendering proxy |
| API | PostgreSQL | 5432 | TCP (SSL) | Database access |
| API | Telegram API | 443 | HTTPS | Bot communication |
| API | WhatsApp API | 443 | HTTPS | Bot communication |

**Recommendation**: Place a reverse proxy (nginx/Caddy) in front for TLS termination in production.

---

## Monitoring & Observability

### Log Aggregation
- Logs written to `/app/logs/simbaflow-*.json` (rolling daily, 30-day retention)
- Docker logs available via `docker logs simbaflow-api`
- Structured JSON format parseable by any log aggregation tool

### Health Monitoring
- `/health` — shallow (process alive): suitable for Docker healthcheck
- `/health/ready` — deep (DB + schema + files): suitable for load balancer

### Metrics (Future Enhancement)
- Prometheus endpoint `/metrics` (add later with `prometheus-net`)
- Grafana dashboards for: request rate, response time, error rate, connection pool usage

---

## Disaster Recovery Runbook

### Restore from Backup

```bash
# 1. Stop the API (prevent writes during restore)
docker compose stop api frontend

# 2. Decrypt backup
gpg --decrypt --batch --passphrase-file /etc/simbaflow/backup.key \
  /data/backups/simbaflow_YYYYMMDD_HHMMSS.dump.gpg > /tmp/restore.dump

# 3. Drop and recreate database
docker exec -i simbaflow-db psql -U simbaflow -c "DROP DATABASE simbaflow;"
docker exec -i simbaflow-db psql -U simbaflow -c "CREATE DATABASE simbaflow;"

# 4. Restore
docker exec -i simbaflow-db pg_restore -U simbaflow -d simbaflow < /tmp/restore.dump

# 5. Restart services
docker compose up -d

# 6. Verify
curl http://localhost:5000/health/ready

# 7. Cleanup
rm /tmp/restore.dump
```

### Estimated Recovery Time: 15-30 minutes (depending on database size)
