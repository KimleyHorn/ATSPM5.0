# ATSPM 5 On-Site Database Migration Runbook

This runbook describes the on-site process for upgrading a client from an older ATSPM 5 build to a newer ATSPM 5 build, starting with the database migrations. It is written for field upgrades where the client already has a working ATSPM 5 deployment and production data must be preserved.

Use this runbook for same-major-version ATSPM 5 upgrades. If the client is moving from ATSPM 4, a legacy MOE database, or a different event log storage model, treat that as a data conversion project and plan a separate migration path before using this runbook.

## 1. Scope And Assumptions

ATSPM 5 uses separate Entity Framework contexts and usually separate databases for:

- `ConfigContext`
- `AggregationContext`
- `EventLogContext`
- `IdentityContext`

The `DatabaseInstaller update` command applies pending migrations for all four contexts and can optionally seed or update the admin account.

This runbook assumes:

- The target application package has already been tested in a staging or internal environment.
- The newer ATSPM 5 code can connect to the same database provider used by the client site.
- The upgrade is being performed during an approved maintenance window.
- A database administrator or site owner is available to approve backups, rollback, and final cutover.
- The operator has credentials for the application server, database server, and any service account used by ATSPM.

## 2. Pre-Site Preparation

Complete these items before traveling to the client site or before the maintenance window starts.

1. Confirm the source and target versions:

   ```text
   Client/site:
   Current ATSPM version/build:
   Target ATSPM version/build:
   Database provider:
   Database server:
   Application server:
   Planned maintenance window:
   Rollback decision deadline:
   ```

2. Review release notes and migrations in the target build. Pay special attention to migrations under each provider project:

   ```text
   SqlDatabaseProvider/Migrations
   PostgreSQLDatabaseProvider/Migrations
   MySqlDatabaseProvider/Migrations
   OracleDatabaseProvider/Migrations
   SqlLiteDatabaseProvider/Migrations
   ```

3. Build or obtain the exact `DatabaseInstaller` package that matches the target ATSPM application build. Do not run a `DatabaseInstaller` from a different commit or release.

4. Test the target build against a restored copy of the client database when possible. Record migration duration, any long-running indexes, and expected warnings.

5. Prepare a site-specific command sheet with real connection strings, database names, service names, paths, and contact information. Store passwords securely; do not add them to this repository or to the runbook.

6. Confirm the rollback plan with the client. Rollback normally means restoring all ATSPM databases from the backup taken immediately before migration and redeploying the previous application build.

## 3. On-Site Pre-Flight Checklist

Run these checks on site before stopping services.

1. Confirm you are on the correct servers:

   ```powershell
   hostname
   whoami
   dotnet --info
   ```

2. Confirm disk space for database backups and logs. Keep backup files on storage that will not be overwritten by the deployment.

3. Locate current application configuration. Capture the database provider and connection details used by the running application. ATSPM 5 currently expects database settings under:

   ```text
   DatabaseConfiguration:{ContextName}
   ```

   Confirm values for `ConfigContext`, `AggregationContext`, `EventLogContext`, and `IdentityContext`.

4. Capture current service status and running application version:

   ```powershell
   Get-Service *atspm*
   Get-Process dotnet -ErrorAction SilentlyContinue
   ```

5. Confirm database connectivity with the same network path that `DatabaseInstaller` will use.

6. Confirm no event log import, aggregation, report generation, or background job is expected to run during the migration window.

## 4. Capture Current Database State

Before making changes, record the currently applied EF migrations in every ATSPM database.

For SQL Server:

```sql
SELECT MigrationId, ProductVersion
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;
```

For PostgreSQL:

```sql
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";
```

Run the query against each database:

- Config database
- Aggregation database
- Event log database
- Identity database

Save the results with the upgrade notes. These records make it much easier to understand what changed if the upgrade needs troubleshooting later.

## 5. Stop ATSPM Services

Stop the application and background services before taking backups or applying migrations. The exact service names vary by site.

Common examples:

```powershell
Stop-Service <ATSPM-Web-Service-Name>
Stop-Service <ATSPM-ConfigApi-Service-Name>
Stop-Service <ATSPM-DataApi-Service-Name>
Stop-Service <ATSPM-ReportApi-Service-Name>
Stop-Service <ATSPM-IdentityApi-Service-Name>
Stop-Service <ATSPM-EventLogUtility-Service-Name>
Stop-Service <ATSPM-WatchDog-Service-Name>
```

If the site runs in Docker:

```powershell
docker compose down
```

Confirm the services are stopped before continuing. Do not apply migrations while the old application is still writing to the databases.

## 6. Take And Verify Backups

Take a full backup of all ATSPM databases after services are stopped.

SQL Server example:

