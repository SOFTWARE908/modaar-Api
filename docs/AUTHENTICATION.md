# Authentication

**Last updated:** 2026-09-08 (commit `81f4d5f`, "refactore login")

## The model in one line

**A mobile number is the account.** There is no registration step and no password. Confirming an
OTP either signs an existing user in or creates the account on the spot; the profile is filled in
afterwards, from inside the app.

That is a deliberate change from the earlier design — see [What changed](#what-changed-2026-09-08)
at the bottom if you remember `POST /api/auth/register`.

## Endpoints

| Method | Route                     | Auth   | Purpose                                          |
|--------|---------------------------|--------|--------------------------------------------------|
| POST   | `/api/auth/login`         | —      | Start a login. Sends an OTP.                     |
| POST   | `/api/auth/verify-otp`    | —      | Confirm the OTP. Returns tokens, creates account if new. |
| POST   | `/api/auth/resend-otp`    | —      | Re-send a code (30s cooldown).                   |
| POST   | `/api/auth/refresh-token` | —      | Rotate an expired access token.                  |
| GET    | `/api/auth/me`            | Bearer | Alias of `GET /api/profile`.                     |
| GET    | `/api/profile`            | Bearer | Read the profile.                                |
| PUT    | `/api/profile`            | Bearer | Fill in / update the profile.                    |

## The login flow

### Step 1 — request a code

```http
POST /api/auth/login
Content-Type: application/json

{ "loginMethod": "PhoneOtp", "phoneNumber": "512345678", "countryCode": "+966" }
```

```json
{
  "success": true,
  "message": "OTP sent successfully",
  "identifier": "+966512345678",
  "otpExpiresInSeconds": 300
}
```

The response is identical whether or not the number is known — the API never reveals
whether an account exists. **Keep `identifier` and send it back verbatim** in the next two calls;
it is the normalised international number and saves the client reassembling it.

### Step 2 — confirm it

```http
POST /api/auth/verify-otp

{ "identifier": "+966512345678", "otpCode": "123456" }
```

```json
{
  "accessToken": "eyJ...",
  "refreshToken": "WF724n9...",
  "accountType": "Tenant",
  "isNewUser": true,
  "profileStatus": "Incomplete"
}
```

Two things happen here depending on whether the number is known:

- **Known number** → signed in. Nothing on the account is modified except `LastLoginAt`.
  Signing in is not a profile edit.
- **Unknown number** → an account is created with `AccountType = Tenant` and
  `ProfileStatus = Incomplete`, then signed in.

`isNewUser` and `profileStatus` are how the app decides between the home screen and the
profile screen. Do not infer it from anything else.

### Step 3 — fill in the profile, whenever

```http
GET /api/profile
Authorization: Bearer {accessToken}
```

```json
{
  "phoneNumber": "512345678", "countryCode": "+966",
  "accountType": "Tenant", "profileStatus": "Incomplete",
  "fullName": null, "email": null, "nationalId": null, "profileImageUrl": null
}
```

```http
PUT /api/profile
Authorization: Bearer {accessToken}

{ "fullName": "Ali Hassan", "accountType": "Owner", "email": "ali@example.com" }
```

The PUT is a **partial update** — any field you omit is left alone. Supplying a full name flips
`profileStatus` to `Complete`. Email and national ID stay optional, but both must be unique across
active accounts (409 `ProfileFieldTaken` otherwise).

## Phone number normalisation

OTPs are keyed on the **full international number**, not the national part. This matters: keying
them on the national part alone let `+966 555000111` and `+20 555000111` supersede each other's
codes and share a resend cooldown.

The server normalises before storing:

- country code gets a leading `+` if missing — `966` → `+966`
- the trunk zero is dropped — `0512345678` → `512345678`
- so `identifier` = `+966512345678`

`Users` stores the two parts separately (`CountryCode` + `PhoneNumber`), unique together while
`IsDeleted = 0`.

> ⚠️ This normalisation is naive — trim, force a `+`, strip leading zeros. There is no real E.164
> parsing, so an oddly formatted number could still create a duplicate account. Worth a
> libphonenumber-style dependency before launch, since the number *is* the identity.

## The other two login methods

`LoginMethod` still has three values, but only one creates accounts:

- **`PhoneOtp`** — the real one. Creates accounts.
- **`NationalIdOtp`** — resolves an account that already has a national ID on its profile, and
  delivers the code by **email**. Returns 404 for an unknown ID; it cannot create an account,
  because an account needs a mobile number to exist.
- **`EmailPassword`** — **dormant**. Nothing in the codebase sets `PasswordHash`, so this always
  returns 401. The plumbing (`BCryptPasswordHasher`, `LoginWithPasswordAsync`) is left in place for
  when email/username login is switched on.

## Tokens

| | |
|---|---|
| Access token | JWT HS256, 60 min. Claims: `sub`, `jti`, `phone_number`, `account_type`, and `email` **only if set** |
| Refresh token | 64 random bytes, stored SHA-256 hashed, 30 days |
| Rotation | `/refresh-token` revokes the old token and links it via `ReplacedByTokenId` |

Send the access token as `Authorization: Bearer {token}`.

## OTP rules

Configured under `OtpSettings` in `appsettings.json`:

| Setting | Default | Meaning |
|---|---|---|
| `LengthDigits` | 6 | code length |
| `ExpiresInMinutes` | 5 | how long a code lives |
| `MaxAttempts` | 5 | wrong guesses before the code is dead |
| `ResendCooldownSeconds` | 30 | applies to `/login` **and** `/resend-otp` |
| `UseFixedCode` | `false` | dev only — see below |

Behaviour worth knowing:

- Issuing a new code **supersedes** every live code for that identifier. Only the newest works.
- A correct code is **burned immediately**, before the account is looked up, so it can never be
  replayed even if a later step fails.

## Error codes

Failures are RFC 7807 `ProblemDetails` with an `errorCode` extension:

```json
{ "title": "Invalid OTP", "status": 400, "detail": "Invalid OTP code.", "errorCode": "OtpInvalid" }
```

| `errorCode` | HTTP | When |
|---|---|---|
| `OtpInvalid` | 400 | wrong code |
| `OtpExpired` | 410 | no live code — expired, superseded, or already used |
| `OtpMaxAttempts` | 429 | too many wrong guesses |
| `OtpResendCooldown` | 429 | asked for a code within 30s |
| `UserNotFound` | 404 | identifier names no account and cannot create one |
| `AccountInactive` | 403 | `IsActive = false` |
| `ProfileFieldTaken` | 409 | email or national ID belongs to another account |
| `InvalidCredentials` | 401 | email/password login (dormant) |
| `InvalidRefreshToken` | 401 | refresh token unknown, revoked, or expired |
| `AccountTypeNotAllowed` | 400 | tried to select `Technician` |
| `UnsupportedLoginMethod` | 400 | resend asked for on `EmailPassword` |

## Running it locally

`appsettings.Development.json` sets `OtpSettings:UseFixedCode = true`, so **the OTP is always
`123456`** and nothing is actually sent — the code is written to the log. That is the only way to
test today: `LoggingOtpDeliverySender` is the registered `IOtpDeliverySender`, and it refuses to log
a real code when `UseFixedCode` is false. **A real SMS provider still needs wiring before launch.**

```bash
dotnet run --project modaar.api
# Swagger: http://localhost:5259/swagger
```

Full round trip:

```bash
curl -X POST http://localhost:5259/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"loginMethod":"PhoneOtp","phoneNumber":"512345678","countryCode":"+966"}'

curl -X POST http://localhost:5259/api/auth/verify-otp \
  -H 'Content-Type: application/json' \
  -d '{"identifier":"+966512345678","otpCode":"123456"}'
```

## Where the code lives

```
Features/Authentication/
  Controllers/AuthController.cs      login, verify-otp, resend-otp, refresh-token, me
  Services/AuthService.cs            the whole flow — OTP issue/verify, account creation, tokens
  Entities/OtpCode.cs                Identifier is the FULL international number for phone OTPs
  Entities/RefreshToken.cs
Features/Users/
  Controllers/ProfileController.cs   GET/PUT /api/profile
  Services/ProfileService.cs         partial update + uniqueness checks
  Entities/User.cs                   phone required, everything else nullable
  Enums/ProfileStatus.cs
Common/Auth/                         JWT, hashing, OTP generation/delivery
Common/Errors/AuthResultExtensions   AuthErrorCode -> HTTP status mapping
```

`AuthService.VerifyOtpAsync` is the one method to read if you only read one.

---

## What changed (2026-09-08)

The original design had a separate `POST /api/auth/register` that took full details up front and
handed back tokens immediately. It was replaced with the phone-first flow above.

**Removed:** `POST /api/auth/register`, `RegisterRequestDto`, `AuthService.RegisterAsync`.
An intermediate draft-registration design (a `RegistrationStatus` Pending/Active column) was
prototyped and dropped before it reached any database.

**Schema** (migration `20260908144455_PhoneFirstAccounts`):

- `Users.FullName`, `Email`, `NationalId` → nullable
- `Users.ProfileStatus` added; existing rows backfilled to `Complete`
- `OtpCodes.CountryCode` added, so an unknown number can be turned into an account at verification
  time, when the original login request is no longer available
- `IX_Users_Email` / `IX_Users_NationalId` recreated with an `IS NOT NULL` filter — without it SQL
  Server allows only *one* account with no email

**Why the old flow was dropped:** register issued tokens with zero verification, so anyone could
claim any phone number or national ID and permanently block the real owner via the uniqueness
index. Nothing ever set `PasswordHash`, so email/password login could not succeed anyway.

### Fixed along the way

- A correct OTP was only marked consumed *after* the user lookup succeeded, and the write was
  never saved on the failure paths — a valid code stayed replayable until it expired.
- Requesting a code via `/login` had no cooldown at all. The 30s limit was only on `/resend-otp`,
  which was trivially bypassed by calling `/login` again — unlimited SMS to any number.
- Older OTPs were not invalidated when a new one was issued; every unexpired code still worked.
- OTPs were keyed on the bare national number, colliding across country codes (see
  [normalisation](#phone-number-normalisation)).
- The JWT built an `email` claim unconditionally, which throws once email is nullable.

### Still open

- **No SMS provider.** `LoggingOtpDeliverySender` is a dev stub. Blocking for launch.
- **Naive phone normalisation** — see the warning above.
- **The signing key in `appsettings.json` is the literal placeholder** `"your-signing-key-here…"`.
  The startup guard only checks for *empty*, so production will happily boot on a public key.
- **OTP hashing is unsalted SHA-256** of a 6-digit code — a 10⁶ rainbow table. Low severity given
  the 5-minute expiry, but an HMAC with a server-side pepper costs nothing.
- **No refresh-token reuse detection.** Replaying a revoked token returns 401 but does not revoke
  the descendant chain, so a stolen token goes unnoticed.
- **No logout / revoke endpoint.**
- `VerifyOtpAsync` returns 404 `UserNotFound` for an unknown national ID, which leaks existence —
  undoing the enumeration protection the rest of the flow is careful about.
- Swagger UI is enabled unconditionally; the `IsDevelopment()` guard in `Program.cs` is commented out.
- `UpdateProfileRequestValidator` is the only FluentValidation validator in the project. The
  `ValidationActionFilter` runs against everything, so other DTOs are simply unvalidated.
