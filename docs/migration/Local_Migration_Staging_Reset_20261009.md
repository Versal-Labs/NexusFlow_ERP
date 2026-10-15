# Local migration staging reset — 9 October 2026

The user requested a fresh start in the local Migration Centre by removing all local migration data. This operation was limited to `NexusERPLocal` on `SAKEELPC`. No hosted environment was changed.

## Result

The reset committed at 06:58:02 Asia/Colombo on 9 October 2026. The local project `test` was in `Preparing` status, with no posted migration units, origins, runs, journals, cheques or stock positions.

Removed staging data:

| Records | Before | After |
| --- | ---: | ---: |
| Migration projects | 1 | 0 |
| Uploaded dataset records | 3 | 0 |
| Migration scopes | 1,599 | 0 |
| Migration audit records | 5 | 0 |
| Migration project history | 4 | 0 |
| Migration units, origins and runs | 0 | 0 |

The three datasets were CustomerMaster (`customermasterfile.xls`), Stock (`stock.csv`) and SupplierMaster (`supliermasterfile.xls`). All seven current tables and seven history tables in the Migration schema are empty. Original uploaded files remain on disk for recovery; their database staging records were removed.

## Preserved data and verification

SHA-256 fingerprints and row counts for all 170 tables outside the Migration and HangFire schemas matched before and after the operation. This includes master data, settings, mappings, number sequences, financial and inventory records, and their histories. HangFire runtime tables were not modified by the reset.

Preserved current master counts include 2 customers, 4 suppliers, 55 accounts, 795 products, 1,055 product variants, 40 system configuration records, 24 system lookups and 24 number sequences. Journals, cheques, stock layers and stock movements remain empty; every account balance remains zero.

A rollback rehearsal passed before committing. The operation used a transaction and the application's migration locks. All seven migration tables retain system versioning with their original history tables. An independent post-reset query confirmed zero projects and datasets, zero journals and nonzero account balances, and no disabled or untrusted migration foreign keys.

## Backup and reports

A native SQL Server `COPY_ONLY` backup with checksums was created, verified with `RESTORE VERIFYONLY`, copied into the workspace and checked for identical SHA-256 hashes before the reset.

- Backup: `artifacts/local-migration-staging-reset-20261009/NexusERPLocal_before_staging_reset_20261009-065649_2c381482.bak`
- SHA-256: `ED5F826D12E9821A2CE6C706406D436229F422CBBA5FB481955914328140AD5F`
- Metadata: `artifacts/local-migration-staging-reset-20261009/backup-manifest.json`
- Rehearsal: `artifacts/local-migration-staging-reset-20261009/rehearsal-report.json`
- Committed verification: `artifacts/local-migration-staging-reset-20261009/reset-report.json`

## Starting again

Refresh the local Migration Centre, return to its project list and choose **New Project**. The old project's page will no longer resolve because the project was removed. Upload the datasets into the new project.

The current application supports replacement upload revisions but does not expose dataset deletion or project discard actions. Those controls have not been implemented. A future discard action should distinguish unposted staging data from posted openings and refuse to remove posted balances through staging deletion.

No application source code or UI controls were changed by this reset. Verification used the local database and reports; it did not include a browser walkthrough.

## Repeat reset for corrected customer CSV

The user requested another local migration reset after correcting the customer CSV. Project 2 (`test`, Preparing) had one CustomerMaster upload, 892 scopes, 3 audit records and 2 project-history rows. There were no posted units, origins, active runs, journals, cheques or stock positions.

A fresh native database backup with checksums was created and verified before a successful rollback rehearsal and committed reset. All 14 migration current/history tables are now empty. Exact fingerprints and counts of the 170 preserved tables matched before and after. An independent query confirmed no projects, datasets, journals or nonzero account balances, and all 7 migration current tables still system-versioned. Original physical upload files remain for recovery; master data, settings and mappings outside the removed migration project were preserved. No application source was changed.

Repeat-reset recovery artifacts are in `artifacts/local-migration-staging-reset-20261009-retry2/`:

- Backup: `NexusERPLocal_before_staging_reset_20261009-085816_215e422d.bak`
- SHA-256: `E6B4790BB2B89882F328223ED10DE04E014BEEC6497E3FA5635697A4D35964AE`
- `backup-manifest.json`, `rehearsal-report.json`, and `reset-report.json`

Refresh the local Migration Centre and choose **New Project**, then upload the corrected CSV. Configure and save mappings/defaults for the new project before validation. Previous project settings are removed with the project.
