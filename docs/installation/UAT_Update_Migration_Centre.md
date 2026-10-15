# Update hosted UAT for Migration Centre

Prepared 6 October 2026 for `https://nexus.garnetnation.lk/`. This updates code/schema; it does not import legacy balances. The browser reached **Sign In** on the hosted site. Authenticated hosted acceptance remains **Needs business confirmation** after deployment.

## Release and recovery

The new feature is `/Migration`, **Administration → Settings & Security → Migration Centre**. The earlier `1.0.6-uat.customer-opening` artifact does **not** contain this wizard. Build/deploy the current implementation as a new release. Existing local BOM, raw-material and deployment changes are also present in the workspace; a local publish includes them. Review the whole release, not just the migration files.

1. Confirm the UAT IIS site, application pool, physical directory, instance ID/root and database. The saved publish profile names `Y:\NexusFlowERPUAT`; do not assume this mapped path is the correct site. These server details are **Needs business confirmation**.
2. Arrange a maintenance window. Back up the SQL database and verify restoration; also back up published files, configuration, installation state, secrets, data-protection keys and uploaded-file storage outside the site directory.
3. Keep the existing app-pool identity and instance settings. Preserve hosted `web.config`, all `appsettings*.json`, environment settings, protected instance secrets and storage. Do not reinstall the tenant or use a mirroring copy that removes those files.
4. Confirm the existing .NET 10 IIS hosting/runtime setup. A framework-dependent publish does not install it. Use the existing release process after committing/reviewing this change; do not label a local dirty-workspace build as a verified CI/main release.

Local publish command, using a **new** output directory:

```powershell
dotnet publish NexusFlow.Web/NexusFlow.Web.csproj -c Release --no-restore -o artifacts/uat/migration-centre/application-build
```

For a manual review kit, package only application binaries/dependencies/static assets/default templates; exclude installation secrets, `appsettings*.json`, `web.config`, PDBs, user exports, uploads and test hosts. Use a generated web.config only as a reference to the existing IIS setup. Record SHA-256 checksums and the actual source state. The repository's Garnet CI release scripts require real commit/run provenance; use them with actual CI values for the normal hosted release.

## Schema upgrade

The required additional migration is **`20261006160821_AddMigrationCentre`**. It follows the customer opening migrations:

- `20261005011038_AddCustomerOpeningMigration`
- `20261005014028_HardenCustomerMigrationControls`

It adds retained migration projects/datasets/scopes/runs/origins/audit, supplier opening/credit records, pending bank items and links, incomplete-detail flags and supplier legacy codes. It widens stock unit costs to six decimals while retaining the former integer capacity (`decimal(22,6)`); monetary journal amounts remain cents. Supplier tax uniqueness applies only to supplied tax numbers. Customer opening/cheque audit history remains intact.

1. Inspect `__EFMigrationsHistory` on the confirmed UAT database. Rehearse upgrading a restored copy before touching the hosted instance. Fix any existing data violations with an authorized correction; do not alter migration-history rows to skip checks.
2. Stop the application pool while copying the reviewed published application files. Preserve the configuration/state/storage listed above. Start the app in its controlled upgrade mode.
3. Open `/maintenance/upgrade` as the installation operator. Confirm the recovery backup, inspect the pending migration list and select **Run controlled upgrade**. Restart/recycle after success so the installed application's background services, including Hangfire, initialize against the upgraded database. Do not run this upgrade concurrently with manual SQL.
4. If using a reviewed SQL deployment instead, generate a script from the **verified actual baseline** to the latest migration and execute it only against that confirmed UAT database during maintenance. The example below assumes baseline `20260622171142_FinancialControlsDocumentsAndProduction` is already present; it is not a universal script for earlier databases.

```powershell
dotnet ef migrations script 20260622171142_FinancialControlsDocumentsAndProduction 20261006160821_AddMigrationCentre --idempotent --project NexusFlow.Infrastructure --startup-project NexusFlow.Web --configuration Release -o docs/migration/Migration_Centre_Upgrade.sql
```

The automated upgrade checks rehearse temporal-table preservation, the combined forward upgrade and repeated idempotent execution on disposable SQL Server databases. They do not establish compatibility with unknown tenant-specific alterations. Do not run Down migrations once business imports exist; use the verified recovery point if rollback is required.

## Hosted acceptance

1. Sign in with `SuperAdmin`, or an explicitly authorized account with `Permissions.Migration.View`, `.Prepare`, `.Approve`, `.Execute` and `.Finalize`. `Admin`'s default role manifest includes all permissions, but confirm the hosted account's actual claims. Do not grant migration permissions to ordinary sales/store users.
2. Confirm **Migration Centre** is visible in Administration. Confirm old opening-import page routes redirect to it. Open **Download Template Pack** and verify twelve CSV templates.
3. Confirm Hangfire is running and can process the `default` queue. Check the existing secure file storage can retain and retrieve originals. A disabled/stopped worker leaves a queued run pending; it does not post in the request.
4. On a clean, disposable UAT target use the MC cases in [UAT_Test_Cases.md](../uat/UAT_Test_Cases.md), including sparse masters, selected imports, negative credits, original FIFO layers, pending received/issued cheques, failure/resume, release locks and final GL. Existing training journals must not receive another opening import.
5. Verify normal receipts/supplier payments and stock consumption against released scopes. Verify pending scopes are blocked at the backend, report/export provisional notices persist until finalization, and final sign-off makes the project read-only.
6. Record pass/fail, screenshots, run/unit/journal IDs and business approval. Only then schedule the actual freeze and load newly exported closing data through the wizard. The supplied September files remain structural references.

Feature details and exact file meaning: [Migration Centre operating guide](../migration/Migration_Centre_Runbook.md). Local evidence: [Migration Centre verification](../uat/Migration_Centre_Verification.md).