```sql
BACKUP DATABASE [ATSPM_Config]
TO DISK = 'D:\ATSPM_Backups\ATSPM_Config_before_upgrade.bak'
WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;

BACKUP DATABASE [ATSPM_Aggregation]
TO DISK = 'D:\ATSPM_Backups\ATSPM_Aggregation_before_upgrade.bak'
WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;

BACKUP DATABASE [ATSPM_EventLog]
TO DISK = 'D:\ATSPM_Backups\ATSPM_EventLog_before_upgrade.bak'
WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;

BACKUP DATABASE [ATSPM_Identity]
TO DISK = 'D:\ATSPM_Backups\ATSPM_Identity_before_upgrade.bak'
WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;
```

Verify SQL Server backups:

```sql
RESTORE VERIFYONLY
FROM DISK = 'D:\ATSPM_Backups\ATSPM_Config_before_upgrade.bak'
WITH CHECKSUM;
```

Repeat verification for each backup file.

PostgreSQL example:

```powershell
pg_dump -h <db-host> -U <db-user> -Fc -d ATSPM-Config -f D:\ATSPM_Backups\ATSPM-Config-before-upgrade.dump
pg_dump -h <db-host> -U <db-user> -Fc -d ATSPM-Aggregation -f D:\ATSPM_Backups\ATSPM-Aggregation-before-upgrade.dump
pg_dump -h <db-host> -U <db-user> -Fc -d ATSPM-EventLogs -f D:\ATSPM_Backups\ATSPM-EventLogs-before-upgrade.dump
pg_dump -h <db-host> -U <db-user> -Fc -d ATSPM-Identity -f D:\ATSPM_Backups\ATSPM-Identity-before-upgrade.dump
```

Verify PostgreSQL backups by listing archive contents:

```powershell
pg_restore --list D:\ATSPM_Backups\ATSPM-Config-before-upgrade.dump
```

Do not continue until backups exist, verification succeeds, and the client agrees the backups are usable for rollback.

## 7. Stage The New ATSPM Package

Copy the target ATSPM package to the application server, but do not start the new services yet.

Recommended staging structure:

```text
C:\ATSPM\releases\<target-version>\
C:\ATSPM\current\
C:\ATSPM\backups\
C:\ATSPM\logs\
```

Before running migrations:

1. Confirm the staged package contains the target `DatabaseInstaller`.
2. Confirm `appsettings.json`, environment variables, user secrets, or service configuration contain the correct `DatabaseConfiguration` values.
3. Confirm JWT, CORS, path base, proxy, and certificate settings are ready for the newer application build.
4. Keep a copy of the previous application package until post-upgrade validation is complete.

## 8. Apply Database Migrations

Run the migration command from the staged target package. Use the database provider and connection strings for the client site.

SQL Server example:

```powershell
dotnet .\DatabaseInstaller.dll update `
  --provider SqlServer `
  --config-connection "Server=<db-server>;Database=ATSPM_Config;User Id=<user>;Password=<password>;TrustServerCertificate=True" `
  --aggregation-connection "Server=<db-server>;Database=ATSPM_Aggregation;User Id=<user>;Password=<password>;TrustServerCertificate=True" `
  --eventlog-connection "Server=<db-server>;Database=ATSPM_EventLog;User Id=<user>;Password=<password>;TrustServerCertificate=True" `
  --identity-connection "Server=<db-server>;Database=ATSPM_Identity;User Id=<user>;Password=<password>;TrustServerCertificate=True"
```

PostgreSQL example:

```powershell
dotnet .\DatabaseInstaller.dll update `
  --provider PostgreSQL `
  --config-connection "Host=<db-host>;Database=ATSPM-Config;Username=<user>;Password=<password>" `
  --aggregation-connection "Host=<db-host>;Database=ATSPM-Aggregation;Username=<user>;Password=<password>" `
  --eventlog-connection "Host=<db-host>;Database=ATSPM-EventLogs;Username=<user>;Password=<password>" `
  --identity-connection "Host=<db-host>;Database=ATSPM-Identity;Username=<user>;Password=<password>"
```

Only include admin seeding options if the upgrade plan explicitly calls for it:

```powershell
--seed-admin true `
--admin-email "<admin-email>" `
--admin-password "<temporary-password>" `
--admin-role "Admin"
```

Important migration rules:

- Run the command once for the site using the target build's `DatabaseInstaller`.
- Keep the full console output in the upgrade notes.
- If the command fails, stop. Do not restart services and do not manually edit migration history.
- If the failure is caused by a transient connectivity issue and no schema changes were applied, fix the issue and rerun only after confirming the database state.
- If the failure occurred after schema changes started, involve the project lead or DBA before retrying. Restoring from backup is usually safer than guessing.

## 9. Confirm Migration Results

After `DatabaseInstaller update` completes, query migration history again in all four databases.

For SQL Server:

```sql
SELECT MigrationId, ProductVersion
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;
```

For PostgreSQL:

```sql
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";
```

Compare the results with the pre-migration capture and the target build's migration files.

Also run a few basic row count checks to confirm the major data sets are still present. Adjust table names if the client database uses provider-specific casing or schema names.

SQL Server examples:

```sql
SELECT COUNT(*) AS Locations FROM Locations;
SELECT COUNT(*) AS Devices FROM Devices;
SELECT COUNT(*) AS CompressedEvents FROM CompressedEvents;
SELECT COUNT(*) AS AspNetUsers FROM AspNetUsers;
```

PostgreSQL examples:

```sql
SELECT COUNT(*) AS "Locations" FROM "Locations";
SELECT COUNT(*) AS "Devices" FROM "Devices";
SELECT COUNT(*) AS "CompressedEvents" FROM "CompressedEvents";
SELECT COUNT(*) AS "AspNetUsers" FROM "AspNetUsers";
```

The goal is not to prove every record by hand. The goal is to catch obvious mistakes before the new application is started.

## 10. Deploy And Start The New Application

After migrations are confirmed:

1. Switch the application to the new release package.
2. Confirm runtime configuration points to the migrated databases.
3. Start ATSPM services.

Windows service examples:

```powershell
Start-Service <ATSPM-IdentityApi-Service-Name>
Start-Service <ATSPM-ConfigApi-Service-Name>
Start-Service <ATSPM-DataApi-Service-Name>
Start-Service <ATSPM-ReportApi-Service-Name>
Start-Service <ATSPM-Web-Service-Name>
Start-Service <ATSPM-EventLogUtility-Service-Name>
Start-Service <ATSPM-WatchDog-Service-Name>
```

Docker example:

```powershell
docker compose up -d
```

Watch logs during startup. Stop and investigate if any service reports missing settings, pending migrations, authentication failures, database login failures, or provider mismatch errors.

## 11. Post-Migration Smoke Test

Run this validation before releasing the site back to the client.

1. Sign in with an existing client account.
2. Confirm the application loads without client-side runtime configuration errors.
3. Open the location map and confirm locations render.
4. Open admin/configuration pages for locations, devices, products, areas, regions, jurisdictions, roles, menu items, and FAQ.
5. Run one report that uses configuration data.
6. Run one report that uses event log data for a known location and date.
7. Confirm current or recent event logs are still being imported, if the site uses EventLogUtility.
8. Confirm background jobs or WatchDog services are healthy.
9. Confirm application logs do not contain repeated database, auth, CORS, or migration errors.
10. Ask the client contact to perform one site-specific workflow before the maintenance window closes.

Record the validation result:

```text
Validated by:
Validation time:
Known issues:
Client approval:
```

## 12. Rollback Procedure

Rollback is required if migrations fail in a way that cannot be corrected during the maintenance window, or if post-migration validation finds a blocking issue.

1. Stop all ATSPM services.
2. Restore each database from the pre-migration backup.
3. Redeploy or re-point services to the previous ATSPM application package.
4. Start services.
5. Run the smoke test against the restored previous version.
6. Preserve failed migration logs and database error messages for follow-up analysis.

SQL Server restore pattern:

```sql
ALTER DATABASE [ATSPM_Config] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [ATSPM_Config]
FROM DISK = 'D:\ATSPM_Backups\ATSPM_Config_before_upgrade.bak'
WITH REPLACE, RECOVERY;
ALTER DATABASE [ATSPM_Config] SET MULTI_USER;
```

Repeat for `ATSPM_Aggregation`, `ATSPM_EventLog`, and `ATSPM_Identity`.

PostgreSQL restore pattern:

```powershell
dropdb -h <db-host> -U <db-user> ATSPM-Config
createdb -h <db-host> -U <db-user> ATSPM-Config
pg_restore -h <db-host> -U <db-user> -d ATSPM-Config D:\ATSPM_Backups\ATSPM-Config-before-upgrade.dump
```

Repeat for `ATSPM-Aggregation`, `ATSPM-EventLogs`, and `ATSPM-Identity`.

Do not attempt a rollback by deleting rows from `__EFMigrationsHistory`. Always restore the databases from backup unless the project lead and DBA approve a different recovery plan.

## 13. Closeout

Before leaving the site:

1. Save the migration output, backup verification output, pre/post migration history, and smoke test notes.
2. Confirm backups are retained according to the client retention policy.
3. Confirm the previous application package is retained until the client accepts the upgrade.
4. Confirm monitoring, scheduled jobs, and event log import are running.
5. Send the client a short upgrade summary with start time, end time, target version, validation results, and any follow-up items.

## 14. Field Checklist

Use this condensed checklist during the maintenance window.

1. Confirm server, version, package, credentials, and maintenance window.
2. Stop ATSPM services.
3. Capture current `__EFMigrationsHistory` for all four databases.
4. Back up all ATSPM databases.
5. Verify backups.
6. Stage the target ATSPM package.
7. Confirm runtime database configuration.
8. Run `DatabaseInstaller update`.
9. Capture post-migration `__EFMigrationsHistory`.
10. Run basic row count checks.
11. Start the new ATSPM services.
12. Smoke test login, maps, admin/configuration pages, reports, event logs, and background jobs.
13. Get client approval.
14. Save logs and closeout notes.

