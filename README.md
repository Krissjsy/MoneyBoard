# MoneyBoard

MoneyBoard is a responsive personal finance app with a React/TypeScript frontend, ASP.NET Core API, SQL Server/EF Core persistence and authenticated account flows. Dashboard totals and activity are calculated from saved transactions; budgets, savings goals and onboarding setup are also persisted per account.

## Requirements

- Windows 10/11
- Node.js 20+ and npm
- .NET 8 SDK
- SQL Server and the configured MoneyBoard database (connection string stored in .NET user-secrets)

## Start local development

The API uses the existing SQL Server database and .NET user-secrets. Keep the connection string and signing key in user-secrets; do not put either in the repository. From the repository root, start both services with one command:

```powershell
npm.cmd run dev
```

Open `http://localhost:5173`. The API listens at `http://localhost:5080`, and its health endpoint is `http://localhost:5080/api/health`. Vite fails clearly if port 5173 is occupied instead of silently switching ports. The combined command stops both services when either one exits; press Ctrl+C to stop both.

If another application already owns port 5173, leave it running and choose an available MoneyBoard frontend port for this session. For example, in PowerShell run `$env:MONEYBOARD_WEB_PORT=5174; npm.cmd run dev` and open `http://localhost:5174`. The launcher configures API CORS for that selected local origin. It still fails clearly if the selected port is occupied.

Install JavaScript dependencies first with `npm.cmd install` if `node_modules` is absent. The API launch profile sets the Development environment so ASP.NET Core loads the project's User Secrets. Its local CORS policy allows the frontend origin `http://localhost:5173`.

## Configuration and architecture

`backend/MoneyBoard.Api/appsettings.json` contains non-secret defaults only. Local connection strings and signing keys belong in .NET user-secrets or environment variables, never committed settings. `.env.example` documents the equivalent SQL Server environment variable names; ASP.NET Core does not load `.env` files automatically.

The API uses `ConnectionStrings:MoneyBoard`, mapped from `ConnectionStrings__MoneyBoard`, and the EF Core SQL Server provider. `MoneyBoardDbContextFactory` uses the same appsettings, user-secrets and environment configuration as the API, so EF migrations use the configured local instance. Generate future migrations from `backend/MoneyBoard.Api` with `dotnet ef migrations add <Name>`; do not run database creation or migration commands as part of normal startup.

## Password recovery

The login page links to `/forgot-password`; reset links open `/reset-password`. The API uses ASP.NET Core Identity reset tokens, which expire after 30 minutes and are invalidated when used. No schema migration is needed. Requests are limited to five per client IP every 15 minutes. The API returns the same confirmation for known and unknown email addresses.

Email is sent through the configured SMTP server. For local testing, run an SMTP capture service on `127.0.0.1:1025` (for example, Mailpit), then set these non-secret defaults and any provider credentials with .NET user-secrets from `backend/MoneyBoard.Api`:

```powershell
dotnet user-secrets set "Email:SmtpHost" "127.0.0.1"
dotnet user-secrets set "Email:SmtpPort" "1025"
dotnet user-secrets set "Email:EnableSsl" "false"
dotnet user-secrets set "Email:From" "MoneyBoard <no-reply@moneyboard.local>"
dotnet user-secrets set "Frontend:BaseUrl" "http://localhost:5173"
```

If the SMTP server requires authentication, set `Email:Username` and `Email:Password` as user-secrets too. View the captured email in the SMTP tool's inbox and use its reset link. Delivery failures are logged without tokens, email addresses, or provider exception details. Do not use a development SMTP host in production. Set `Frontend:BaseUrl` to the production HTTPS frontend origin and configure production SMTP credentials through the deployment secret manager; production requests with localhost reset-link origins are refused.

The reset API endpoints are `POST /api/auth/forgot-password` (`{ "email" }`) and `POST /api/auth/reset-password` (`{ "email", "token", "newPassword" }`).

When SMTP is unavailable, a loopback-only `POST /api/dev/auth/reset-password` helper is registered only in the Development environment. It requires the existing account email twice and a new password, and applies the normal Identity password policy. It is unavailable in Production.

```text
src/                          React UI and responsive styles
backend/MoneyBoard.Api/       ASP.NET Core API, Identity, EF Core
backend/MoneyBoard.Api/Models/ Finance and account entities
backend/MoneyBoard.Api/Data/   SQL Server DbContext, factory and migrations
scripts/dev.mjs               Starts and supervises frontend and API
```

## API foundation

- `POST /api/auth/register` with `{ "name", "email", "password" }`
- `POST /api/auth/login` with `{ "email", "password" }` returns a short-lived bearer token
- `GET /api/auth/me` (bearer token required)
- `POST /api/auth/forgot-password` returns a privacy-preserving response and sends a reset email when configured
- `POST /api/auth/reset-password` validates the expiring Identity token and changes the password
- `GET /api/dashboard` returns transaction-derived totals and monthly trends
- `GET`, `POST`, `PUT`, and `DELETE /api/transactions` manage saved income and expense transactions
- `/api/budgets`, `/api/savings-goals`, and `/api/debts` provide authenticated financial records
- `PUT /api/setup/progress` and `POST /api/setup/complete` save the onboarding draft and financial setup
- `GET /api/health`
