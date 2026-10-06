# ATSPM 5 Fresh Install Runbook

This runbook describes how to set up a fresh local ATSPM 5 development environment from an empty machine or empty database set.

Use this when onboarding a developer, rebuilding a local environment, creating clean test databases, or validating that the current branch can boot from scratch.

## 1. Install Prerequisites

Install these tools before cloning or running the application:

- Git
- .NET SDK matching the solution target
- Node.js and npm
- Docker Desktop, if using Docker Compose
- PostgreSQL tools, if using PostgreSQL locally
- SQL Server tools, if using SQL Server locally
- OpenSSL, if running the Docker HTTPS gateway locally

Confirm the basics:

```powershell
git --version
dotnet --version
node --version
npm --version
docker --version
```

## 2. Clone The Repository

```powershell
git clone <repo-url> ATSPM5.0
cd ATSPM5.0
git checkout ATSPM5Dev
git pull
```

If this is a fork, add the upstream repository once:

```powershell
git remote add upstream https://github.com/utahudot/udot-atspm.git
git fetch upstream
```

## 3. Choose The Local Database Provider

ATSPM 5 supports multiple providers, but local development is usually easiest with PostgreSQL or SQL Server.

Recommended local PostgreSQL database names:

- `ATSPM-Config`
- `ATSPM-Aggregation`
- `ATSPM-EventLogs`
- `ATSPM-Identity`

Recommended local SQL Server database names:

- `ATSPM_Config`
- `ATSPM_Aggregation`
- `ATSPM_EventLog`
- `ATSPM_Identity`

Create empty databases before running migrations, or let your provider create them if your local permissions allow it.

## 4. Configure Local Secrets

Do not commit local database passwords, JWT signing keys, API keys, or production credentials.

ATSPM 5 currently reads database connection details from:

```text
DatabaseConfiguration:{ContextName}
```

Each API or utility that opens database contexts needs settings for:

- `ConfigContext`
- `AggregationContext`
- `EventLogContext`
- `IdentityContext`

Initialize user secrets from the `ATSPM` folder:

```powershell
cd .\ATSPM

dotnet user-secrets init --project .\ConfigApi\ConfigApi.csproj
dotnet user-secrets init --project .\DataApi\DataApi.csproj
dotnet user-secrets init --project .\ReportApi\ReportApi.csproj
dotnet user-secrets init --project .\IdentityApi\IdentityApi.csproj
dotnet user-secrets init --project .\DatabaseInstaller\DatabaseInstaller.csproj
dotnet user-secrets init --project .\EventLogUtility\EventLogUtility.csproj
```

Set JWT values. Use the same `Jwt:Key` and `Jwt:Issuer` anywhere tokens are created or validated.

```powershell
dotnet user-secrets set "Jwt:Issuer" "AvenueConsultants" --project .\IdentityApi\IdentityApi.csproj
dotnet user-secrets set "Jwt:Key" "<local-dev-signing-key>" --project .\IdentityApi\IdentityApi.csproj
dotnet user-secrets set "Jwt:ExpireDays" "1" --project .\IdentityApi\IdentityApi.csproj

dotnet user-secrets set "Jwt:Issuer" "AvenueConsultants" --project .\ConfigApi\ConfigApi.csproj
dotnet user-secrets set "Jwt:Key" "<local-dev-signing-key>" --project .\ConfigApi\ConfigApi.csproj
dotnet user-secrets set "Jwt:ExpireDays" "1" --project .\ConfigApi\ConfigApi.csproj

dotnet user-secrets set "Jwt:Issuer" "AvenueConsultants" --project .\DataApi\DataApi.csproj
dotnet user-secrets set "Jwt:Key" "<local-dev-signing-key>" --project .\DataApi\DataApi.csproj
dotnet user-secrets set "Jwt:ExpireDays" "1" --project .\DataApi\DataApi.csproj

dotnet user-secrets set "Jwt:Issuer" "AvenueConsultants" --project .\ReportApi\ReportApi.csproj
dotnet user-secrets set "Jwt:Key" "<local-dev-signing-key>" --project .\ReportApi\ReportApi.csproj
dotnet user-secrets set "Jwt:ExpireDays" "1" --project .\ReportApi\ReportApi.csproj
```

For PostgreSQL, set database values for each API and utility. Repeat these settings for every project that needs database access.

```powershell
dotnet user-secrets set "DatabaseConfiguration:ConfigContext:DBType" "PostgreSql" --project .\ConfigApi\ConfigApi.csproj
dotnet user-secrets set "DatabaseConfiguration:ConfigContext:Host" "localhost" --project .\ConfigApi\ConfigApi.csproj
dotnet user-secrets set "DatabaseConfiguration:ConfigContext:Port" "5432" --project .\ConfigApi\ConfigApi.csproj
dotnet user-secrets set "DatabaseConfiguration:ConfigContext:Database" "ATSPM-Config" --project .\ConfigApi\ConfigApi.csproj
dotnet user-secrets set "DatabaseConfiguration:ConfigContext:User" "<db-user>" --project .\ConfigApi\ConfigApi.csproj
dotnet user-secrets set "DatabaseConfiguration:ConfigContext:Password" "<db-password>" --project .\ConfigApi\ConfigApi.csproj
```

