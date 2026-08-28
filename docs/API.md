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

## Reminder emails

Opt-in daily reminder mail, sent at an hour the user picks in their own timezone, listing only what is still pending that day. Nothing is sent when everything is done.

### PUT /api/v1/reminders/settings (auth)

```json
{ "enabled": true, "hour": 20 }
```

`hour` is 0–23 in the user's timezone. **200** returns the updated user (`remindersEnabled`, `reminderHour`). **400** for an hour outside the range.

Turning reminders off also clears the "already sent today" marker, so re-enabling mid-day works immediately.

### GET /api/v1/reminders/unsubscribe?token=… (public)

The link embedded in every reminder mail. The token is an HMAC-signed, purpose-tagged value that can **only** disable reminders — it grants no other access and needs no session.

**200** `text/html` confirmation, **400** for a missing, malformed or tampered token.

### Delivery rules

- One mail per local day, at or after the chosen hour; a scheduler outage still delivers later the same local day.
- The send marker is written only after a successful send, so transport failures retry.
- Weekly-quota habits are judged by the week's progress, not by a single day.

### Configuration

```bash
Reminders__Enabled=true              # scheduler is off by default
Reminders__PollIntervalMinutes=5
Email__ApiBaseUrl=https://api.example.com   # unsubscribe links point here
Email__FromAddress=hatirlatma@example.com
Email__SmtpHost=smtp.example.com     # unset -> mail is logged, not sent
Email__SmtpPort=587
Email__SmtpUser=…
Email__SmtpPassword=…
```

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

## Friends

Consent-first social layer. Sharing is **opt-in and off by default**: a friend who has not enabled it exposes nothing but their email address. Sharing grants a *summary*, never access — the habit endpoints stay owner-scoped and return 404 for a friend.

### POST /api/v1/friends/requests (auth)

```json
{ "email": "arkadas@example.com" }
```

**204 — always**, whether or not the address belongs to an account. This is deliberate: reporting "no such user" would make the endpoint an account-enumeration oracle. Sending to yourself, to an existing friend, or twice is a silent no-op.

If the other party already has a pending request to you, this **auto-accepts** it: asking back is consent.

### GET /api/v1/friends/requests (auth)

```json
{
  "incoming": [{ "requestId": "guid", "userId": "guid", "email": "…", "createdAt": "…" }],
  "outgoing": [{ "requestId": "guid", "userId": "guid", "email": "…", "createdAt": "…" }]
}
```

### POST /api/v1/friends/requests/{id}/accept · /decline (auth)

**204**. Only the **addressee** may respond — the requester gets **404**, as does anyone else. A declined request can be reopened by sending a new one.

### GET /api/v1/friends (auth)

```json
[
  {
    "userId": "guid",
    "email": "arkadas@example.com",
    "sharingEnabled": true,
    "activeHabits": 4,
    "bestStreak": 12,
    "bestStreakUnit": "days",
    "bestStreakHabitName": "Meditasyon",
    "completedToday": 2,
    "scheduledToday": 3,
    "friendsSince": "2026-06-10T00:00:00Z"
  }
]
```

When `sharingEnabled` is false every stat is **null** — absence of consent is absence of data, not a zero. Today's figures are computed in that friend's own timezone.

### DELETE /api/v1/friends/{userId} (auth)

**204**, removing the row so neither side keeps visibility. **404** if you are not friends.

### PUT /api/v1/friends/sharing (auth)

```json
{ "shareStreaks": true }
```

**200** returns the updated user. Turning it off hides the data on the next read.

## Habit groups

A group is a shared goal. Each member links **their own** habit to it; the group projects only that habit's progress. Joining is the consent, and it is independent of the friend-list sharing flag — a narrower grant, scoped to one habit and one group.

A member may only ever link a habit they own; anything else returns **404**.

### GET /api/v1/groups (auth)

Groups you are in, including invitations you have not answered (`joined: false`).

```json
[{ "id": "guid", "name": "Sabah koşusu", "description": null, "color": "#22c55e",
   "icon": "🏃", "isOwner": true, "joined": true, "memberCount": 3,
   "createdAt": "2026-07-01T00:00:00Z" }]
```

