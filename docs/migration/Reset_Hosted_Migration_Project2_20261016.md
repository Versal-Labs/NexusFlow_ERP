# Start a new hosted migration project

For the 18 October fresh-start request after the history purge, use the newer [combined reset SQL](Reset_Migration_For_Fresh_Start_20261018.sql) and [instructions](Reset_Migration_For_Fresh_Start_20261018.md). That version clears current migration records and their histories together.

Requested on 16 October 2026 after project **Data Migrate**, ID 2, run 1 remained Running with 500 committed master units and its Hangfire job 44 was missing. This supersedes the earlier plan to recover and resume that run.

Updated on 18 October 2026 at the user's request: the script requires neither a backup nor a backup reference. Worker-stop confirmations, the data checks and the rehearsal/hash checks still apply.

**This reset has not been applied to the hosted database. The user will execute it in SSMS.**

Use [Reset_Hosted_Migration_Project2_20261016.sql](Reset_Hosted_Migration_Project2_20261016.sql) against **versalapps.database.windows.net / NexusGarnetProd**. Do not use the older production test-transaction reset: that script targets a different data position and a wider set of business transactions.

The reset removes all seven current migration tables: projects (including mappings/defaults/evidence), datasets, scopes, runs, units, origins and migration audit. It also removes customer/account masters created by this import when their origins, timestamps, creator and unchanged state prove ownership. It preserves masters that existed before the run. The Accounts unit can link existing accounts, so an origin alone is not permission to delete an account.

The script stops if it finds financial openings/journals, later edits to imported masters, retained business rows depending on those masters, another project/run, or a different import position. It does not expand deletion into sales, purchasing, stock, treasury, HR, users or setup. Temporal histories, original uploads/source files, backups and unrelated Hangfire jobs remain available. Those histories do not prevent creation of a new project.

The user subsequently requested removal of the retained migration history copies as well. After this current-project reset completes, run [Purge_Migration_History_20261018.sql](Purge_Migration_History_20261018.sql) using [its instructions](Purge_Migration_History_20261018.md). Run that purge last so the reset's deletions do not leave new migration history behind.

## SSMS steps

1. Connect to **NexusGarnetProd** on **versalapps.database.windows.net** and open the complete SQL file. Leave `@Mode='PREVIEW'` and execute the whole file. Review its table counts and the proposed counts of newly imported customers/accounts. PREVIEW does not delete anything.
2. Confirm exact Hangfire job 44 is still missing. Stop every site, IIS application pool, worker service and integration connected to this installation; confirm their processes have exited. Keep them stopped through rehearsal and commit. The empty Hangfire Processing list alone is insufficient because the interrupted code does not use worker cancellation.
3. Set these values at the top of the SQL file:

   ```sql
   DECLARE @Mode varchar(10) = 'REHEARSE';
   DECLARE @AllWritersStopped bit = 1;
   DECLARE @Job44ConfirmedMissing bit = 1;
   ```

4. Run the **whole file**. Require **REHEARSED_ROLLED_BACK**. Save its **SnapshotHash**. The original data should still be present because rehearsal rolls the reset back. If a check fails, retain the exact error and stop; do not remove the guard.
5. Change `@Mode` to **COMMIT**, paste the successful rehearsal's hash into `@RehearsalHash`, and retain the worker/job confirmations. Run the **whole file** once. Require **COMMITTED**. Changed data invalidates the rehearsal hash.
6. In a new query session, verify:

   ```sql
   SELECT 'Projects' AS Item, COUNT_BIG(*) AS RowsRemaining FROM Migration.MigrationProjects
   UNION ALL SELECT 'Datasets', COUNT_BIG(*) FROM Migration.MigrationDatasets
   UNION ALL SELECT 'Scopes', COUNT_BIG(*) FROM Migration.MigrationScopes
   UNION ALL SELECT 'Runs', COUNT_BIG(*) FROM Migration.MigrationRuns
   UNION ALL SELECT 'Units', COUNT_BIG(*) FROM Migration.MigrationUnits
   UNION ALL SELECT 'Origins', COUNT_BIG(*) FROM Migration.MigrationOrigins
   UNION ALL SELECT 'Migration audit', COUNT_BIG(*) FROM Migration.MigrationAudit;
   ```

   All seven counts must be zero. Retain the result sets. If a query is interrupted, check `SELECT @@TRANCOUNT;` in that same session and roll back any open transaction before continuing.

7. Restart the hosting services. Open **Settings & Security → Migration Centre**, select **Refresh Progress**, then **New Project**. The old project should no longer appear. Configure the new project's source identifier, dates, uploads, mappings and approvals again. Do not assume its new database ID will be 2.

## Verification

The SQL passed execution tests against a uniquely named disposable SQL LocalDB database built from the current application model. Synthetic data included 500 posted units, a customer master reused from before the run, 498 newly imported customers, an existing account and two newly imported parent/child accounts. The test database was removed after verification. No hosted database or existing local application database was contacted by these tests.

| Check | Result |
| --- | --- |
| Production target guards reject a different database/server | Pass |
| Worker-stop confirmation is required for rehearsal/commit | Pass |
| PREVIEW leaves all application rows intact | Pass |
| Later imported-master edits block reset | Pass |
| Retained business dependencies block deletion without cascading | Pass |
| REHEARSE restores the project, 500 units and all masters by rollback | Pass |
| Incorrect hash or changed migration data blocks COMMIT | Pass |
| COMMIT clears all seven current migration tables and removes only import-created masters, including account children before parents | Pass |
| Reused masters, settings, suppliers and temporal history remain | Pass |
| Repeating the reset rejects the cleared baseline | Pass |

Test harness and output: `artifacts/hosted-migration-reset-20261016/`. The original 500-unit verification passed on 16 October. The build passed with zero warnings and zero errors. These checks validate the source model and representative data; they do not replace rehearsal on the actual hosted database.

On 18 October, the version without the backup-reference requirement passed the complete checks with a compact disposable fixture (three units, including reused and newly imported masters). Only the private test copy substitutes its fixture target and counts; the delivered SQL retains the actual hosted target and 500-unit baseline. Output is in `verification-no-backup-20261018.txt`. The full 500-unit rerun reached the client command timeout of 180 seconds during rehearsal, recorded in `test-results.txt`. For SSMS, allow sufficient query execution time (for example, execution timeout 0 for no client time limit), keep writers stopped, and require the actual hosted rehearsal to finish successfully before COMMIT. No hosted data was changed by either test.

No application source changes or hosted database execution were performed when preparing these files.
