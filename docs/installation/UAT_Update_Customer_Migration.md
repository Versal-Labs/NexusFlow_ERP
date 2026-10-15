# Update the hosted UAT customer migration release

> Historical customer-import release: the current implementation uses [Migration Centre](../migration/Migration_Centre_Runbook.md) and [its UAT update steps](../installation/UAT_Update_Migration_Centre.md). Preserve old audit history; use the central workflow for new cutover work.

Prepared 5 October 2026. Site: https://nexus.garnetnation.lk/. This guide updates the application and schema; it does not load customer balances.

The repository's **Nexus Flow UAT** publish profile uses FileSystem publishing to `Y:\NexusFlowERPUAT`, Release configuration, `net10.0`, framework-dependent. Confirm that this mapped directory belongs to this UAT website before copying. IIS site name, application pool name, physical server path, UAT database name and last applied migration are **Needs business confirmation**. The hosted site previously reached Sign In; server configuration and authenticated hosted UAT have not been inspected.

## Release contents

The delivered release-kit ZIP contains `application.zip`, `database/01-Check-UAT.sql`, `database/02-Customer-Migration-Upgrade.sql`, `web.config.reference`, a release manifest, file hashes and these instructions. The application ZIP contains published binaries, dependencies, static assets and default document templates. It excludes all `appsettings*.json`, `web.config`, PDB files, source exports, test hosts and installation secrets.

Preserve the hosted `web.config`, `appsettings*.json`, IIS/environment settings, installation-state/secrets, data-protection keys, uploads and storage. `web.config.reference` is a clean generated reference, not a replacement for your site's configuration. The package requires the existing compatible .NET 10 IIS hosting setup.

This is a snapshot of the current working directory, including existing local BOM and raw-material import changes alongside the customer migration. The release manifest records the base commit and modified/untracked source paths. Review the full release before deployment. It is not a customer-only patch or a release from a clean Git commit.

## 1. Prepare a recovery point

1. Arrange a UAT maintenance window and stop users from entering transactions. Confirm that the selected website and database are UAT, not production.
2. Back up the UAT SQL database and verify it can be restored. Also back up the current published website, its configuration and the existing instance state/storage. Keep backups outside the web directory.
3. Record the IIS website, application pool, physical path, instance ID/root and UAT database name. Keep the same app-pool identity and instance settings: DPAPI-encrypted secrets may depend on them. Do not regenerate setup keys or reinstall the tenant for a routine update.
4. Confirm the server has the .NET 10 Hosting Bundle and `AspNetCoreModuleV2`. This package does not install the hosting bundle. Check with `dotnet --list-runtimes` on the server. [Microsoft IIS hosting guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0).
5. Extract the release kit to a staging directory outside the website. Verify the ZIP SHA-256 against the supplied checksum file, then verify the staged files against `FILES.sha256`.

## 2. Check the database without changing it

In SQL Server Management Studio, select the confirmed UAT database. Edit only `@ExpectedDatabase` in `database/01-Check-UAT.sql` to that exact name and run it. This script is read-only and reports the current database, migration history and rows that would violate the new constraints.

The packaged customer SQL upgrade requires baseline `20260622171142_FinancialControlsDocumentsAndProduction` already applied. It brings that schema to:

- `20261005011038_AddCustomerOpeningMigration`
- `20261005014028_HardenCustomerMigrationControls`

If the baseline is missing, unexpected later migrations are present, or constraint checks fail, stop before deploying. Review the older pending migrations and prepare the correct upgrade from the actual database state. Do not edit migration-history rows, delete invalid records or drop/recreate the database to get past a failure.

## 3. Replace the application files

1. Confirm `Y:\NexusFlowERPUAT` maps to the UAT physical site path. Work directly on the server path if a mapped drive is unavailable.
2. Put `app_offline.htm` in that existing site's root with a short UAT maintenance message. IIS uses this file to shut down the ASP.NET Core app and serve the maintenance page. If files remain locked, stop only the confirmed UAT application pool in IIS Manager. [Microsoft deployment guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0).
3. Extract `application.zip` into a staging folder first. Copy its **contents** into the UAT site's root, replacing published binaries and assets. Avoid an extra `application` folder under the web root. Use a copy/update operation, not a mirror/delete operation that could erase server-only configuration or uploaded files.
4. Keep the existing `web.config` and application settings. Confirm its `aspNetCore` launch entry remains compatible with `NexusFlow.Web.dll` or the packaged Windows apphost. Retain every existing environment variable, IIS binding, SSL setting and instance path. Merge a required launch-entry change carefully using `web.config.reference`; never replace the entire server file blindly.
5. Keep `app_offline.htm` in place until the copy is complete. Then start the UAT app pool if stopped and remove the offline file.

Do not run `Deploy-NexusFlow.ps1` or `/install` for this update: the deployment script also provisions an instance and setup key. Do not publish directly with the existing Visual Studio profile for this prepared package: that can overwrite configuration from the development machine.

