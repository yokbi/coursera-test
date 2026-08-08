# HabitTracker API

Base URL: `http://localhost:5000`. All endpoints are versioned under `/api/v1`.

- **Auth:** `Authorization: Bearer <accessToken>` unless marked public.
- **Refresh cookie:** `ht_refresh` — httpOnly, Secure, SameSite=Strict, Path=`/api/v1/auth`.
- **CSRF:** `POST /auth/refresh` and `POST /auth/logout` additionally require the header `X-CSRF: 1`.
- **Errors:** RFC 7807 `application/problem+json`. Validation failures return `400` with an `errors` map; domain-rule violations return `422`; auth failures `401`; missing/foreign resources `404`; duplicates `409`; rate limiting `429`.
- **Dates:** check-in dates are calendar days (`yyyy-MM-dd`) **in the user's timezone**; all timestamps are UTC ISO-8601.

## Auth

### POST /api/v1/auth/register (public, rate-limited 5/min/IP)

```json
{ "email": "kisi@example.com", "password": "Str0ngPass!x", "timeZone": "Europe/Istanbul" }
```

`timeZone` optional (IANA id, default `Europe/Istanbul`). Password policy: 8–128 chars, ≥1 lower, ≥1 upper, ≥1 digit.

**200** (sets `ht_refresh` cookie):

```json
{
  "accessToken": "eyJ…",
  "expiresInSeconds": 900,
  "user": { "id": "guid", "email": "kisi@example.com", "timeZone": "Europe/Istanbul", "createdAt": "2026-07-09T08:00:00Z" }
}
```

**400** validation, **409** email in use, **429** rate limited.

### POST /api/v1/auth/login (public, rate-limited 5/min/IP)

```json
{ "email": "kisi@example.com", "password": "Str0ngPass!x" }
```

**200** same shape as register. **401** generic `Invalid email or password.` for unknown email, wrong password, *and* locked accounts (5 failures → 15 min lockout) — no user enumeration.

### POST /api/v1/auth/refresh (public; requires cookie + `X-CSRF: 1`; 60/min/IP)

Rotates the refresh token (old one is revoked; reuse of a revoked token revokes the whole token family). **200** same shape as register. **401** missing/invalid/expired/reused token or missing CSRF header.

### POST /api/v1/auth/logout (public; requires cookie + `X-CSRF: 1`)

Revokes the presented refresh token and clears the cookie. **204** always (idempotent).

### POST /api/v1/auth/change-password (auth, rate-limited)

```json
{ "currentPassword": "Str0ngPass!x", "newPassword": "N3wStr0ngPass!" }
```

Revokes **all** refresh tokens, then returns a fresh token pair (200, register shape). **401** wrong current password.

### DELETE /api/v1/auth/account (auth, rate-limited)

```json
{ "password": "Str0ngPass!x" }
```

Soft-deletes and anonymizes the account, revokes all tokens, clears the cookie. **204**. **401** wrong password.

## Google sign-in

Server-side authorization-code flow with PKCE. The browser is redirected; the client secret stays on the server. A successful callback issues the **same** `ht_refresh` cookie the password flow issues, so nothing downstream changes.

### GET /api/v1/auth/google/available (public)

**200** `{ "available": true }` — whether this server has Google credentials configured. The UI hides the Google button when false.

### GET /api/v1/auth/google/start?returnPath=/aliskanliklar (public, rate-limited)

Sets a 10-minute `ht_oauth` cookie (httpOnly, Secure, **SameSite=Lax**, Path=`/api/v1/auth/google`) carrying the OAuth `state`, the PKCE verifier and the return path, then **302**s to Google.

`returnPath` must be a relative same-origin path; anything else falls back to `/`.

### GET /api/v1/auth/google/callback?code=&state=&error= (public, rate-limited)

Verifies `state` against the flow cookie in constant time, exchanges the code with the PKCE verifier, then links or provisions the account:

1. match on Google `sub`, else
2. match on **verified** email — links Google to that existing account, else
3. create a new passwordless account.

**302** to `{Frontend:BaseUrl}/giris/google?next=<returnPath>` with `ht_refresh` set. That public page exchanges the cookie for a session and forwards.

- **302** to `/giris?hata=google` when the user declines consent.
- **401** for a missing/tampered `state`, a missing flow cookie, or an unverified provider email.

### Passwordless accounts

Accounts created through Google have no password until they set one:

- `POST /auth/login` → **401** (generic message; no enumeration signal).
- `POST /auth/change-password` → `currentPassword` may be empty; sets the first password. Accounts that already have one still must prove it.
- `DELETE /auth/account` → `password` may be empty; deletion also releases the Google link so the identity can register again.

`GET /api/v1/me` reports `hasPassword` and `linkedGoogle` so the UI can adapt.

