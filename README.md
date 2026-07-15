# SimbaFlow — Labour Export Agency Management System

<p align="center">
  <img src="docs/screenshots/login.png" alt="SimbaFlow Login" width="600" />
</p>

SimbaFlow is a **multi-tenant SaaS platform** for labour export agencies that manages the end-to-end lifecycle of overseas worker deployment — from candidate registration through embassy processing, government labour clearances, travel logistics, and financial settlement.

## Features

### Multi-Tenant Architecture
- **Schema-per-tenant isolation** — Each agency gets its own PostgreSQL schema with completely isolated data
- **Platform Admin** — SuperAdmin manages all agencies from a single dashboard
- **Agency-specific roles & permissions** — Each agency defines their own roles and assigns permissions

### Candidate Management
- Full candidate registration with document management
- Passport and labour ID tracking
- Status timeline and workflow history
- CV auto-generation

### Configurable Workflow Engine (Event-Sourced)
- 8-stage default pipeline: Intake → Embassy → LMIS → Ticket → Departure → Arrival → Commission
- Configurable stages, statuses, and transition rules per agency
- Dynamic action buttons based on field conditions
- Parallel track support (e.g., Medical + Tasheer simultaneously)

### Agency ERP
- Staff/employee management
- Office/branch management
- Partner agency directory
- Custom role & permission management
- Audit trail for all operations

### Real-Time Updates
- SignalR WebSocket for live candidate status changes
- Toast notifications on stage transitions

### Additional Modules (In Progress)
- Embassy & visa processing
- LMIS government registration
- Travel & logistics with departure countdown
- Commission & finance (double-entry accounting)
- Telegram/WhatsApp bot integration
- Reporting & analytics with Excel/PDF export

## Screenshots

<!-- Add your screenshots here -->
<details>
<summary>📸 Click to view screenshots</summary>

### Dashboard
![Dashboard](docs/screenshots/dashboard.png)

### Candidates
![Candidates](docs/screenshots/candidates.png)

### Create Candidate
![Create Candidate](docs/screenshots/create-candidate.png)

### Agencies (Platform Admin)
![Agencies](docs/screenshots/agencies.png)

### Users & Staff
![Users](docs/screenshots/users.png)

### Roles & Permissions
![Roles](docs/screenshots/roles.png)

</details>

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend API | .NET 10, CQRS + MediatR, Carter (Minimal API), EF Core |
| Database | PostgreSQL 16+ (schema-per-tenant) |
| Frontend | Next.js 15, React 19, TypeScript, Tailwind CSS 4, shadcn/ui |
| Auth | JWT + Refresh Tokens, ASP.NET Core Identity, MFA support |
| Real-Time | SignalR (WebSocket) |
| State | SWR (server), Zustand (client) |
| Forms | React Hook Form + Zod validation |
| Tables | TanStack React Table |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 22+](https://nodejs.org/)
- [PostgreSQL 16+](https://www.postgresql.org/download/)

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/your-org/simbaflow.git
cd simbaflow
```

### 2. Set Up the Database

```bash
# Create the database
psql -U postgres -c "CREATE DATABASE simbaflow;"
```

### 3. Configure Backend

```bash
cd backend/src/SimbaFlow.API

# The connection string is in appsettings.Development.json
# Update if your PostgreSQL credentials are different:
# "Host=localhost;Port=5432;Database=simbaflow;Username=postgres;Password=YOUR_PASSWORD"
```

### 4. Run Database Migrations

```bash
cd backend
dotnet ef database update --project src/SimbaFlow.Infrastructure --startup-project src/SimbaFlow.API
```

Or simply start the API — it auto-migrates in development mode.

### 5. Install Frontend Dependencies

```bash
cd frontend
npm install
```

### 6. Configure Frontend

Create `frontend/.env.local`:

```env
NEXT_PUBLIC_API_URL=http://localhost:5117
API_BASE_URL=http://localhost:5117/api
API_URL=http://localhost:5117
BACKEND_URL=http://localhost:5117
NEXTAUTH_SECRET=your-secret-change-in-production
NEXTAUTH_URL=http://localhost:3000
```

## Running

### Start Backend

```bash
cd backend/src/SimbaFlow.API
dotnet run
```

The API starts on `http://localhost:5117`

### Start Frontend

```bash
cd frontend
npm run dev
```

The frontend starts on `http://localhost:3000`

### Seed Sample Data (Optional)

```bash
bash scripts/seed-sample-data.sh
```

This creates:
- 3 sample agencies with admin users
- 5 sample candidates
- 3 custom roles

## Default Credentials

| User | Username | Password | Role |
|------|----------|----------|------|
| Platform Admin | `admin` | `Admin@123!` | SuperAdmin |
| Ethio Star Agency | `tadesse@ethiostar.et` | `Agency@123!` | AgencyOwner |
| Addis Manpower | `hana@addismanpower.com` | `Agency@123!` | AgencyOwner |
| Golden Gate | `mohammed@goldengate.et` | `Agency@123!` | AgencyOwner |

## Project Structure

```
simbaflow/
├── backend/
│   ├── src/
│   │   ├── SimbaFlow.API/          # Minimal API endpoints (Carter modules)
│   │   ├── SimbaFlow.Application/   # CQRS behaviors, interfaces
│   │   ├── SimbaFlow.Domain/        # Entities, enums, domain events
│   │   ├── SimbaFlow.Infrastructure/ # EF Core, Identity, services
│   │   └── SimbaFlow.Shared/        # Shared DTOs
│   └── tests/
│       └── SimbaFlow.API.Tests/     # Unit + property-based tests
├── frontend/
│   ├── app/                         # Next.js App Router pages
│   ├── components/                  # React components
│   ├── lib/                         # Utilities, auth, API
│   └── types/                       # TypeScript definitions
├── scripts/                         # Seed data, backup scripts
├── docker-compose.yml               # Docker deployment
└── README.md
```

## Multi-Tenant Architecture

```
┌──────────────────────────────────────────────────┐
│  PUBLIC SCHEMA                                    │
│  • Users (ASP.NET Identity) — all users          │
│  • Tenants — agency metadata                     │
│  • Permissions — system permission codes         │
│  • Audit logs                                    │
└──────────────────────────────────────────────────┘

┌───────────────────┐  ┌───────────────────┐  ┌───────────────────┐
│ tenant_ethio_star │  │ tenant_addis_mp   │  │ tenant_golden_gate│
│ • candidates      │  │ • candidates      │  │ • candidates      │
│ • tenant_roles    │  │ • tenant_roles    │  │ • tenant_roles    │
│ • role_permissions│  │ • role_permissions│  │ • role_permissions│
│ • user_roles      │  │ • user_roles      │  │ • user_roles      │
└───────────────────┘  └───────────────────┘  └───────────────────┘
```

**How it works:**
1. Agency user logs in → JWT includes `tenant_id` claim
2. Every API request → `TenantConnectionInterceptor` sets PostgreSQL `search_path` to tenant's schema
3. All queries automatically scoped to that agency's data
4. SuperAdmin (no tenant_id) operates on public schema, manages all agencies

## API Documentation

When running in development, API docs are available at:
- **Scalar UI**: http://localhost:5117/scalar/v1
- **Health Check**: http://localhost:5117/health

## Docker Deployment

```bash
docker compose up -d
```

This starts:
- PostgreSQL 16 (port 5432)
- .NET API (port 5000)
- Next.js Frontend (port 3000)

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is proprietary software. All rights reserved.

---

Built with ❤️ for the Ethiopian labour export industry.
