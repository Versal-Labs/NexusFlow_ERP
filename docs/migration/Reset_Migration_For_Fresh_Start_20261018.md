# Clear the old migration and start a new project

Use [Reset_Migration_For_Fresh_Start_20261018.sql](Reset_Migration_For_Fresh_Start_20261018.sql) in SSMS against **versalapps.database.windows.net / NexusGarnetProd**. This combines the current-project reset with the migration-history purge. It supersedes the separate reset-then-purge sequence for this request. No backup or backup reference is required.

The user reported completing the history-only purge on 18 October. That result has not been independently verified. This reset clears history again at the end because deleting current temporal records creates new history.

The reviewed position is project **Data Migrate**, ID 2, source **TESTTTT**, with run 1, missing Hangfire job 44 and 500 posted master units. The script rejects a different project/run position rather than guessing what to delete.

## What the SQL clears

- All seven current migration tables: project settings/mappings, uploaded dataset records and parsed rows, scopes, runs, units, origins and migration audit.
- All seven associated migration history tables, including versions created by this reset.
- Customer and account masters proved to have been created by this import, provided they have no later edits or retained dependencies. Masters that existed before the import remain.

Temporal tracking is restored with the same history tables and retention settings. New imports can generate history again. This script does not change application code or shrink the database files.

Original uploaded files are stored outside SQL, in Azure Blob Storage or the server's local storage. The script displays their exact `StorageReference` values before deleting the dataset records. It does not delete those physical files; they do not prevent creation of a new project or another upload. Source: `MigrationCentreService.UploadAsync` and `GlobalStorageCoordinator`. Do not use SQL filesystem commands to guess their location.

## Run in SSMS

1. Open the complete SQL file. Its default `PREVIEW` mode makes no application changes. Run the whole file and review the proposed deletion counts.
2. Keep all application sites, background workers and integrations stopped throughout rehearsal and commit. Confirm exact Hangfire job **44** is missing from the configured job storage.
3. **Replace the existing declarations at the top** with these values; do not append another set:

   ```sql
   DECLARE @Mode varchar(10) = 'REHEARSE';
   DECLARE @AllWritersStopped bit = 1;
   DECLARE @Job44ConfirmedMissing bit = 1;
   DECLARE @RehearsalHash varchar(64) = '';
   ```

   Set the confirmation flags to 1 only after completing step 2. These flags prevent error 51003 when the prerequisites have actually been met.
4. Run the **whole file**. Require `REHEARSED_ROLLED_BACK`, and copy its `SnapshotHash`. Rehearsal rolls back every deletion, including history truncation.
5. Change `@Mode` to `COMMIT` and paste that hash into `@RehearsalHash`. Keep both confirmation flags at 1 and run the **whole file** again. Require `COMMITTED`. A changed data position invalidates the rehearsal hash.
6. Restart the application and workers. Open **Settings & Security → Migration Centre**, refresh progress and select **New Project**. Enter the new project details and upload the source files again. The new database ID need not be 2.

If any check fails, retain its exact error; do not bypass it. The transaction rolls back on an error. After manually cancelling a query, check `SELECT @@TRANCOUNT;` in that session and roll back any open transaction. Allow sufficient SSMS execution time for the actual hosted rehearsal; the full local 500-unit rerun of the earlier reset reached a 180-second client timeout.

## Verification

The combined script is verified using a uniquely named disposable SQL LocalDB database built from the application model. The compact fixture uses three posted units, reused masters, a new customer and new parent/child accounts. Only the private test copy substitutes the fixture database/server and counts; the delivered SQL retains the hosted target and reviewed 500-unit baseline.

The checks cover preview, required worker confirmation, later master edits, retained business dependencies, wrong or stale rehearsal hashes, rollback after history truncation, rehearsal rollback, clearing all fourteen migration tables, retaining existing masters/settings/suppliers, and restoring the original history bindings and both finite and infinite retention. Test output is recorded in `artifacts/hosted-migration-reset-20261016/verification-fresh-start-20261018.txt`. No hosted database is contacted by this test harness.

Hosted execution remains with the user in SSMS.
