# ATSPM 5 Local Upgrade Runbook

This runbook describes the repeatable process for updating a local ATSPM 5 development environment after new upstream changes are merged into the dev branch. It covers the application/APIs, local database schema, configuration import, and validation.

Use this process every time the fork is updated from the upstream ATSPM repository.

## 1. Decide What Kind Of Update This Is

Before touching the database, identify the scope of the incoming update.

1. Review the merged changes:

   ```powershell
   git status --short
   git log --oneline --decorate -10
   git diff --name-only HEAD~1..HEAD
   ```

2. Look for database or backend-impacting changes:

   ```powershell
   rg -n "Migration|DbSet|OnModelCreating|DatabaseConfiguration|Jwt|PathBase|CorsPolicies|DeviceConfiguration|TransportProtocol" ATSPM
   ```

3. If the update includes migrations, model changes, API auth changes, appsettings changes, or package changes, run the full process below.

4. If the update is frontend-only, still run the backend compile checks, but a database migration may not be necessary.

## 2. Protect The Current Local Database

Never run migrations or destructive imports without a backup when the local database contains work you care about.

For PostgreSQL local databases:

```powershell
pg_dump -h localhost -U postgres -Fc -d ATSPM-Config -f .\backups\ATSPM-Config-before-upgrade.dump
pg_dump -h localhost -U postgres -Fc -d ATSPM-Aggregation -f .\backups\ATSPM-Aggregation-before-upgrade.dump
pg_dump -h localhost -U postgres -Fc -d ATSPM-EventLogs -f .\backups\ATSPM-EventLogs-before-upgrade.dump
pg_dump -h localhost -U postgres -Fc -d ATSPM-Identity -f .\backups\ATSPM-Identity-before-upgrade.dump
```

For SQL Server local databases:

```sql
BACKUP DATABASE [ATSPM_Config] TO DISK = 'C:\Backups\ATSPM_Config_before_upgrade.bak';
BACKUP DATABASE [ATSPM_Aggregation] TO DISK = 'C:\Backups\ATSPM_Aggregation_before_upgrade.bak';
BACKUP DATABASE [ATSPM_EventLog] TO DISK = 'C:\Backups\ATSPM_EventLog_before_upgrade.bak';
BACKUP DATABASE [ATSPM_Identity] TO DISK = 'C:\Backups\ATSPM_Identity_before_upgrade.bak';
```

For disposable local data, it is usually faster to recreate the local databases and reseed/import configuration after the code update.

## 3. Update Local Secrets And Configuration

ATSPM 5 now expects database settings under `DatabaseConfiguration:{ContextName}`. The old `ConnectionStrings:{ContextName}` shape may still appear in some files, but the current infrastructure registration reads `DatabaseConfiguration`.

Each API and utility that opens database contexts needs these sections:

```json
{
  "DatabaseConfiguration": {
    "ConfigContext": {
      "DBType": "PostgreSql",
      "Host": "localhost",
      "Port": 5432,
      "Database": "ATSPM-Config",
      "User": "postgres",
      "Password": "postgres",
      "RunMigrations": false,
      "Options": {
        "Pooling": "true",
        "Timeout": "30",
        "CommandTimeout": "60"
      }
    },
    "AggregationContext": {
      "DBType": "PostgreSql",
      "Host": "localhost",
      "Port": 5432,
      "Database": "ATSPM-Aggregation",
      "User": "postgres",
      "Password": "postgres"
    },
    "EventLogContext": {
      "DBType": "PostgreSql",
      "Host": "localhost",
      "Port": 5432,
      "Database": "ATSPM-EventLogs",
      "User": "postgres",
      "Password": "postgres"
    },
    "IdentityContext": {
      "DBType": "PostgreSql",
      "Host": "localhost",
      "Port": 5432,
      "Database": "ATSPM-Identity",
      "User": "postgres",
      "Password": "postgres"
    }
  }
}
```

Use user secrets or environment variables for real values. Do not commit local passwords, API keys, or JWT signing keys.

Recommended local secret setup from the `ATSPM` folder:

```powershell
dotnet user-secrets init --project .\ConfigApi\ConfigApi.csproj
dotnet user-secrets init --project .\DataApi\DataApi.csproj
dotnet user-secrets init --project .\ReportApi\ReportApi.csproj
dotnet user-secrets init --project .\IdentityApi\IdentityApi.csproj
dotnet user-secrets init --project .\DatabaseInstaller\DatabaseInstaller.csproj
```

