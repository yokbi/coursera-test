# Security Overview

Threat model: a public, multi-tenant web app holding low-sensitivity personal data
(habit history) plus credentials. Primary risks: account takeover, cross-tenant data
access (IDOR), token theft, brute force, and injection. Mitigations below map to the
OWASP Top 10 (2021).

## A01 — Broken Access Control

- Every Application-layer query/command filters by the authenticated `userId` taken
  from the JWT `sub` claim — never from the request body.
- A habit owned by someone else is indistinguishable from a missing one (**404**, not
  403), preventing resource enumeration.
- Integration tests (`HabitApiTests.Idor_UserACannotTouchUserBData`) prove user A
  cannot read/update/archive/check-in/reorder user B's data.

## A02 — Cryptographic Failures

- Passwords: `PasswordHasher<T>` (ASP.NET Identity v3 defaults — PBKDF2-HMAC-SHA256,
  100k iterations, 128-bit salt, per-hash format versioning).
- Refresh tokens: 512-bit random values; only their SHA-256 hashes are stored, so a
  database leak yields no usable tokens.
- JWTs signed with HMAC-SHA256; the signing key must be ≥32 chars and comes from
  configuration (env var / user-secrets) — the app refuses to start without it.
- TLS/HSTS: `UseHsts()` outside Development; deploy behind HTTPS.

## A03 — Injection

- All persistence goes through EF Core LINQ (parameterized). There is no raw SQL,
  no string-concatenated queries anywhere.
- JSON model binding with strict DTO records; unknown enum values are rejected.

## A04 — Insecure Design

- Account lockout: 5 consecutive failures → 15-minute lockout.
- Check-in edit window (7 days, user-timezone aware) enforced server-side.
- Soft delete everywhere: user data is never hard-deleted by user actions.

## A05 — Security Misconfiguration

- Security headers on every API response: `X-Content-Type-Options: nosniff`,
  `X-Frame-Options: DENY`, `Content-Security-Policy: default-src 'none';
  frame-ancestors 'none'`, `Referrer-Policy: no-referrer`, `Permissions-Policy`,
  `Cache-Control: no-store`. Verified by an integration test.
- CORS: explicit allowlist (`Cors:AllowedOrigins`, default `http://localhost:3000`)
  with credentials; no wildcards.
- ProblemDetails (RFC 7807) responses never include stack traces or internals
  (asserted in tests).

## A07 — Identification & Authentication Failures

- Access token TTL 15 minutes; refresh token 30 days, httpOnly + Secure +
  SameSite=Strict cookie scoped to `/api/v1/auth`.
- Refresh rotation on every use; **reuse of a rotated token revokes the whole token
  family** (theft detection). Logout, password change, and account deletion revoke
  server-side.
- Generic `401` for unknown email / wrong password / locked account — no user
  enumeration; a dummy hash verification equalizes response timing for unknown emails.
- Rate limiting (built-in .NET 8 limiter): 5 requests/min/IP on register, login,
  change-password, delete-account; 60/min on refresh. Returns **429**.

## Google OAuth

- Authorization-code flow with **PKCE**; the client secret never leaves the server.
- OAuth CSRF: a random `state` is stored in a short-lived httpOnly `ht_oauth` cookie
  and compared on callback with `CryptographicOperations.FixedTimeEquals`. The cookie
  is SameSite=**Lax** because Strict would not survive Google's cross-site redirect
  back; it carries no session authority, only flow state, and expires in 10 minutes.
- Account takeover: an account is only linked by email when the provider reports
  `email_verified`; unverified identities are rejected outright.
- Open redirect: `returnPath` is accepted only as a relative same-origin path, checked
  on both the callback and the landing page.
- Passwordless accounts cannot be logged into with a password, and the generic 401 is
  reused so their existence is not distinguishable.
- Deleting an account releases its Google link, so no orphaned identity mapping remains.

## Reminder emails

- Opt-in only; a new account is never mailed until the user enables reminders.
- The unsubscribe token is an HMAC-SHA256 over the user id, **purpose-tagged**
  (`unsubscribe:`) so a signature can never be replayed as another token type, and
  compared with `FixedTimeEquals`. Bearing it can only disable reminders.
- Mail bodies escape `& < > " '` in habit names, so a habit called
  `<script>…</script>` cannot inject markup into the message.
