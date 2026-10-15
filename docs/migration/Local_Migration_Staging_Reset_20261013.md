# Local migration staging reset - 13 October 2026

The user requested a clean slate for uploading the new UAT migration files. The reset was limited to NexusERPLocal on SAKEELPC. No hosted database was changed.

Project test (ID 3, SourceSystem TESTTTT, Preparing) contained CustomerMaster.csv and CustomerOpenings.csv. No migration units, origins, runs, journals, opening items, cheques, stock layers or stock transactions were present before clearing.

A fresh native SQL Server COPY_ONLY backup with checksums was created, RESTORE VERIFYONLY passed, and the workspace copy matched its SHA256. A rollback rehearsal passed before the committed transaction. The reset acquired the application migration locks, refused posted/active data, and checked exact fingerprints of all 170 tables outside Migration and HangFire.

## Result

| Table | Before | After |
|---|---:|---:|
| Migration.MigrationAudit | 7 | 0 |
| Migration.MigrationDatasets | 2 | 0 |
| Migration.MigrationOrigins | 0 | 0 |
| Migration.MigrationProjects | 1 | 0 |
| Migration.MigrationRuns | 0 | 0 |
| Migration.MigrationScopes | 896 | 0 |
| Migration.MigrationUnits | 0 | 0 |
| Migration.MigrationAudit_History | 0 | 0 |
| Migration.MigrationDatasets_History | 0 | 0 |
| Migration.MigrationOrigins_History | 0 | 0 |
| Migration.MigrationProjects_History | 6 | 0 |
| Migration.MigrationRuns_History | 0 | 0 |
| Migration.MigrationScopes_History | 0 | 0 |
| Migration.MigrationUnits_History | 0 | 0 |

All seven current migration tables and seven history tables are empty. System versioning remains enabled on all seven current tables with the same history-table configuration. Independent post-reset checks found no disabled or untrusted migration foreign keys.

Exact row counts and SHA256 fingerprints of all 170 preserved business tables matched before and after. This includes customer/supplier/product/account masters, account and stock records, settings, number sequences and business histories. HangFire runtime tables were not modified. Physical uploaded files, legacy originals and the prepared UAT upload bundle remain unchanged. The two customers and four suppliers are preserved; journal count and nonzero account balances remain zero. Application source code was not changed.

## Start the new upload

1. Refresh the local Migration Centre (/Migration). The old project 3 no longer exists.
2. Select New Project. Enter a project name, SourceSystem TESTTTT and Actual go-live opening date 2026-10-01. Closing is 2026-09-30. TESTTTT must match the supplied ZIP manifests; individual CSVs may be used for another source identifier.
3. Stage 01_Masters.zip, save mappings/defaults again, validate, approve and import masters. Old project mappings/defaults were removed with its settings.
4. Follow [the complete UAT guide](uat_upload_20261012/UAT_Complete_Guide.md) for opening schedules, combined Stock, controls, scope release and the trial balance. Import execution remains an operator action; no new project or imports were executed by this reset.

## Recovery and verification

- [Native database backup](../../artifacts/local-migration-staging-reset-20261013/NexusERPLocal_before_staging_reset_20261013-194708_ee0d18a3.bak)
- SHA256: 5B80E81191017E3FEB8EB13DB5C19F768257C1EC9FD3AEB9B70CA322389B7361
- [Backup manifest](../../artifacts/local-migration-staging-reset-20261013/backup-manifest.json)
- [Rollback rehearsal](../../artifacts/local-migration-staging-reset-20261013/rehearsal-report.json)
- [Committed reset report](../../artifacts/local-migration-staging-reset-20261013/reset-report.json)
- [Independent post-reset verification](../../artifacts/local-migration-staging-reset-20261013/post-reset-verification.json)

Restoring the backup is a separate recovery operation requiring explicit authorization. It was not performed.