### Configuration

```bash
Authentication__Google__ClientId="…apps.googleusercontent.com"
Authentication__Google__ClientSecret="…"
Authentication__Google__RedirectUri="https://api.example.com/api/v1/auth/google/callback"
Frontend__BaseUrl="https://app.example.com"
```

The redirect URI must match an authorized redirect URI in the Google Cloud console exactly.

## Profile

### GET /api/v1/me (auth)

**200** → `user` object (see register).

### PUT /api/v1/me (auth)

```json
{ "timeZone": "America/New_York" }
```

**200** updated user. **400** unknown IANA id.

## Habits

Habit object:

```json
{
  "id": "guid",
  "name": "Su iç",
  "description": null,
  "color": "#0ea5e9",
  "icon": "💧",
  "category": "Sağlık",
  "type": "quantity",            // boolean | quantity | duration
  "targetValue": 8,               // null for boolean; minutes for duration
  "unit": "bardak",              // quantity only
  "scheduleType": "daily",       // daily | specificWeekdays | timesPerWeek
  "scheduleDays": ["monday", "wednesday"],
  "timesPerWeek": null,
  "sortOrder": 0,
  "isArchived": false,
  "startDate": "2026-06-09",
  "createdAt": "2026-06-09T08:00:00Z"
}
```

### GET /api/v1/habits?includeArchived=false&page=1&pageSize=50 (auth)

Offset pagination, `pageSize` ≤ 100. **200**:

```json
{ "items": [ …habit ], "page": 1, "pageSize": 50, "totalCount": 3 }
```

### POST /api/v1/habits (auth)

Body: habit fields (`name`, `color`, `icon`, `type`, `scheduleType` required; conditional: `targetValue` for quantity/duration, `unit` for quantity, `scheduleDays` for specificWeekdays, `timesPerWeek` for timesPerWeek). **201** with `Location` header. **400** validation.

### GET /api/v1/habits/{id} (auth) → **200** habit; **404** not found or not owned.

### PUT /api/v1/habits/{id} (auth)

Same fields as create **except `type` (immutable)**. **200** updated habit.

### POST /api/v1/habits/{id}/archive · POST /api/v1/habits/{id}/unarchive (auth)

Soft archive toggle. **200** updated habit. Archived habits reject check-ins (**422**).

### PUT /api/v1/habits/reorder (auth)

```json
{ "habitIds": ["guid-1", "guid-2"] }
```

Index in the array becomes `sortOrder`. **204**. **404** if any id is not the caller's.

## Check-ins

### PUT /api/v1/habits/{id}/checkins (auth)

```json
{ "date": "2026-07-09", "value": 1, "increment": false }
```

- `increment: true` adds to the day's value, `false` replaces it.
- Boolean habits clamp to 0/1; value 0 removes the day's check-in.
- Window: `date` must be within **today − 7 … today** in the user's timezone; otherwise **422**.

**200**:

```json
{ "checkIn": { "date": "2026-07-09", "value": 1, "completed": true }, "currentStreak": 4, "streakUnit": "days" }
```

### GET /api/v1/habits/{id}/checkins?from=2026-07-01&to=2026-07-09 (auth)

**200** → array of `{ date, value, completed }`, ascending by date.

## Dashboard & stats

### GET /api/v1/habits/today (auth)

Habits scheduled for the caller's current local day:

```json
[
  {
    "habit": { …habit },
    "todayValue": 3,
    "completedToday": false,
    "currentStreak": 4,
    "streakUnit": "days",       // "weeks" for timesPerWeek habits
    "weekCompletions": 1         // current ISO-week completions (timesPerWeek only)
  }
]
```

### GET /api/v1/habits/week?weekStart=2026-07-06 (auth)

`weekStart` optional (defaults to the current ISO week, Monday-first):

```json
{
  "weekStart": "2026-07-06",
  "habits": [
    { "habit": { …habit }, "days": [ { "date": "2026-07-06", "value": 8, "completed": true, "scheduled": true }, … ] }
  ]
}
```

### GET /api/v1/habits/{id}/stats (auth)

```json
{
  "habitId": "guid",
  "currentStreak": 4,
  "longestStreak": 9,
  "streakUnit": "days",
  "completionRate30": 0.83,
  "completionRate90": 0.71,
  "totalCheckIns": 64,
  "heatmap90": [ { "date": "2026-04-11", "value": 8, "completed": true, "scheduled": true }, … ]
}
```

Streak rules: schedule-aware (unscheduled days never break a streak); the in-progress day/week doesn't break a streak until it is missed; `timesPerWeek` streaks are counted in weeks.

## Health

### GET /health (public)

**200** `Healthy` (includes a PostgreSQL connectivity probe) / **503** `Unhealthy`.