## 4. Apply the controlled schema upgrade

Use **one** of these paths after the successful database preflight.

### Recommended: the existing maintenance screen

1. Open https://nexus.garnetnation.lk/maintenance/upgrade and sign in with an authorized existing account. The controller accepts SuperAdmin, legacy Admin or the protected SuperAdmin permission; server setup authorization is another supported path.
2. Check **Pending migrations**. For a database at the stated baseline, the two migration IDs above should be listed. If other migrations appear, review them before continuing; the button applies all pending migrations.
3. Check **I confirm that a current, restorable database backup exists.** Select **Run controlled upgrade**.
4. The application applies migrations, checks readiness and restarts on success. Wait for the upgrade result, then sign in again. A failed readiness check keeps the ERP in maintenance mode; review logs and fix the named issue instead of changing installation-state JSON manually.

### Alternative: reviewed SQL deployment

If your DBA requires SQL scripts, keep the UAT application stopped/offline and execute `database/02-Customer-Migration-Upgrade.sql` against the preflight-verified UAT database using SSMS. Enable stop-on-error behavior; in SQLCMD Mode use `:ON ERROR EXIT` before the script. The script checks the baseline and only applies missing customer migrations. It is not a new-database installer and does not import financial balances. Review it before execution. [Microsoft EF migration deployment guidance](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).

Restart the app after SQL succeeds. If `/maintenance/upgrade` still appears, use **Run controlled upgrade** with the backup confirmation to complete the readiness/state transition; an empty pending list means the SQL migrations are already applied. Do not execute both methods simultaneously or run schema changes while customer posting is in progress.

## 5. Verify the hosted update

1. Open https://nexus.garnetnation.lk/health/live and https://nexus.garnetnation.lk/health/ready. Expect HTTP 200 and `live` / `ready`. HTTP 503 on readiness is a deployment failure to resolve before releasing UAT to users.
2. Confirm the two migration IDs exist in `dbo.__EFMigrationsHistory`. No unrelated database should have been modified.
3. Sign in and hard-refresh the browser. Open **Accounting & GL → Customer Opening Migration**, or https://nexus.garnetnation.lk/Finance/CustomerMigration. Check the master CSV template, both report inputs, saved batches and preview controls.
4. Confirm posting is disabled: `/api/finance/customer-migration/setup` should report `postingEnabled: false` for an authorized session. If this UAT database was previously enabled, choose **Disable Migration Posting** on the page. Do not enable it merely to verify the deployment.
5. Verify the migration operator has both ManageAccounts and ManageCheques, plus ManageCustomers for CSV import. Check restricted users cannot open the page/API.
6. Smoke-test Customer Master, Customer Receipts, Cheque Vault (PDCs), Customer Statement, AR Aging and PDF/Excel exports. The release also includes the existing workspace changes; include their affected workflows in UAT.
7. Run the customer acceptance cases in [UAT_Test_Cases.md](../uat/UAT_Test_Cases.md). Use synthetic data in a disposable test database for posting/bounce/reversal cases. Do not sign off a synthetic batch in the actual migration database: sign-off locks later migration posting.

## 6. Load approved opening data later

After hosted UAT is approved, follow [Customer Opening Migration Runbook](../migration/Customer_Opening_Migration_Runbook.md). Prepare all customer legacy codes, verified branches and control-account mappings first. For a 1 October 2026 cutover, use both refreshed closing reports from 30 September. The attached 28 September reports are parser/test evidence and are deliberately rejected for that cutover. Enabling posting, posting balances and final sign-off are separate business actions from this deployment.

## Recovery

If the file update fails before schema changes, restore the previous site files/configuration and restart only the UAT pool. If schema changes have run, or readiness fails afterward, keep users blocked and use a reviewed forward repair or restore the coordinated pre-update application, database and instance-state backup. Do not blindly roll back only the DLLs, run EF Down migrations or delete customer opening records after transactions have been posted. Record any UAT work lost when restoring a database.

## Local release evidence

The corrected 1.0.6 release records 74 passing tests and 3 separately opt-in stock SQL tests skipped, including a regression check for application service registration. The MVC harness now uses `AddAppCore`, and its page setup/controller activation and migration workflow checks pass. Release publish succeeds with no pending EF model changes. The packaged SQL is unchanged from the previously rehearsed upgrade, which was executed twice on a disposable database. Exact build/check results and SHA-256 hashes are included in the release kit. Hosted deployment and authenticated business UAT remain pending.

Version 1.0.5 is superseded because it omitted the customer master import service registration. If its two customer migrations are already applied, deploy the corrected 1.0.6 application files and restart the UAT application pool; this correction requires no additional database migration. Preserve the hosted configuration, keys and storage as described above, then verify `/Finance/CustomerMigration` and `/api/finance/customer-migration/setup` under an authorized session.