Set the shared JWT values consistently for all APIs that validate tokens:

```powershell
dotnet user-secrets set "Jwt:Issuer" "AvenueConsultants" --project .\IdentityApi\IdentityApi.csproj
dotnet user-secrets set "Jwt:Key" "<local-dev-signing-key>" --project .\IdentityApi\IdentityApi.csproj
dotnet user-secrets set "Jwt:ExpireDays" "1" --project .\IdentityApi\IdentityApi.csproj

dotnet user-secrets set "Jwt:Issuer" "AvenueConsultants" --project .\DataApi\DataApi.csproj
dotnet user-secrets set "Jwt:Key" "<same-local-dev-signing-key>" --project .\DataApi\DataApi.csproj
dotnet user-secrets set "Jwt:ExpireDays" "1" --project .\DataApi\DataApi.csproj

dotnet user-secrets set "Jwt:Issuer" "AvenueConsultants" --project .\ReportApi\ReportApi.csproj
dotnet user-secrets set "Jwt:Key" "<same-local-dev-signing-key>" --project .\ReportApi\ReportApi.csproj
dotnet user-secrets set "Jwt:ExpireDays" "1" --project .\ReportApi\ReportApi.csproj
```

If running with Docker Compose, update the compose environment mappings to use:

```yaml
DatabaseConfiguration__ConfigContext__DBType: "PostgreSql"
DatabaseConfiguration__ConfigContext__Host: "postgres"
DatabaseConfiguration__ConfigContext__Port: "5432"
DatabaseConfiguration__ConfigContext__Database: "ATSPM-Config"
DatabaseConfiguration__ConfigContext__User: "${POSTGRES_USER}"
DatabaseConfiguration__ConfigContext__Password: "${POSTGRES_PASSWORD}"
```

Repeat that shape for `AggregationContext`, `EventLogContext`, and `IdentityContext`.

## 4. Restore Packages And Build Before Migrating

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

For the WebUI:

```powershell
cd .\ATSPM\WebUI
npm install
npm run build
cd ..\..
```

Do not run migrations until the backend projects compile.

## 5. Apply Database Migrations

Run the `DatabaseInstaller update` command. This applies migrations for:

- `ConfigContext`
- `AggregationContext`
- `EventLogContext`
- `IdentityContext`

PostgreSQL example:

```powershell
dotnet run --project .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -- update `
  --provider PostgreSQL `
  --config-connection "Host=localhost;Database=ATSPM-Config;Username=postgres;Password=postgres" `
  --aggregation-connection "Host=localhost;Database=ATSPM-Aggregation;Username=postgres;Password=postgres" `
  --eventlog-connection "Host=localhost;Database=ATSPM-EventLogs;Username=postgres;Password=postgres" `
  --identity-connection "Host=localhost;Database=ATSPM-Identity;Username=postgres;Password=postgres" `
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

If migrations fail, stop and restore from backup before trying a different migration strategy.

## 6. Import Or Refresh Configuration Data

Use `transfer-config` when the local config database needs to match a source Config API.

Import locations and related configuration:

```powershell
dotnet run --project .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -- transfer-config `
  --api-base-url "https://atspm.udot.utah.gov/config/" `
  --api-key "<config-api-key>" `
  --update-locations true
```

Refresh locations destructively:

```powershell
dotnet run --project .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -- transfer-config `
  --api-base-url "https://atspm.udot.utah.gov/config/" `
  --api-key "<config-api-key>" `
  --update-locations true `
  --delete true
```

Import or update only speed devices:

```powershell
dotnet run --project .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -- transfer-config `
  --api-base-url "https://atspm.udot.utah.gov/config/" `
  --api-key "<config-api-key>" `
  --update-speed true
```

Use `--delete true --update-speed true` only when you want to delete existing speed devices before re-importing them. That mode preserves non-speed devices, locations, products, and device configurations.

## 7. Transfer Or Rebuild Event Logs Only When Needed

Most schema upgrades do not require reloading event logs. Only run these commands when the event log storage format changed, when moving data between providers, or when creating a fresh local database.

Copy already-compressed SQL Server event logs into PostgreSQL:

```powershell
dotnet run --project .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -- copy-sql `
  --source "Server=localhost;Database=ATSPM-EventLogs;Trusted_Connection=True;TrustServerCertificate=True" `
  --start "2024-01-01" `
  --end "2024-01-31" `
  --batch-size 500 `
  --resume `
  --locations "LOC1,LOC2"