At minimum:

- `ConfigApi` needs `ConfigContext`.
- `DataApi` needs `ConfigContext`, `AggregationContext`, and `EventLogContext`.
- `ReportApi` needs `ConfigContext`, `AggregationContext`, and `EventLogContext`.
- `IdentityApi` needs `IdentityContext`.
- `DatabaseInstaller` needs all four contexts.
- `EventLogUtility` needs the contexts used by the command you run.

## 5. Restore And Build

From the repository root:

```powershell
dotnet restore .\ATSPM\ATSPM.sln
dotnet build .\ATSPM\ConfigApi\ConfigApi.csproj -m:1
dotnet build .\ATSPM\DataApi\DataApi.csproj -m:1
dotnet build .\ATSPM\ReportApi\ReportApi.csproj -m:1
dotnet build .\ATSPM\IdentityApi\IdentityApi.csproj -m:1
dotnet build .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -m:1
dotnet build .\ATSPM\EventLogUtility\EventLogUtility.csproj -m:1
```

Build the WebUI:

```powershell
cd .\ATSPM\WebUI
npm install
npm run build
cd ..\..
```

Do not continue to database setup until these builds pass.

## 6. Apply Initial Database Migrations

Run `DatabaseInstaller update` from the repository root.

PostgreSQL example:

```powershell
dotnet run --project .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -- update `
  --provider PostgreSQL `
  --config-connection "Host=localhost;Database=ATSPM-Config;Username=<db-user>;Password=<db-password>" `
  --aggregation-connection "Host=localhost;Database=ATSPM-Aggregation;Username=<db-user>;Password=<db-password>" `
  --eventlog-connection "Host=localhost;Database=ATSPM-EventLogs;Username=<db-user>;Password=<db-password>" `
  --identity-connection "Host=localhost;Database=ATSPM-Identity;Username=<db-user>;Password=<db-password>" `
  --seed-admin true `
  --admin-email "admin@example.com" `
  --admin-password "<local-admin-password>" `
  --admin-role "Admin"
```

SQL Server example:

```powershell
dotnet run --project .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -- update `
  --provider SqlServer `
  --config-connection "Server=localhost;Database=ATSPM_Config;Trusted_Connection=True;TrustServerCertificate=True" `
  --aggregation-connection "Server=localhost;Database=ATSPM_Aggregation;Trusted_Connection=True;TrustServerCertificate=True" `
  --eventlog-connection "Server=localhost;Database=ATSPM_EventLog;Trusted_Connection=True;TrustServerCertificate=True" `
  --identity-connection "Server=localhost;Database=ATSPM_Identity;Trusted_Connection=True;TrustServerCertificate=True" `
  --seed-admin true `
  --admin-email "admin@example.com" `
  --admin-password "<local-admin-password>" `
  --admin-role "Admin"
```

The update command applies migrations for `ConfigContext`, `AggregationContext`, `EventLogContext`, and `IdentityContext`.

## 7. Load Configuration Data

A fresh database has schema, but it does not have useful location/device/report configuration until data is seeded or imported.

To import configuration from a Config API:

```powershell
dotnet run --project .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -- transfer-config `
  --api-base-url "https://atspm.udot.utah.gov/config/" `
  --api-key "<config-api-key>" `
  --update-locations true
```

To also refresh speed devices:

```powershell
dotnet run --project .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -- transfer-config `
  --api-base-url "https://atspm.udot.utah.gov/config/" `
  --api-key "<config-api-key>" `
  --update-speed true
```

For a clean local rebuild where existing config can be discarded:

```powershell
dotnet run --project .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -- transfer-config `
  --api-base-url "https://atspm.udot.utah.gov/config/" `
  --api-key "<config-api-key>" `
  --update-locations true `
  --delete true
```

Use `--delete true` carefully. It clears existing target configuration when used with `--update-locations`.

## 8. Load Event Log Data

Skip this step if you only need to test admin/configuration screens.

Copy already-compressed event logs from SQL Server into PostgreSQL:

```powershell
dotnet run --project .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -- copy-sql `
  --source "Server=localhost;Database=ATSPM-EventLogs;Trusted_Connection=True;TrustServerCertificate=True" `
  --start "2024-01-01" `
  --end "2024-01-31" `
  --batch-size 500 `
  --resume `
  --locations "LOC1,LOC2"
```

Transfer legacy/raw SQL Server event logs:

```powershell
dotnet run --project .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -- transfer `
  --source "Server=localhost;Database=MOE;Trusted_Connection=True;TrustServerCertificate=True" `
  --start "2024-01-01" `
  --end "2024-01-31" `
  --locations "LOC1,LOC2" `
  --batch 250
```

Transfer speed events:

```powershell
dotnet run --project .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -- transfer-speed `
  --source "Server=localhost;Database=MOE;Trusted_Connection=True;TrustServerCertificate=True" `
  --start "2024-01-01" `
  --end "2024-01-31"
```

## 9. Configure The WebUI Runtime Environment

The WebUI reads runtime values from its environment/runtime configuration. Confirm these values point at the local APIs or local gateway:

- Config API base URL
- Data API base URL
- Report API base URL
- Identity API base URL
- Sponsor image URL, if used
- Powered-by image URL, if used
- Google map/tile settings, if used
- Speed limit map layer, if used

When using Docker Compose, check `ATSPM/frontend-env.txt` and the WebUI volume mapping in `docker-compose.yml`.

## 10. Run Locally Without Docker

Start each API in a separate terminal:

```powershell
dotnet run --project .\ATSPM\ConfigApi\ConfigApi.csproj
dotnet run --project .\ATSPM\DataApi\DataApi.csproj
dotnet run --project .\ATSPM\ReportApi\ReportApi.csproj
dotnet run --project .\ATSPM\IdentityApi\IdentityApi.csproj
```

Start the WebUI:

```powershell
cd .\ATSPM\WebUI
npm run dev
```

Default WebUI URL:

```text
http://localhost:3000
```

## 11. Run Locally With Docker Compose

Generate local HTTPS certificates from the `ATSPM` folder:

```powershell
mkdir nginx\certs
openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout nginx\certs\aspnetapp.key -out nginx\certs\aspnetapp.crt -subj "/CN=localhost"
openssl pkcs12 -export -out nginx\certs\aspnetapp.pfx -inkey nginx\certs\aspnetapp.key -in nginx\certs\aspnetapp.crt -passout pass:password
```

Create `ATSPM/.env` with local-only values. Do not commit it.

Important: make sure Docker environment variables use the current `DatabaseConfiguration__...` shape. If `docker-compose.yml` still maps only `ConnectionStrings__...`, update it or the APIs may start without usable database settings.

Start the stack:

```powershell
cd .\ATSPM
docker compose up --build
```

Expected local endpoints:

- WebUI: `http://localhost:3000`
- HTTPS gateway: `https://localhost:3443`
- Config API container port: `https://localhost:44400`
- Data API container port: `https://localhost:44401`
- Report API container port: `https://localhost:44402`
- Identity API container port: `https://localhost:44403`

## 12. Fresh Install Smoke Test

After the APIs and WebUI are running:

1. Sign in with the seeded admin user.
2. Confirm the WebUI loads without runtime environment errors.
3. Open admin pages for locations, devices, products, areas, regions, jurisdictions, roles, menu items, and FAQ.
4. Confirm location map tiles and markers render.
5. Confirm Config API can return locations.
6. Confirm Identity API can issue a token.
7. Confirm Data API can reach the event log database.
8. Confirm Report API can run at least one report using imported configuration.
9. If event logs were loaded, run one report for a known location/date.
10. If speed data was loaded, open the speed management pages and check map/vector layer behavior.

## 13. Common Fresh Install Problems

`DatabaseConfiguration:{ContextName}` missing:
The APIs may compile but fail at runtime. Add the nested database settings to user secrets, environment variables, or appsettings.

JWT token validation fails:
Make sure `Jwt:Key` and `Jwt:Issuer` match across IdentityApi, ConfigApi, DataApi, and ReportApi.

WebUI build fails from Visual Studio or solution build:
Make sure both `node` and `npm` are available on PATH.

Docker APIs cannot connect to PostgreSQL:
Use `Host=postgres` or `DatabaseConfiguration__...__Host=postgres` inside Docker, not `localhost`.

CORS errors from the browser:
Confirm each API has `CorsPolicies:Default:Origins` including the WebUI origin, usually `http://localhost:3000`.

No locations or devices appear:
Run `transfer-config --update-locations true`, or confirm your source Config API and API key are valid.

Reports return no data:
Confirm event logs exist for the selected location/date range and that the location identifiers match the imported configuration.

## 14. Fresh Install Checklist

1. Install Git, .NET SDK, Node/npm, Docker, and database tools.
2. Clone repo and check out the dev branch.
3. Create empty local databases.
4. Configure user secrets for database contexts and JWT.
5. Restore and build backend projects.
6. Install and build WebUI packages.
7. Run `DatabaseInstaller update`.
8. Import configuration with `transfer-config`.
9. Load event logs only if report testing requires them.
10. Start APIs and WebUI.
11. Smoke test login, admin pages, map, APIs, reports, and speed management.