### POST /api/v1/groups (auth)

```json
{ "name": "Sabah koşusu", "description": "Birlikte", "color": "#22c55e", "icon": "🏃", "habitId": "guid" }
```

**201**. The creator joins immediately — creating a group is itself consent. **404** if the habit is not yours, **400** on validation.

### GET /api/v1/groups/{id} (auth)

```json
{
  "id": "guid", "name": "Sabah koşusu", "isOwner": true,
  "members": [{
    "userId": "guid", "email": "arkadas@example.com", "isOwner": false, "isYou": false,
    "habitName": "Sabah koşusu", "habitIcon": "🏃",
    "currentStreak": 5, "streakUnit": "days",
    "completedToday": true, "scheduledToday": true, "completionsLast7Days": 6,
    "joinedAt": "2026-07-02T00:00:00Z"
  }],
  "pendingInvitees": [{ "userId": "guid", "email": "davetli@example.com" }]
}
```

- **403** for an invitee who has not joined — an invitation is not consent.
- **404** for anyone who is not in the group at all.
- `pendingInvitees` is empty for everyone but the owner.
- Today's figures use each member's own timezone.

### POST /api/v1/groups/{id}/invitations (auth, owner only)

```json
{ "userId": "guid" }
```

**204**. **422** if the target is not an accepted friend, **404** if you are not the owner. Re-inviting an existing member is a no-op.

### POST /api/v1/groups/{id}/join · /decline (auth, invitee only)

Join takes `{ "habitId": "guid" }` — your own habit. **204**. **404** for a missing invitation or a habit you do not own. Declining removes the invitation entirely.

### PUT /api/v1/groups/{id}/habit (auth, joined member)

`{ "habitId": "guid" }` swaps which of *your* habits this group tracks. **204**, or **404** for a habit you do not own.

### POST /api/v1/groups/{id}/leave (auth)

**204**. **422** for the owner — delete the group instead of stranding it.

### DELETE /api/v1/groups/{id}/members/{userId} · DELETE /api/v1/groups/{id} (auth, owner only)

Remove a member, or delete the group and every membership. **204**, **404** for non-owners.

## Admin

Every route below requires a JWT carrying `role: Admin`. Ordinary users get **403**, anonymous callers **401**. Admin status does **not** widen the habit endpoints — an admin still gets 404 for someone else's habit.

The first admin is created from the command line:

```bash
dotnet run --project backend/src/HabitTracker.Api -- promote-admin kisi@example.com
```

Role lives in the access token, so a promoted user must sign in again before the panel opens.

### GET /api/v1/admin/users?search=&includeDeleted=false&page=1&pageSize=25

Offset pagination, `pageSize` ≤ 100. `search` matches the email address, case-insensitively.

```json
{
  "items": [
    {
      "id": "guid",
      "email": "kisi@example.com",
      "role": "user",
      "timeZone": "Europe/Istanbul",
      "isSuspended": false,
      "suspendedAt": null,
      "isDeleted": false,
      "remindersEnabled": true,
      "habitCount": 5,
      "createdAt": "2026-06-09T08:00:00Z"
    }
  ],
  "page": 1, "pageSize": 25, "totalCount": 42
}
```

Password hashes, Google subjects and refresh tokens are never included.

### POST /api/v1/admin/users/{id}/suspend · /unsuspend

Suspending blocks sign-in and **revokes the account's live refresh tokens**, so the session dies within the access token's remaining lifetime.

- **200** updated admin user object
- **404** unknown user
- **422** suspending yourself, or suspending another admin

A suspended user gets **403** on login (only once the password verifies — a wrong password still returns the generic 401) and on `/api/v1/me`.

### GET /api/v1/admin/metrics

```json
{
  "totalUsers": 128, "activeUsers": 120, "suspendedUsers": 3, "deletedUsers": 5,
  "usersWithRemindersOn": 44, "totalHabits": 512, "archivedHabits": 61,
  "checkInsLast7Days": 1840, "newUsersLast30Days": 19
}
```

## Health

### GET /health (public)

**200** `Healthy` (includes a PostgreSQL connectivity probe) / **503** `Unhealthy`.
