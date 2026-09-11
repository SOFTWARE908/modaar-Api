# Modaar API

ASP.NET Core 9 Web API for the Modaar mobile app. Vertical-slice layout, EF Core + SQL Server,
JWT bearer auth, Serilog.

## Start here

**Authentication is the part most likely to surprise you — read
[docs/AUTHENTICATION.md](docs/AUTHENTICATION.md) before touching it.**

The short version: a **mobile number is the account**. There is no registration endpoint and no
password. `POST /api/auth/login` sends an OTP, `POST /api/auth/verify-otp` returns the tokens and
creates the account if the number is new, and the profile is filled in afterwards via
`PUT /api/profile`.

## Running locally

```bash
dotnet restore
dotnet run --project modaar.api
```

- API: `http://localhost:5259`
- Swagger: `http://localhost:5259/swagger`

In Development, `OtpSettings:UseFixedCode` is `true`, so **the OTP is always `123456`** and nothing
is sent — no SMS provider is wired up yet.

## Database

Connection string: `ConnectionStrings:ModaarDb` in `appsettings.json`
(dev: `10.110.0.2 / modaar_dev` — needs network access to that host).

Apply migrations:

```bash
dotnet ef database update --project modaar.api/modaar.api.csproj
```

Or generate a script to review / hand to whoever owns the server:

```bash
dotnet ef migrations script --idempotent --project modaar.api/modaar.api.csproj -o migrate.sql
```

| Migration | What it does |
|---|---|
| `20260425150121_InitialCreate` | `Users` |
| `20260425150556_AddAuthTables` | `OtpCodes`, `RefreshTokens` |
| `20260908144455_PhoneFirstAccounts` | phone-first accounts — nullable profile fields, `ProfileStatus`, `OtpCodes.CountryCode` |

## Layout

```
modaar.api/
  Features/            one folder per slice: Controllers / Services / Dtos / Entities / Enums
    Authentication/    login, OTP, tokens
    Users/             profile read + update
  Common/
    Auth/              JWT, password + OTP hashing, OTP generation and delivery
    Errors/            GlobalExceptionHandler, AuthErrorCode -> HTTP mapping
    Validation/        FluentValidation action filter
    Extensions/        DI registration (AddModaarAuth, AddModaarPersistence, ...)
  Persistence/         DbContext, entity configurations, migrations
```

Services return `AuthResult<T>` rather than throwing; `.ToActionResult()` maps it to an HTTP
response with an `errorCode` in the `ProblemDetails` body.

## Before this ships

Collected in [docs/AUTHENTICATION.md](docs/AUTHENTICATION.md#still-open). The blocking ones:

1. **No SMS provider** — OTP delivery is a logging stub.
2. **The JWT signing key in `appsettings.json` is the literal placeholder.** The startup guard only
   rejects an *empty* key, so production boots on a publicly known secret unless it is overridden.
