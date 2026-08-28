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
- Route guard cookie → separate non-httpOnly `ht_session` marker (value `1`) set by the client, because the real refresh cookie is path-scoped to `/api/v1/auth` and invisible to page requests → marker is a pure routing hint, carries nothing sensitive.
- Next.js 16 renamed `middleware.ts` → `proxy.ts` (exported `proxy` function) → followed the new convention.
- Habit type is immutable after creation (edit form disables it) → changing type would make historical check-in values meaningless.
- Reorder UI → up/down buttons instead of drag-and-drop → keyboard accessible and dependency-free (YAGNI).
- Check-in steps → +1 for quantity, +5 minutes for duration habits → matches common logging granularity.

## Phase 5 — E2E & hardening

- Playwright boots the API (`dotnet run`) and Next dev server via `webServer`; PostgreSQL from docker-compose must already be up (documented) → keeps the E2E entry point to one command.
- E2E runs serially in one browser context (register → … → logout is one continuous journey per spec).

## Phase 6 — Final verification

- Backend coverage (line, merged unit+integration): Domain **96.5%**, Application **97.2%**, combined **97.0%** — target was ≥80%.
- `npm audit` → 2 moderate findings, both in `postcss` **bundled inside Next.js itself**; the only offered fix downgrades Next to 9.x. Accepted as upstream risk (build-time tooling, not runtime-exposed); revisit on the next Next.js release.
- Playwright browsers → environment pre-installs Chromium at `/opt/pw-browsers/chromium`; config honors `PLAYWRIGHT_CHROMIUM_PATH` to avoid re-downloading. Without the variable, Playwright uses its default download.
- Vitest 4 quirk → a `beforeEach` mock reset combined with a rejecting mock consumed inside a React component is misreported as an unhandled error; HabitCard tests install per-test implementations instead of resetting (documented in the test file).

## Phase 7 — CI

- CI runner → GitHub Actions, three parallel jobs (backend / frontend / e2e) → matches the three documented test commands; `main` is protected so every change now lands with a green signal.
- Integration tests in CI → Testcontainers against the runner's own Docker daemon (no service container); the E2E job instead uses a PostgreSQL **service container** because the API process needs a fixed `localhost:5432` matching `appsettings.Development.json`.
- Coverage enforcement → a script merges both cobertura reports (best hit count per line) and fails under 80% on Domain + Application → unit and integration runs complement rather than overwrite each other.

## Phase 8 — Google OAuth

- Flow → server-side authorization code + **PKCE**, secret never reaches the browser → standard for confidential clients; PKCE also blocks code interception.
- OAuth CSRF → `state` stored in a short-lived `ht_oauth` cookie with **SameSite=Lax**, compared in constant time on callback → Strict would not survive Google's cross-site redirect back to the callback, so Lax is required here (unlike the refresh cookie, which stays Strict).
- Provider abstraction → `IExternalAuthClient` with a real Google implementation and a fake in tests → the whole redirect/callback/link flow is covered without real credentials.
- ID token → claims read directly from Google's token-endpoint response over TLS, without a second signature verification → per Google's guidance for the server-side flow, where the channel itself authenticates the issuer.
- Account matching → provider `sub` first, then **verified** email → links an existing password account instead of duplicating it; an unverified provider email is rejected outright since it could belong to someone else.
- Passwordless accounts → `PasswordHash` is now nullable; password login fails generically for them, `change-password` sets the first password without demanding an old one (the caller already holds a valid access token), and account deletion skips the password check.
- Deletion → clears `GoogleSubject` so the same Google identity can register again → without this the unique index would permanently block re-registration.
- Post-callback landing → redirects to the public `/giris/google` page rather than the target route → the route guard keys off a marker cookie only the frontend can set; that page exchanges the refresh cookie first, then forwards. Also avoids depending on cookie-domain sharing between the app and API hosts in production.
- Open redirect → `returnPath` accepted only as a relative same-origin path, enforced on both the backend and the landing page.
- Google button visibility → `GET /auth/google/available` reports whether credentials are configured; the button renders only when true → no dead button on servers without Google set up.

## Phase 9 — E-posta hatırlatmaları

- Opt-in, never opt-out → `RemindersEnabled` defaults to false; a fresh account is never mailed.
- Scheduling rules live in a pure `ReminderPlanner` (Domain) → every timezone/window edge case is unit tested without a database or a mail server.
- Idempotency → `LastReminderSentOn` stores the **local** day and is written only after a successful send → repeated scheduler ticks cannot double-send, and a transport failure retries on the next tick.
- Late delivery → a reminder still goes out later the same local day after a scheduler outage; the local day boundary stops it leaking into the night.
- Nothing pending → no mail. A user who finished everything is not nagged.
- Weekly-quota habits → judged by the week's progress, not by "today", so a 2x/week habit does not nag daily.
- Scheduler → `BackgroundService` with a `PeriodicTimer`, default 5-minute poll, **disabled by default** (`Reminders:Enabled`) so tests and one-off runs never send mail; users pick an hour, so minute-level precision is unnecessary.
- Transport → `IEmailSender` with `SmtpEmailSender` (System.Net.Mail, no extra dependency) when `Email:SmtpHost` is set, otherwise a `LoggingEmailSender` that records only the subject and recipient **domain** → development works with no mail server and no PII in logs.
- Unsubscribe → HMAC-signed, purpose-tagged token in the mail; the link needs no session and can only ever disable reminders → standard for transactional mail, and harmless if a mail archive leaks.
- Mail HTML → escapes only `& < > " '` rather than `WebUtility.HtmlEncode` → keeps Turkish letters and emoji readable under the declared UTF-8 charset while still blocking markup injection from habit names.
- Turkish suffixes → streak phrases are built as whole words (`günlük` / `haftalık`) rather than concatenating a suffix → vowel harmony makes a single shared suffix wrong; a unit test pinned this after the first attempt produced "günlık".
- Reminder integration tests assert per-recipient, not on the sweep's batch count → the sweep is global and the test database is shared across the collection, so batch counts are not isolated.

