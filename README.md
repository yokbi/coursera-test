# Alışkanlık Takibi — Habit Tracker

Production-grade habit tracking web app. Turkish UI, English codebase.

- **Frontend:** Next.js 16 (App Router) · TypeScript strict · Tailwind CSS 4 · zod
- **Backend:** .NET 8 Web API · Clean Architecture · EF Core + Npgsql · FluentValidation · Serilog
- **Database:** PostgreSQL 16 (Docker)
- **Tests:** xUnit (unit + Testcontainers integration) · Vitest + RTL · Playwright

```
backend/    .NET solution (Domain / Application / Infrastructure / Api + tests)
frontend/   Next.js app (src/app, src/components, src/lib, e2e/)
docs/       API.md · SECURITY.md · DECISIONS.md
```

## Quick start (5 commands)

Prerequisites: Docker, .NET 8 SDK, Node 20+.

```bash
cp .env.example .env                                          # 1. local env (edit the password)
docker compose up -d                                          # 2. PostgreSQL 16
dotnet run --project backend/src/HabitTracker.Api -- seed     # 3. migrate + demo data (dev only)
dotnet run --project backend/src/HabitTracker.Api --urls http://localhost:5000   # 4. API
cd frontend && npm install && npm run dev                     # 5. frontend → http://localhost:3000
```

Demo login (after seeding): `demo@habittracker.local` / `Demo1234!`

> Migrations apply automatically on startup in Development
> (`Database:MigrateOnStartup`). Manual alternative:
> `cd backend && dotnet ef database update -p src/HabitTracker.Infrastructure -s src/HabitTracker.Api`

## Configuration & secrets

No secrets live in the repo. Development values sit in
`backend/src/HabitTracker.Api/appsettings.Development.json` (clearly dev-only).
Production configuration comes from environment variables:

```bash
ConnectionStrings__Default="Host=…;Database=…;Username=…;Password=…"
Jwt__SigningKey="$(openssl rand -base64 48)"     # ≥32 chars, required
Cors__AllowedOrigins__0="https://your-frontend.example"
```

or `dotnet user-secrets` (project: `backend/src/HabitTracker.Api`):

```bash
dotnet user-secrets set "Jwt:SigningKey" "<random-48-bytes>"
```

Frontend: `NEXT_PUBLIC_API_URL` (default `http://localhost:5000`).

## Tests

| Suite | Command | Notes |
| --- | --- | --- |
| Backend unit | `cd backend && dotnet test tests/HabitTracker.UnitTests` | streak engine, timezone/DST edges |
| Backend integration | `cd backend && dotnet test tests/HabitTracker.IntegrationTests` | needs Docker (Testcontainers PostgreSQL) |
| Frontend unit | `cd frontend && npm test` | Vitest + Testing Library |
| E2E | `docker compose up -d && cd frontend && npm run test:e2e` | Playwright; boots API + web automatically |

Backend coverage (Domain + Application):

```bash
cd backend && dotnet test --collect:"XPlat Code Coverage"
```

## Docs

- [docs/API.md](docs/API.md) — every endpoint with request/response examples
- [docs/SECURITY.md](docs/SECURITY.md) — threat model & OWASP Top 10 mapping
- [docs/DECISIONS.md](docs/DECISIONS.md) — every non-obvious implementation decision
