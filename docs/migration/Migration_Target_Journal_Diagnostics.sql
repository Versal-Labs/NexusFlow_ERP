-- Read-only diagnosis of the Migration Centre "target already contains financial journals" guard.
-- In SSMS, select the database used by the failing application. Replace the placeholder below.
-- This script reports records only. It does not clear data, reset balances or create a project.
-- Checked against the current source mappings; not executed against hosted UAT.
SET NOCOUNT ON;
DECLARE @ExpectedDatabase sysname = N'REPLACE_WITH_EXACT_TARGET_DATABASE';
IF @ExpectedDatabase = N'REPLACE_WITH_EXACT_TARGET_DATABASE' OR DB_NAME() <> @ExpectedDatabase
    THROW 51000, 'Select the confirmed application database and set its exact name before running.', 1;
IF OBJECT_ID(N'Finance.JournalEntries', N'U') IS NULL
    OR OBJECT_ID(N'Finance.JournalLines', N'U') IS NULL
    OR OBJECT_ID(N'Finance.CustomerMigrationBatches', N'U') IS NULL
    THROW 51001, 'Required financial tables are missing. Check the database and deployed schema.', 1;

SELECT DB_NAME() AS ConfirmedDatabase, @@SERVERNAME AS SqlServer;

-- Mirrors CreateAsync: every journal is blocked except journals linked to Posted customer batches.
-- No date, module, amount, reversal or zero-net exception is applied by this guard.
SELECT COUNT_BIG(*) AS TotalJournalEntries,
       COALESCE(SUM(CASE WHEN EXISTS_FOR_ADOPTION.IsEligible = 1 THEN CONVERT(bigint, 1) ELSE 0 END), 0) AS CustomerBatchJournalCandidates,
       COALESCE(SUM(CASE WHEN EXISTS_FOR_ADOPTION.IsEligible = 0 THEN CONVERT(bigint, 1) ELSE 0 END), 0) AS BlockingJournalEntries
FROM Finance.JournalEntries AS j
CROSS APPLY (SELECT CASE WHEN EXISTS (
    SELECT 1 FROM Finance.CustomerMigrationBatches AS b
    WHERE b.Status = N'Posted' AND b.JournalEntryId = j.Id
) THEN 1 ELSE 0 END AS IsEligible) AS EXISTS_FOR_ADOPTION;

-- Summary of exactly the entries that cause the reported error.
SELECT j.Module, COUNT_BIG(*) AS BlockingJournalEntries,
       MIN(j.[Date]) AS EarliestJournalDate, MAX(j.[Date]) AS LatestJournalDate
FROM Finance.JournalEntries AS j
WHERE NOT EXISTS (
    SELECT 1 FROM Finance.CustomerMigrationBatches AS b
    WHERE b.Status = N'Posted' AND b.JournalEntryId = j.Id
)
GROUP BY j.Module
ORDER BY j.Module;

-- Document references help distinguish training activity, previous imports and real trading.
SELECT j.Id AS JournalEntryId, j.[Date], j.Module, j.ReferenceNo, j.[Description],
       COALESCE(lines.TotalDebit, 0) AS TotalDebit,
       COALESCE(lines.TotalCredit, 0) AS TotalCredit,
       COALESCE(lines.LineCount, 0) AS LineCount
FROM Finance.JournalEntries AS j
OUTER APPLY (
    SELECT SUM(l.Debit) AS TotalDebit, SUM(l.Credit) AS TotalCredit, COUNT_BIG(*) AS LineCount
    FROM Finance.JournalLines AS l WHERE l.JournalEntryId = j.Id
) AS lines
WHERE NOT EXISTS (
    SELECT 1 FROM Finance.CustomerMigrationBatches AS b
    WHERE b.Status = N'Posted' AND b.JournalEntryId = j.Id
)
ORDER BY j.[Date], j.Id;

-- A candidate is not automatic approval: adoption also requires matching dates, evidence,
-- reconciled account balances and the supported unused opening position.
SELECT b.Id AS CustomerMigrationBatchId, b.Status, b.CutoverDate, b.SourceAsOfDate,
       b.JournalEntryId, b.ReversalJournalEntryId, b.IsSignedOff
FROM Finance.CustomerMigrationBatches AS b
ORDER BY b.Id;

-- Resolution depends on business classification, not the journal's Module label alone:
-- * Disposable training history: retain a backup/archive and provision a separate clean migration target.
-- * Supported existing customer opening: review adoption evidence and the same original dates.
-- * Real trading or other imports: retain history and design a reviewed reconciliation/correction strategy.
-- Deleting only journals, setting account balances to zero, or removing the guard is not a valid reset.