```

Transfer legacy/raw SQL Server event logs into the current event log store:

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

## 8. Start The Backend APIs

Run the APIs individually while debugging:

```powershell
dotnet run --project .\ATSPM\ConfigApi\ConfigApi.csproj
dotnet run --project .\ATSPM\DataApi\DataApi.csproj
dotnet run --project .\ATSPM\ReportApi\ReportApi.csproj
dotnet run --project .\ATSPM\IdentityApi\IdentityApi.csproj
```

Or run the Docker stack from the `ATSPM` folder after `.env` and certificates are updated:

```powershell
docker compose up --build
```

Expected local paths:

- Config API: `/config`
- Data API: `/data`
- Report API: `/report`
- Identity API: `/identity` if configured by the hosting/proxy layer
- WebUI: `http://localhost:3000`
- Nginx HTTPS gateway: `https://localhost:3443`

## 9. Smoke Test The Upgrade

After migrations and imports, check the high-value flows:

1. Sign in with the seeded admin account.
2. Open the WebUI and confirm it loads runtime environment values.
3. Open the location map and confirm tiles, selected-location zoom, and markers still work.
4. Open admin pages for locations, devices, products, areas, regions, jurisdictions, roles, menu items, and FAQ.
5. Confirm `ConfigApi` can return locations and related OData expansions.
6. Run one report that uses configuration data.
7. Run one report that uses event log data.
8. Check speed management pages if the update touched speed devices, route sources, or vector map layers.
9. Run EventLogUtility against a small controlled folder or test device set.
10. Check application logs for auth, database binding, missing migrations, missing JWT values, and CORS errors.

Useful compile checks:

```powershell
dotnet build .\ATSPM\ConfigApi\ConfigApi.csproj -m:1 -v:minimal
dotnet build .\ATSPM\DatabaseInstaller\DatabaseInstaller.csproj -m:1 -v:minimal
dotnet build .\ATSPM\Infrastructure\Infrastructure.csproj -m:1 -v:minimal
dotnet build .\ATSPM\EventLogUtility\EventLogUtility.csproj -m:1 -v:minimal
```

Useful Git checks:

```powershell
git diff --name-only --diff-filter=U
rg -n "<<<<<<<|>>>>>>>" ATSPM Atspm .gitignore
git diff --check
```

## 10. Commit The Upgrade

Once the local database and APIs are working:

```powershell
git status --short
git add .
git commit -m "Merge upstream ATSPM 5 updates into dev"
```

If this was a merge from upstream, do not squash away the merge history unless the team intentionally wants a linear history.

## 11. Repeatable Upgrade Checklist

Use this short checklist after the first full run:

1. Fetch upstream and merge/rebase into the dev branch.
2. Resolve conflicts, preserving local features and incoming upstream features.
3. Run conflict-marker checks.
4. Back up local databases.
5. Update local secrets/config to match any new appsettings shape.
6. Restore and build backend projects.
7. Run `DatabaseInstaller update`.
8. Run `transfer-config` if configuration data needs to be refreshed.
9. Transfer event logs only if schema/storage changes require it.
10. Start APIs and WebUI.
11. Smoke test auth, location map, admin config pages, reports, speed management, and event log utility.
12. Commit the merge/update after validation.

## Current Upgrade Notes

For the current ATSPM 5 merge, pay special attention to these items:

- Database context binding now uses `DatabaseConfiguration:{ContextName}` in infrastructure code.
- Local/Docker env mappings that only set `ConnectionStrings__...` should be updated.
- `Jwt:Key`, `Jwt:Issuer`, and `Jwt:ExpireDays` must be provided through local secrets or environment variables.
- WebUI solution builds require both `node` and `npm` on PATH because the Visual Studio JavaScript SDK invokes `npm install`.
- `transfer-config` can import locations/configuration and can update speed devices separately.
- The merge preserved SQL Server identity insert support for much of the configuration import path, but detector import should be smoke-tested carefully if preserving legacy detector IDs is required.
