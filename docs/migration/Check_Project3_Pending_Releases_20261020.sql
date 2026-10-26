-- READ ONLY. Run in SSMS against NexusGarnetProd on versalapps.
-- This reports saved release progress and supplies a small customer code filter.
-- It does not release scopes, change balances, or change any application data.
SET NOCOUNT ON;
SET LOCK_TIMEOUT 5000;

IF DB_NAME() <> N'NexusGarnetProd'
   OR LOWER(CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')))
      NOT IN (N'versalapps', N'versalapps.database.windows.net')
    THROW 54100, 'Select NexusGarnetProd on versalapps.', 1;

DECLARE @ProjectId int = 3;
IF NOT EXISTS (SELECT 1 FROM Migration.MigrationProjects WHERE Id = @ProjectId)
    THROW 54101, 'Project 3 was not found.', 1;

SELECT N'PROJECT' AS ResultSet, SYSUTCDATETIME() AS CheckedAtUtc,
       Id, Name, Status, Version
FROM Migration.MigrationProjects WHERE Id = @ProjectId;

-- Repeat this query later to see whether the saved counts are still changing.
SELECT N'SCOPE_COUNTS' AS ResultSet, Kind, Status, COUNT_BIG(*) AS ScopeCount
FROM Migration.MigrationScopes WHERE ProjectId = @ProjectId
GROUP BY Kind, Status ORDER BY Kind, Status;

-- Bulk release is a browser request, not a Hangfire posting run.
-- These rows show posting runs only; their absence does not prove release is idle.
SELECT N'ACTIVE_POSTING_RUNS' AS ResultSet, Id AS RunId, JobId, Status, CommittedUnits
FROM Migration.MigrationRuns
WHERE ProjectId = @ProjectId AND Status IN (N'Queued', N'Running')
ORDER BY Id;

SELECT N'FINANCIAL_DATA_STATES' AS ResultSet,
       states.[key] AS Module, states.[value] AS DataStatus
FROM Migration.MigrationProjects AS p
CROSS APPLY OPENJSON(CASE WHEN ISJSON(p.SettingsJson) = 1
                         THEN p.SettingsJson ELSE N'{}' END, '$.DatasetStates') AS states
WHERE p.Id = @ProjectId
  AND states.[key] IN (N'CustomerOpenings', N'SupplierOpenings', N'ReceivedCheques',
                      N'IssuedCheques', N'Stock', N'BankOpening', N'BankRecon', N'TrialBalance')
ORDER BY states.[key];

SELECT N'CONTROL_TOTALS_UNITS' AS ResultSet, Id AS UnitId, ScopeKey, Status
FROM Migration.MigrationUnits
WHERE ProjectId = @ProjectId AND Module = N'ControlTotals'
ORDER BY Id;

-- No posted unit can be correct for a genuinely empty component. These counts
-- alone are not a failed release reason; the application checks the source too.
SELECT TOP (25) N'PENDING_CUSTOMERS' AS ResultSet,
       s.Code, s.Status, s.EntityId, s.RequiredModulesJson,
       (SELECT COUNT_BIG(*) FROM Migration.MigrationUnits AS u
        WHERE u.ProjectId = s.ProjectId AND u.ScopeKey = s.Code
          AND u.Module = N'CustomerOpenings' AND u.Status = N'Posted') AS CustomerOpeningsPostedUnits,
       (SELECT COUNT_BIG(*) FROM Migration.MigrationUnits AS u
        WHERE u.ProjectId = s.ProjectId AND u.ScopeKey = s.Code
          AND u.Module = N'ReceivedCheques' AND u.Status = N'Posted') AS ReceivedChequesPostedUnits
FROM Migration.MigrationScopes AS s
WHERE s.ProjectId = @ProjectId AND s.Kind = N'Customer' AND s.Status = N'Pending'
ORDER BY s.Code;

-- Use only after the earlier release request has ended and progress is stable.
-- Select Customer Master ONLY, paste these codes into Customer codes, enter the
-- independently checked evidence, then use Release Selected Reconciled Scopes.
-- After each result, reselect Customer Master ONLY; the page reload can restore
-- other module checkboxes. Re-run this script to obtain the next pending batch.
SELECT N'NEXT_CUSTOMER_BATCH_25' AS ResultSet,
       STRING_AGG(CONVERT(nvarchar(max), batch.Code), N',')
           WITHIN GROUP (ORDER BY batch.Code) AS CustomerCodes
FROM (
    SELECT TOP (25) Code FROM Migration.MigrationScopes
    WHERE ProjectId = @ProjectId AND Kind = N'Customer' AND Status = N'Pending'
    ORDER BY Code
) AS batch;

-- After the single-customer test succeeds, this supplies a smaller batch for
-- each remaining kind. Work on one row/kind per browser request. Select ONLY
-- the indicated module, and paste Codes into the indicated code filter.
-- Re-run after every successful batch. A failed scope can recur in the list;
-- resolve its actual application message rather than repeating it indefinitely.
WITH numbered AS (
    SELECT Kind, Code,
           ROW_NUMBER() OVER (PARTITION BY Kind ORDER BY Code) AS Position
    FROM Migration.MigrationScopes
    WHERE ProjectId = @ProjectId AND Status = N'Pending'
      AND Kind IN (N'Customer', N'Supplier', N'Stock', N'Bank')
)
SELECT N'NEXT_BATCH_PER_KIND_25' AS ResultSet, Kind,
       CASE Kind WHEN N'Customer' THEN N'Customer Master'
                 WHEN N'Supplier' THEN N'Supplier Master'
                 WHEN N'Stock' THEN N'Stock'
                 WHEN N'Bank' THEN N'Bank Opening' END AS SelectOnlyModule,
       CASE Kind WHEN N'Customer' THEN N'Customer codes'
                 WHEN N'Supplier' THEN N'Supplier codes'
                 WHEN N'Stock' THEN N'SKU codes'
                 WHEN N'Bank' THEN N'Bank account codes' END AS PasteIntoFilter,
       STRING_AGG(CONVERT(nvarchar(max), Code), N',')
           WITHIN GROUP (ORDER BY Code) AS Codes
FROM numbered WHERE Position <= 25
GROUP BY Kind ORDER BY Kind;

SET LOCK_TIMEOUT -1;
