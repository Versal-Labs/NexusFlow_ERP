# Recover project 3 / run 2 after the Azure SQL space quota

## Confirmed from the user's SSMS and Hangfire results

The user supplied these results on 20 October 2026. They establish the space consumer and failure; the assistant has not connected to or changed the hosted database.

| Item | Supplied value |
| --- | --- |
| Database | NexusGarnetProd, versalapps.database.windows.net |
| Diagnostic capture | 2026-10-20 05:50:11.8579450 UTC |
| Data file allocated / used / free | 2,048.00 / 2,045.31 / 2.69 MB |
| MigrationRuns_History | 502 versions; 1,984.55 MB used, including 1,984.48 MB of large values |
| Current MigrationRuns | 1 row; 3.99 MB used |
| Current run payload length | 3.863 MB |
| Current MigrationUnits | 500 rows; 2.15 MB used |
| Project / run / job | 3 / 2 / 50 |
| Run state | Running, CommittedUnits 500, no completion date/error |
| Last saved run change | 2026-10-20 05:30:40.2844831 UTC |
| Exact Hangfire job 50 | Failed; inner SqlException says the database reached its size quota |

Run history consumes about **97% of used data space**. This is actual table usage, not just unused allocation. The earlier cleanup restored history capture, so repeated posting progress produced large history again. The current run, payload, approval and 500 committed units must be retained.

## Recover the space and stop this recurrence

Use [Recover_Run2_Quota_Space_Project3_20261020.sql](Recover_Run2_Quota_Space_Project3_20261020.sql) in SSMS against **NexusGarnetProd** on the confirmed server. This supersedes the earlier history-purge/reset scripts for this recovery. It does not require a backup reference.

The default **PREVIEW** checks the reviewed run, 500 master units and exact history binding. It makes no data changes. For recovery:

1. Stop every application site, background worker and integration connected to this installation, and confirm their processes have exited. Keep them stopped until the SQL commits. Confirm exact job **50** is still **Failed** in the configured Hangfire storage.
2. Open the complete SQL file. Replace the declarations at the top, without appending a second set:

   ```sql
   DECLARE @Mode varchar(10) = 'APPLY';
   DECLARE @AllWritersStopped bit = 1;
   DECLARE @Job50ConfirmedFailed bit = 1;
   ```

3. Run the **whole file once**. Require **RUN_HISTORY_CLEARED_CAPTURE_OFF**, run **2**, project **3**, job **50**, and **500** committed units. The run's saved **Running** state is intentionally retained. The script does not clear its JobId or change its approval/payload.
4. The script disables `SYSTEM_VERSIONING` **only for Migration.MigrationRuns**, truncates **only Migration.MigrationRuns_History**, and deliberately leaves capture off for that table. It retains the current table's period definition/columns, all current migration records, and all other history tables/configurations. It verifies current-data fingerprints before committing. Other migration auditing continues.
5. SQL may defer physical page deallocation until after commit. Allow that background work to finish, then rerun:

   ```sql
   SELECT name,
          CAST(size / 128.0 AS decimal(18,2)) AS AllocatedMB,
          CAST(FILEPROPERTY(name,'SpaceUsed') / 128.0 AS decimal(18,2)) AS UsedMB,
          CAST((size-FILEPROPERTY(name,'SpaceUsed')) / 128.0 AS decimal(18,2)) AS FreeInsideFileMB
   FROM sys.database_files
   WHERE type = 0;
   ```

   Used space should fall substantially and free space inside the file should increase. Allocation can remain 2,048 MB; shrinking is not required to reuse this space. Verify space has become available before resuming. If an APPLY check fails, retain its exact error rather than bypassing it. After manually cancelling a query, inspect `@@TRANCOUNT` and roll back any open transaction in that session.

## Resume the existing run

**Subsequent user-supplied completion evidence:** the later Trial Balance coverage results show run **2**, replacement job **51**, **Completed** with **1,112** committed units at **2026-10-20 08:01:24.1317162 UTC**. Runs 3–7 also show Completed. This records the user's reported resume/completion outcome; the assistant has not independently connected to the hosted database.

Restart the application/background workers, reload **Migration Centre**, select project **Data Migrate**, refresh progress and select **Resume Run** for run **2** once. Monitor its progress.

The source `DispatchAsync` accepts an existing Running run when its exact linked Hangfire job is **Failed**. It sets that same run to Queued, enqueues a replacement job and retains its committed count. The worker recognizes already committed units. A manual Running-to-Failed database change is unnecessary for the supplied job state. Do not create a new project/run, reset the units, clear JobId, edit approvals/mappings, or use Hangfire Retry for this recovery.

If job 50 changes to Processing, Enqueued or Scheduled, or can no longer be verified as Failed, inspect that state before retrying. This script checks the local HangFire.Job state when that table is present and otherwise requires the operator's configured-storage confirmation.

## Scope of the mitigation

This is a targeted UAT schema mitigation, reflecting the user's instruction that historical migration copies are unnecessary. It prevents new run-history copies while the existing payload stays available for resume. `FOR SYSTEM_TIME` history queries for MigrationRuns are unavailable while versioning is off. The other six migration temporal tables and business histories remain enabled.

The application model still marks MigrationRun temporal. Account for this database difference before future schema migrations; do not automatically re-enable run history while the large payload remains on the frequently updated row. The durable source design is to keep the immutable approved payload separate from progress. Application source has not been changed under the project's instruction requiring explicit authorization.

## Verification and sources

The verification fixture uses the real application EF model with 500 synthetic posted master units and a large run payload. It checks target/shutdown/job-state guards, preview preservation, rollback after history truncation, preservation of current migration/business data, the actual application's dispatch of Running + Failed job, and ordinary EF progress saves without additional run-history copies. It uses a uniquely named disposable LocalDB database and no hosted connection. Results are recorded in `artifacts/run2-quota-recovery-20261020/test-output.txt`.

**All seven execution checks passed.** The real dispatcher queued a replacement for Failed job 50 while preserving the 500 units. Subsequent ordinary EF status/progress saves succeeded and generated zero run-history copies. The build had zero warnings and zero errors. The disposable test database was removed after verification. This validates dispatch and the SQL mitigation; it does not claim that the remaining hosted import has completed.

Microsoft documents that [turning versioning off preserves current data and, when retained, period-column generation](https://learn.microsoft.com/en-us/sql/relational-databases/tables/temporal/stop-system-versioning?view=sql-server-ver17). History truncation is transactional and [large truncations can defer physical page deallocation until after commit](https://learn.microsoft.com/en-us/sql/t-sql/statements/truncate-table-transact-sql?view=sql-server-ver17). [Allocated Azure SQL data files do not automatically shrink after deletion](https://learn.microsoft.com/en-us/azure/azure-sql/database/file-space-manage).
