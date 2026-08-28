# Alışkanlık Takibi — Habit Tracker

Production-grade habit tracking web app. Turkish UI, English codebase.

## Features

**Habits & progress** — three habit types (done/not-done, quantity, duration) on three
schedules (daily, specific weekdays, X times per week). Timezone-aware streaks, a
weekly grid, per-habit stats with a 90-day heatmap, archive and reorder.

**Accounts** — email + password or **Google sign-in**, JWT with rotating refresh
tokens, password change, and account deletion with anonymization.

**Reminders** — opt-in daily email at an hour you choose in your own timezone,
listing only what is still pending. One-click unsubscribe, no login required.

**Friends** — friend requests and a friend list, with **opt-in** streak sharing.
Sharing is a summary, never access: friends can see a best streak, not your habits.

**Groups** — shared goals with friends. Each member links their *own* habit; joining
is a separate, narrower consent than friend sharing.

**Admin** — role-based panel for user search, suspension and system metrics. Admins
manage accounts, not content: they get no window into anyone's habits.

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

Optional features are off until configured — Google sign-in hides its button without
credentials, and the reminder scheduler does not run. See `.env.example` and
[docs/API.md](docs/API.md) for the settings.

Grant yourself the admin panel:

```bash
dotnet run --project backend/src/HabitTracker.Api -- promote-admin kisi@example.com
```

Role lives in the access token, so sign in again afterwards. The seeded demo account
is already an admin.

Backend coverage (Domain + Application):

```bash
cd backend && dotnet test --collect:"XPlat Code Coverage"
```

## Docs

- [docs/API.md](docs/API.md) — every endpoint with request/response examples
- [docs/SECURITY.md](docs/SECURITY.md) — threat model & OWASP Top 10 mapping
- [docs/DECISIONS.md](docs/DECISIONS.md) — every non-obvious implementation decision
