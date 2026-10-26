# Database growth: confirmed run-history storage

**20 October confirmation:** the user's diagnostics show `Migration.MigrationRuns_History` using **1,984.55 MB** of **2,045.31 MB** used data space (about 97%), with 502 versions. The current run payload is 3.863 MB, and the run has 500 committed units. Exact Hangfire job **50** failed with the Azure SQL size-quota exception while run **2**, project **3**, remained Running. The earlier pending-measurement notes below are superseded by these supplied measurements. Use the [current recovery instructions](Run2_Quota_Recovery_Project3_20261020.md) to preserve committed work, clear only run history and stop run-history capture for this UAT database.

The user reported growth from about 200 MB to about 2 GB on 18 October 2026. The latest hosted table/file measurements have not been supplied. The exact space consumer remains unconfirmed.

## Recurrence reported on 20 October

The user reported another size increase and explicitly confirmed that it followed **Execute Approved Import**. The successful first settings save was reported for project **3 / Data Migrate / version 4 / 84 mappings / audit 516**. No current storage size, per-table measurements, run ID or completed-unit count has yet been supplied for this recurrence. The timing supports the run-history explanation below; it does not by itself prove which table physically occupies the space.

The source was rechecked: execution stores the full selection and preview in `MigrationRun.SelectionJson`, then updates the run as committed units increase. The model still versions this entity. The history purge and fresh-start reset supplied earlier both restore system versioning, so they remove existing migration history without changing the mechanism that can create it again. The follow-up warehouse/cheque mapping SQL updates one project's settings and inserts two small audit records; it does not loop over run payloads.

Run the existing read-only diagnostics before another purge to distinguish current run/history payloads, other large tables, transaction-log usage and allocated-but-unused file space. No hosted diagnostics have been performed by the assistant. No source change, history deletion or shrink operation was performed for this recurrence investigation.

## Evidence already available

- On 16 October, before any hosted reset was performed in this chat, the signed-in Hangfire overview showed **Data File(s) Used (MB): 2044.25**. It also showed log usage of 1.35 MB at that observation. This is a historical dashboard observation, not a current storage measurement.
- Project Data Migrate (2), run 1 had 500 committed units out of 1,112 approved units.
- `NexusFlow.AppCore/Features/Migration/MigrationPosting.cs:36` saves the full selection **and preview**, including its units/rows, into `MigrationRun.SelectionJson`.
- In the same source, the posting loop increments `run.CommittedUnits` for every newly posted unit and saves the tracked run at each atomic group. This changes the run repeatedly while retaining its large payload.
- `NexusFlow.Infrastructure/ErpDbContext.cs:212` enables temporal history on auditable entities, including MigrationRun, with a corresponding `_History` table. `SelectionJson` is `nvarchar(max)` in the migration schema.
- The reset leaves temporal history enabled and retained. Deleting a current run therefore does not remove its earlier versions. REHEARSE rolls its changes back; it can still generate transaction-log work and leave allocated file space available for reuse.
- Error 51003 is raised before the reset transaction begins. An attempt that stops at that error has not performed the reset deletions.

## Leading explanation

Repeated run-history versions containing a large immutable preview are a strong candidate for the growth. For illustration only, a 4 MB snapshot represented in 500 versions corresponds to roughly 2,000 MB of logical payload. The actual average payload length and physical history-table usage must be measured; logical field lengths are not identical to physical storage because allocation, indexes, compression and LOB storage behavior affect the result.

Microsoft documents that temporal tables retain previous row versions and that update/delete-heavy workloads can produce large history tables. It also identifies substantial storage/performance costs for large values in temporal tables. See [temporal history management](https://learn.microsoft.com/en-us/sql/relational-databases/tables/temporal/manage-retention?view=sql-server-ver17) and [temporal large-value considerations](https://learn.microsoft.com/en-us/sql/relational-databases/tables/temporal/considerations-limitations?view=sql-server-ver17).

A second distinction is **used space versus allocated space**. Azure SQL data-file allocation does not automatically decrease when rows are deleted. A database can therefore still report a large allocation after its used space falls. See [Azure SQL file-space management](https://learn.microsoft.com/en-us/azure/azure-sql/database/file-space-manage?view=azuresql-db).

## Read-only confirmation

Run [Database_Size_Diagnostics_20261018.sql](Database_Size_Diagnostics_20261018.sql) in SSMS connected to NexusGarnetProd. It reports:

1. The selected database and any open transaction in that session.
2. Data-file used/allocated/free space and log allocation.
3. The 25 largest current/history tables, with physical LOB and index usage.
4. Migration history-table mappings and retention settings.
5. Current run payload sizes and history-version counts/logical payload totals, without exposing the payload contents.
6. Transaction-log usage and reuse wait, when permitted by the login.

The script contains read-only queries and session output settings; it performs no data cleanup, shrink, rebuild or schema change. It was checked against the source schema and Microsoft's DMV definitions, not executed against the hosted database.

Interpretation:

- If Migration.MigrationRuns_History dominates UsedMB, run-history amplification is confirmed as the main table-space consumer.
- If allocated data space is about 2 GB but used data space is much smaller, much of the reported size is reusable allocation.
- If the log dominates, review log usage/reuse and active transactions separately.
- Other large tables, including audit or Hangfire tables, should be investigated according to their actual measured contribution.

## Preventing recurrence

The durable design improvement is to store the approved immutable payload once, separately from the frequently updated/versioned progress record, while keeping approval hashes, execution ownership, resume safety and audit evidence intact. The history retention period needs business confirmation. Cleanup and file-space reclamation should follow the measured result; no history deletion or shrink operation was performed for this investigation. Application source remains unchanged under AGENTS.md.
