# MoneyBoard

MoneyBoard is a responsive personal finance dashboard. Phase 1 contains the React/TypeScript frontend, ASP.NET Core API, SQL Server/EF Core model and authentication foundation. Dashboard values are illustrative preview content; the browser transaction form is not persisted. This setup intentionally has no Docker or WSL requirement.

## Requirements

- Windows 10/11
- Node.js 20+ and npm
- .NET 8 SDK
- SQL Server Express or Developer Edition installed locally
- SQL Server Management Studio (SSMS), for managing the local database

Install [SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) or Developer Edition and [SSMS](https://learn.microsoft.com/en-us/ssms/install/install). During SQL Server setup, create a local instance (the Express default is `SQLEXPRESS`) and enable Windows Authentication. Connect in SSMS using `localhost\SQLEXPRESS` (or the instance name selected during setup). Microsoft documents the [SQL Server Express install](https://www.microsoft.com/en-us/download/details.aspx?id=104781) and [SSMS connection setup](https://learn.microsoft.com/en-us/ssms/quickstarts/ssms-connect).

## Set up the local database and API

Open PowerShell in `MoneyBoard/backend/MoneyBoard.Api`:

1. Create the empty application database in SSMS. Connect to your local server, open **New Query**, and run:

   ```sql
   CREATE DATABASE MoneyBoard;
   GO
   ```

2. Configure a local Windows-authenticated connection string and a development signing key with .NET user-secrets. Secrets live outside the repository:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:MoneyBoard" "Server=localhost\SQLEXPRESS;Database=MoneyBoard;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
   $signingKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
   dotnet user-secrets set "Authentication:SigningKey" $signingKey
   ```

   If your server uses another instance name or a default instance, update `Server=` to match the server name you use in SSMS. The connection string can also be supplied through the `ConnectionStrings__MoneyBoard` environment variable. `TrustServerCertificate=True` is for local development only.

3. Install the EF Core 8 command-line tool if it is not installed, then apply the checked-in SQL Server migration:

   ```powershell
   dotnet tool install --global dotnet-ef --version 8.0.11
   dotnet ef database update
   dotnet run --urls http://localhost:5080
   ```

   The migration creates the ASP.NET Identity tables and MoneyBoard finance tables. The equivalent generated T-SQL is at `backend/Database/001_InitialCreate.sql`; use EF migrations for normal updates so the migration history stays synchronized. Confirm the tables in SSMS under **MoneyBoard → Tables**. If EF reports a connection error, verify the SQL Server service is running and that `Server=` matches the SSMS connection.

## Start the frontend

In another terminal, from the `MoneyBoard` folder:

```powershell
npm install
npm run dev
```

Open `http://localhost:5173`. The API health endpoint is `http://localhost:5080/api/health`.

## Configuration and architecture

`backend/MoneyBoard.Api/appsettings.json` contains non-secret defaults only. Local connection strings and signing keys belong in .NET user-secrets or environment variables, never committed settings. `.env.example` documents the equivalent SQL Server environment variable names; ASP.NET Core does not load `.env` files automatically.

The API uses `ConnectionStrings:MoneyBoard`, mapped from `ConnectionStrings__MoneyBoard`, and the EF Core SQL Server provider. `MoneyBoardDbContextFactory` uses the same appsettings, user-secrets and environment configuration as the API, so EF migrations use the configured local instance. Generate future migrations from `backend/MoneyBoard.Api` with `dotnet ef migrations add <Name>`.

```text
src/                          React UI and responsive styles
backend/MoneyBoard.Api/       ASP.NET Core API, Identity, EF Core
backend/MoneyBoard.Api/Models/ Finance and account entities
backend/MoneyBoard.Api/Data/   SQL Server DbContext, factory and migrations
```

## API foundation

- `POST /api/auth/register` with `{ "name", "email", "password" }`
- `POST /api/auth/login` with `{ "email", "password" }` returns a short-lived bearer token
- `GET /api/auth/me` (bearer token required)
- `POST /api/auth/forgot-password` returns a privacy-preserving response; email delivery and reset-link redemption are not enabled yet
- `GET /api/health`

## Phase 1 scope

The dashboard is a visual shell with illustrative figures. The transaction form is browser-local and is not connected to the API. Onboarding, persisted finance CRUD, reporting, and production account-recovery/session hardening are future work. Do not enter real financial data until persistence and production security are implemented.