- The development sender logs only the subject and the recipient's **domain** —
  never the address, the body, or the unsubscribe link.
- A failing recipient (unknown timezone, transport error) is logged by user id and
  skipped; it never aborts the batch or leaks the address into logs.

## Admin panel

- Every `/api/v1/admin` route requires the `Admin` role, carried as a signed JWT
  claim. Ordinary users receive **403**, anonymous callers **401**.
- Admin status widens nothing else: the owner-scoped habit endpoints are unchanged,
  so an admin still gets 404 for another user's habit. Verified by a test.
- The admin user projection excludes password hashes, Google subjects and refresh
  tokens; a test asserts the response body never contains them.
- Suspension revokes live refresh tokens immediately, so a suspended session cannot
  outlive the current access token (15 minutes at most).
- Suspension is only revealed **after** the password verifies — a wrong password
  still returns the generic 401, so it is not an account-enumeration oracle.
- Admins cannot suspend themselves or other admins, so the panel cannot be locked out.
- The first admin is minted by an explicit CLI command, never by configuration that
  could silently grant privileges.

## Social features

- **Consent-first.** `ShareStreaksWithFriends` defaults to false. A friend who has not
  opted in exposes nothing but the email address the requester already knew; every
  stat is returned as null, not zero.
- Sharing grants a **summary, not access**. Friends never reach the owner-scoped habit
  endpoints — those still return 404 for a friend, verified by a test.
- Friend requests to an unregistered address return the same 204 as a real one, so the
  endpoint cannot be used to enumerate which emails have accounts.
- Only the addressee can accept or decline a request; a requester accepting their own
  would let anyone friend anyone unilaterally.
- Revoking consent and removing a friendship both take effect on the next read, with no
  cached or lingering visibility.
- Deleting an account removes its friendship rows and clears its sharing flag.

## Habit groups

- A member can only link a habit they **own** — enforced on create, join and change.
  Without it, a caller could attach someone else's habit to a group and read its
  progress back.
- Joining is the consent, and it is **narrower** than friend-list sharing: the two
  flags are independent, so group progress never implies general streak sharing.
- An outstanding invitation grants no visibility (403 on the detail endpoint); only
  a joined member sees anyone's progress.
- Only friends can be invited, so a group cannot be used to reach a stranger.
- Only the owner may invite, remove members or delete the group; pending invitees are
  visible only to the owner.
- Group progress is a projection: fellow members still get 404 on the owner-scoped
  habit endpoints, reads and writes alike.
- Deleting an account removes its memberships and the groups it owns.

## CSRF

- The refresh cookie is SameSite=Strict, and `refresh`/`logout` additionally require
  the custom `X-CSRF: 1` header, which cross-site requests cannot set.
- All other mutating endpoints authenticate via the `Authorization` header (not
  cookies), so they are not CSRF-able.

## A08 — Software & Data Integrity

- Dependencies pinned in lockfiles (`package-lock.json`, csproj versions).
- `npm audit` run as part of hardening; no known-vulnerable packages shipped.

## A09 — Logging & Monitoring

- Serilog structured logging with request logging. Passwords, tokens, and cookies
  are never logged (only method/path/status/duration and unhandled exceptions).
- Unhandled exceptions are logged server-side while the client receives a generic 500.

## A10 — SSRF

- The API performs no outbound requests derived from user input.

## Input validation

- FluentValidation on every command: length limits, whitelist weekday names, hex
  color regex, IANA timezone validation, numeric ranges (target ≤ 100000,
  timesPerWeek 1–7), email format, password policy. Mirrored client-side with zod.

## Secrets

- No secrets in the repository. `appsettings.json` ships empty values; Development
  uses a clearly-marked dev-only key. Production configuration comes from environment
  variables (`ConnectionStrings__Default`, `Jwt__SigningKey`) or `dotnet user-secrets`.
  See `.env.example`.

## Frontend

- Access token kept in JS memory only (never localStorage); refresh token is
  httpOnly. The `ht_session` cookie is a non-sensitive routing marker containing `1`.
- Client-side validation mirrors server rules; server remains authoritative.
- React's default escaping everywhere; no `dangerouslySetInnerHTML` with dynamic data
  (the only usage is a static, constant theme-init script).