## Phase 10 — Admin paneli

- Authorization model → a `Role` enum on User (`User` / `Admin`) carried as a `role` claim in the JWT → `[Authorize(Roles = "Admin")]` gates every admin route with no extra database round-trip per request.
- `JwtBearerOptions.MapInboundClaims = false` → the default mapping rewrites the short `role` claim to a long WS-Federation URI, which then no longer matches `RoleClaimType`; every admin request 403'd until this was turned off (caught by the integration tests).
- Role changes take effect on the **next token**, not immediately → an access token lives 15 minutes; promoting a user therefore requires them to sign in again, which the tests pin explicitly.
- Admins get **no** window into other users' habits → the ordinary owner-scoped endpoints are unchanged, so an admin still gets 404 on someone else's habit. The panel manages accounts, not content.
- Suspension over deletion → `SuspendedAt` blocks sign-in without destroying data; suspending also revokes live refresh tokens so sessions die immediately rather than at expiry.
- Suspension is reported as **403 only after the password verifies** → a wrong password still returns the same generic 401, so suspension never becomes an enumeration oracle.
- Admins cannot suspend themselves or each other → prevents locking the panel out; unsuspending is always available.
- First admin → `dotnet run -- promote-admin <email>` rather than a bootstrap config value or a magic email → explicit, auditable, works in every environment. The dev seeder's demo account is also an admin so the panel is reachable locally.
- Admin user DTO excludes password hashes, provider subjects and refresh tokens → an admin manages accounts, not secrets; a test asserts the payload never contains them.

## Phase 11 — Sosyal özellikler (arkadaşlık + seri paylaşımı)

- Split from "shared habit groups" → friendships and consent-based sharing ship first as a coherent unit; groups build on top and land separately rather than in one oversized change.
- One row per pair, keyed by who asked → the reverse direction is found by matching either column, never by a second row; a unique index on `(requester, addressee)` plus an explicit reverse lookup keeps it single.
- Sending a request to an unknown address returns **204, exactly like a real one** → reporting "no such user" would make this an account-enumeration oracle, which the rest of the API deliberately avoids. The UI copy is vague to match.
- Mutual requests auto-link → if B asks A while A→B is pending, that is consent from both sides; no accept step needed.
- A declined request reopens the same row rather than blocking forever → refusal is not permanent, and no duplicate rows accumulate.
- Only the **addressee** can accept or decline → otherwise a requester could unilaterally friend anyone. Pinned by a test.
- **Sharing is opt-in and defaults to off.** A friend without consent exposes nothing but an email address; every stat field is null rather than zero, so "no data" is never confused with "no progress".
- Sharing grants a **summary, not access** → friends see best active streak, active habit count and today's progress; the owner-scoped habit endpoints stay closed and still return 404 for a friend. Pinned by a test.
- Revoking consent takes effect immediately, and removing a friendship deletes the row outright so neither side keeps visibility for even a moment.
- Account deletion removes its friendship rows → sharing must not outlive the account, and the pairs are freed for reuse.
- Friendship FKs use `Restrict`, not `Cascade` → two cascade paths into the same table are not allowed by SQL Server/Postgres semantics EF enforces; cleanup is explicit instead.

## Phase 12 — Ortak alışkanlık grupları

- Each member links **their own** habit to the group → nobody's habit is handed over; the group only projects that one habit's progress. A member may only link a habit they own, checked on create, join and change (without it a caller could point a membership at someone else's habit and read its progress back).
- Membership and invitation are one row (`Status = Invited | Joined`) → an invitation *is* a member who has not consented yet; no second table, and the unique `(group, user)` index prevents duplicates.
- **Joining is the consent**, and it is independent of the friend-list sharing flag → two separate, narrower consent surfaces: a member can share group progress without opening their whole streak summary to all friends. Pinned by a test.
- An outstanding invitation grants **no** view of the members (403) → being invited is not consent.
- Only friends can be invited → a group must not become a way to push yourself into a stranger's app.
- Only the owner invites, removes members, or deletes; the owner cannot leave (that would strand the group) and must delete it instead.
- Pending invitees are visible only to the owner → members do not need to see who declined to answer.
- Group progress is a projection, not access → the owner-scoped habit endpoints still return 404 for fellow members, including writes.
- Account deletion removes memberships and owned groups → consent must not outlive the account.
- Frontend: the route component unwraps `params` with `use()` and delegates to an exported `GroupDetailView({ groupId })` → `use(promise)` never resolves under jsdom + Testing Library (verified with a minimal probe), so the view is tested directly with a plain id instead of fighting the environment.

## Future ideas (explicitly out of scope, not built)

- Push/email notifications, social features, OAuth login, admin panel, analytics, mobile app.
