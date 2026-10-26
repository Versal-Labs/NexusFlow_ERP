# Delete stored Migration Centre test-history copies

The user requested deletion of the retained copies on 18 October 2026 and explicitly declined a backup requirement. In this context these copies are SQL temporal-history rows. Azure recovery settings, separate database copies and physical backup files are outside this operation.

**Hosted execution remains with the user in SSMS. This file is not evidence that deletion has happened.**

Use [Purge_Migration_History_20261018.sql](Purge_Migration_History_20261018.sql) connected to **versalapps.database.windows.net / NexusGarnetProd**. It clears all rows in the exact history tables for MigrationProjects, MigrationDatasets, MigrationScopes, MigrationRuns, MigrationUnits, MigrationOrigins and MigrationAudit. It preserves their current records and every history table outside that allowlist.

## Run the query

1. If clearing the current project as well, finish the [current-project reset](Reset_Hosted_Migration_Project2_20261016.md) first. Run this history purge **last**, because deleting current temporal records creates history. If the current project is already gone, use only this purge.
2. Open the complete history-purge SQL file in SSMS and run its default **PREVIEW** mode. It reports the exact history tables and counts. It requires no backup reference and no missing-job confirmation.
3. Stop the application sites, IIS pools, worker processes and integrations for this installation. Once they have exited, replace the two existing declarations near the top of the file with:

   ```sql
   DECLARE @Mode varchar(10) = 'PURGE';
   DECLARE @AllWritersStopped bit = 1;
   ```

4. Execute the **whole file**. Require **PURGED** and seven HistoryRowsAfter values of zero. Optional mode **REHEARSE** performs the same deletion and verification but rolls it back; it is not required before PURGE and needs no hash.
5. Restart the services. To verify the resulting space usage, run [the read-only size diagnostics](Database_Size_Diagnostics_20261018.sql).

The purge temporarily disables versioning inside one transaction, truncates only the seven allowlisted history tables, then restores their exact original history-table mappings and retention settings. Current-row counts and SHA-256 fingerprints must match before/after. A failed operation rolls back the history removal and configuration changes. Normal history tracking resumes afterward; this removes existing copies but does not change the application's storage design or prevent future growth.

Clearing history releases its pages for reuse; it does not automatically reduce the allocated data-file size from 2 GB to 200 MB. Deferred deallocation may also delay reported free space. No shrink, Azure backup-policy change, physical backup deletion or application source edit is included.

Microsoft references: [temporary versioning shutdown for maintenance](https://learn.microsoft.com/en-us/sql/relational-databases/tables/temporal/stop-system-versioning?view=sql-server-ver17), [transactional TRUNCATE and deferred deallocation](https://learn.microsoft.com/en-us/sql/t-sql/statements/truncate-table-transact-sql?view=sql-server-ver17), and [Azure SQL used versus allocated space](https://learn.microsoft.com/en-us/azure/azure-sql/database/file-space-manage?view=azuresql-db).

## Verification status

The SQL passed execution tests in a uniquely named disposable SQL LocalDB database created from the current application model, with a large run payload, existing history, both finite and infinite retention settings, and preserved business/master records. The test database was removed afterward. The build passed with zero warnings/errors. Hosted execution has not been performed.

| Check | Result |
| --- | --- |
| Wrong server/database and missing stopped-writer confirmation reject changes | Pass |
| PREVIEW leaves current records, history and table settings unchanged | Pass |
| REHEARSE clears history and rolls it back exactly | Pass |
| Simulated failure after all truncations restores history and versioning | Pass |
| PURGE clears all seven migration history tables | Pass |
| Current migration fingerprints and business/master/history fingerprints remain unchanged | Pass |
| Exact history object mappings and 30-day/infinite retention settings are restored | Pass |
| Repeating purge on empty histories succeeds | Pass |
| Purge after a current-project reset leaves all fourteen migration current/history tables empty | Pass |

The metadata comparisons handle differing database/catalog/tempdb collations. Test harness and output are in `artifacts/migration-history-purge-20261018/`; they do not represent hosted deletion.
