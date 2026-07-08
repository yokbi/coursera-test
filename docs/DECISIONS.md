# Architecture & Implementation Decisions

One line per decision: context → choice → rationale.

## Phase 0 — Scaffold

- .NET SDK install → installed `dotnet-sdk-8.0` from Ubuntu 24.04 apt (8.0.128) → official `builds.dotnet.microsoft.com` blocked by egress policy; apt package is a stable equivalent.
- Frontend framework version → Next.js 16.2.10 (latest stable from `create-next-app@latest`) with React 19 and Tailwind CSS v4 → spec says "latest stable, App Router".
- Test database → Testcontainers (PostgreSQL) for integration tests instead of a second compose service → isolated, disposable, CI-friendly; compose only carries the app DB.
- Application layer mediator → plain application service classes with interfaces (no MediatR) → MediatR v13+ is commercially licensed; simple services satisfy Clean Architecture with less indirection (KISS).
- Repo cleanup → removed legacy `examples/`, `site/`, `test.txt`, `test2.txt` → user explicitly allowed deleting irrelevant files.

## Phase 1 — Domain & DB

- Streak unit for "X times per week" habits → streak counted in **weeks** (consecutive weeks meeting the target); an in-progress week that hasn't met the target yet does not break the streak → daily streaks are meaningless for weekly-quota schedules.
- Check-in completion rule → boolean: value ≥ 1; quantity/duration: value ≥ target → a partially-filled quantity day counts toward completion rate but not streak.
- Check-in date storage → `DateOnly` representing the calendar day **in the user's timezone** (computed at write time), plus UTC `created_at`/`updated_at` → makes day-boundary queries indexable and unambiguous.
- Weekday schedule storage → bitmask int (Mon=1 … Sun=64) → compact, index-friendly, no join table needed.
- User ID on CheckIn → denormalized `user_id` column alongside `habit_id` → enables the hot `user_id + date` index required by the spec without a join.

## Phase 2 — Auth

- Password hashing → `Microsoft.Extensions.Identity.Core` `PasswordHasher<T>` (PBKDF2-HMAC-SHA256, 100k iterations, per ASP.NET Identity v3 defaults) → spec allows Identity defaults; avoids pulling full Identity framework tables.
- Refresh token storage → SHA-256 hash of the token stored server-side; raw token only in httpOnly cookie → DB leak does not expose usable tokens.
- Refresh rotation → every refresh issues a new token and revokes the old one; reuse of a revoked token revokes the whole family (token-theft detection) → OWASP recommendation.
- CSRF protection → SameSite=Strict cookie + required custom `X-CSRF: 1` header on refresh/logout → cross-site requests cannot set custom headers; no server-side token state needed.
- Rate limiting → built-in .NET 8 `RateLimiter` middleware, fixed window 5 requests/min/IP on auth endpoints → no extra dependency (AspNetCoreRateLimit unnecessary).
- Account lockout → 5 consecutive failures → 15 min lockout; response stays the generic "invalid credentials" message → prevents user enumeration and brute force.
- Password policy → min 8 chars, at least one lower, one upper, one digit, max 128 → strong but usable; enforced by FluentValidation and mirrored in zod.

## Phase 3 — API

- Pagination → offset-based (`page`, `pageSize`, max 100) → habit lists are small; cursor pagination is YAGNI here.
- Check-in edit window → server compares the target date to "today" in the user's timezone; allowed range: today-7 … today → per spec.
- Reorder API → `PUT /api/v1/habits/reorder` with the full ordered id list → simplest correct approach for small lists.

- Effective streak start → `min(habit.StartDate, earliest completed check-in)` → backfilled check-ins (allowed 7 days back) may predate the habit's creation day and must still count toward streaks; also protects users whose local "today" is a day ahead of UTC.
- Ownership failures → 404 (not 403) for habits belonging to another user → avoids confirming that a resource id exists (resource enumeration).
- Boolean habit check-in values → clamped to 0/1; a zeroed check-in deletes the row so completion-rate denominators stay clean.
- Container images → Docker Hub blob CDN blocked by egress policy; images pulled via `mirror.gcr.io` and tagged locally (`postgres:16-alpine`, `testcontainers/ryuk:0.9.0`) → CI environments with normal Docker Hub access need no change.

## Phase 4 — Frontend

- Access token → kept in JS memory only; refresh token in httpOnly cookie; on app load `/auth/refresh` bootstraps the session → avoids XSS-readable token storage.
- Route guard → Next.js middleware checks presence of the refresh cookie (same host `localhost`, so the API cookie is visible to the frontend server) → redirect to `/giris` without a round-trip.
- Data fetching → small typed `fetch` wrapper with automatic 401→refresh→retry; no TanStack Query → app is small, YAGNI.
- Theme → class-based dark mode with `localStorage` persistence + `prefers-color-scheme` default → no dependency.

## Future ideas (explicitly out of scope, not built)

- Push/email notifications, social features, OAuth login, admin panel, analytics, mobile app.
