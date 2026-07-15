# Technology Stack

## Programming Languages

| Language | Version | Usage |
|----------|---------|-------|
| C# | 13 (net10.0) | Backend API, domain, infrastructure |
| TypeScript | 5.x | Frontend application |
| SQL | PostgreSQL 16+ | Database queries, migrations |

## Backend Frameworks & Libraries

| Framework/Library | Version | Purpose |
|-------------------|---------|---------|
| ASP.NET Core | 10.0 | Web framework |
| Carter | 9.0.0 | Minimal API module registration |
| MediatR | 13.0.0 | CQRS mediator pattern |
| FluentValidation | 12.0.0 | Request validation |
| Entity Framework Core | 10.0.9 | ORM / data access |
| Npgsql.EF | 10.0.2 | PostgreSQL provider for EF Core |
| ASP.NET Core Identity | 10.0.9 | User/role management |
| Microsoft.Authentication.JwtBearer | 10.0.9 | JWT authentication |
| Microsoft.IdentityModel.Tokens | 8.11.0 | Token validation |
| System.IdentityModel.Tokens.Jwt | 8.11.0 | JWT creation |
| Mapster | 7.4.0 | Object-object mapping |
| Scalar.AspNetCore | 2.16.10 | OpenAPI documentation UI |

## Frontend Frameworks & Libraries

| Framework/Library | Version | Purpose |
|-------------------|---------|---------|
| Next.js | 15.2.4 | React framework (App Router) |
| React | 19 | UI library |
| Tailwind CSS | 4.1.9 | Utility-first CSS |
| Radix UI | Various | Accessible component primitives |
| React Hook Form | 7.60.0 | Form management |
| Zod | 3.25.67 | Schema validation |
| SWR | 2.3.6 | Data fetching with caching |
| Zustand | latest | State management |
| Tanstack React Table | 8.21.3 | Data table components |
| FullCalendar | 6.1.19 | Calendar/scheduling views |
| Recharts | 2.15.4 | Charts and dashboards |
| Framer Motion | 12.23.19 | Animations |
| Lucide React | 0.454.0 | Icon library |
| date-fns | 4.1.0 | Date utilities |
| next-auth | 4.24.11 | Authentication (JWT strategy) |
| next-themes | 0.4.6 | Theme switching |
| Sonner | latest | Toast notifications |
| cmdk | 1.0.4 | Command palette |
| jwt-decode | 4.0.0 | Client-side JWT parsing |
| Immer | latest | Immutable state updates |
| Vaul | 1.1.2 | Drawer component |
| react-phone-input-2 | 2.15.1 | Phone number input |
| react-resizable-panels | 2.1.7 | Resizable panel layouts |
| Embla Carousel | 8.5.1 | Carousel component |

## Infrastructure

| Service | Purpose |
|---------|---------|
| PostgreSQL | Primary relational database |
| Azure (planned) | Cloud deployment target |

## Build & Dev Tools

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 10.0.203 | Backend build toolchain |
| Node.js | 22+ (implied) | Frontend build |
| Turbopack | (Next.js built-in) | Fast dev server |
| cross-env | 7.0.3 | Cross-platform env vars |
| PostCSS | 8.5 | CSS processing |
| tw-animate-css | 1.3.3 | Tailwind animation utilities |

## Testing Tools

| Tool | Version | Purpose |
|------|---------|---------|
| xUnit | (implied) | Backend unit testing |
| (Frontend testing TBD) | — | No frontend test framework configured |

## Security Headers (Frontend)

| Header | Value |
|--------|-------|
| X-Frame-Options | DENY |
| Content-Security-Policy | Strict self-only policy |
| X-Content-Type-Options | nosniff |
| Referrer-Policy | strict-origin-when-cross-origin |
| HSTS | max-age=31536000; includeSubDomains; preload |
| Permissions-Policy | Restrictive (no camera, mic, payment, etc.) |
