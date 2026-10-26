# Migration operations: local review and additive rollout

Prepared 2026-10-21. This document is an operator review procedure. No hosted deployment or SQL execution is authorized/performed in this task.

## Artifacts and prerequisites

- EF migration: `20261021032811_AddMigrationOperations` after `20261008142956_latest`.
- Reviewed incremental/idempotent SQL: [Migration_Operations_Upgrade.sql](Migration_Operations_Upgrade.sql).
- Deploy the matching Web application, `Views/Migration/Index.cshtml` and `wwwroot/js/migration/centre.js` together.
- Existing original-file storage, migration permissions and Hangfire storage/workers must remain configured.
- Perform the upgrade while migration workers and other application writers are idle; resume services after schema and application versions match.

The script adds six non-temporal tables, foreign keys and indexes. It does not alter MigrationRuns, change its actual temporal setting, reenable versioning, delete history, rewrite old payloads, reset data or reopen finalized projects. Existing posted data, sources, released scopes and final evidence are preserved. Both temporal-on and temporal-off legacy MigrationRuns are covered by disposable upgrade tests. Do not downgrade/drop the new tables after operations have been recorded.

## Local verification

From the repository root in PowerShell:

```powershell
$env:NEXUSFLOW_RUN_LOCALDB_CUSTOMER_TESTS = '1'
dotnet test NexusFlow.IntegrationTests/NexusFlow.IntegrationTests.csproj -m:1 /p:UseSharedCompilation=false --filter 'FullyQualifiedName~MigrationCentre'
$env:NEXUSFLOW_MIGRATION_SMOKE_SEED = '1'
dotnet run --project tools/MigrationCentreSmokeHost --no-launch-profile
```

The harness uses an owned GUID-named disposable LocalDB database and loopback `http://127.0.0.1:5191/smoke-login`. Run `tools/MigrationCentreSmokeHost/VerifyOperations.ps1` against a fresh seeded host for the repeatable HTTP checks. Use `/smoke-viewport?width=375` (also 768/1024/1440) for browser viewport spot checks. Use its restricted-login query modes for permission checks; POST `/smoke-stop` ends the harness and removes its owned database. It does not load hosted connection configuration. Fixture and acceptance instructions are in [UAT Test Data](../uat/UAT_Test_Data.md) and [background operation guide](../migration/Migration_Centre_Background_Operations.md).

To regenerate the review script locally without connecting to a database:

```powershell
dotnet ef migrations script 20261008142956_latest 20261021032811_AddMigrationOperations --idempotent --project NexusFlow.Infrastructure --startup-project NexusFlow.Infrastructure --output docs/installation/Migration_Operations_Upgrade.sql
```

## Authorized future rollout procedure

1. Confirm the installed baseline and idle workers/writers. Review the generated incremental SQL and target identity. Treat the finalized hosted project as a read-only reference.
2. Use the established installer upgrade path, or an authorized operator applies the reviewed incremental script once. Do not run an unrelated baseline/reset/quota-recovery script for this feature.
3. Install matching application/static assets, restart workers and application, and verify existing finalized data is still read-only. The new dispatcher checks unsent queued operations every 30 seconds when production services are enabled.
4. Use a separate disposable UAT project/environment for the write acceptance cases. Verify 202 acceptance, reconnect, paging, controls, roles and safe recovery. Keep financial cutover evidence and UAT results separate from automated regression results.
5. Record authenticated hosted acceptance before allowing a future live migration operation. Current hosted project 3 is already finalized and must not be replayed.

Current verification status and any browser limitations are recorded in [Migration Operations Verification](../uat/Migration_Operations_Verification.md).
