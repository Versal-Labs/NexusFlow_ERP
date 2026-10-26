-- READ-ONLY production migration audit. No data or schema changes.
-- In Azure Data Studio select:
--   Server: versalapps.database.windows.net
--   Database: NexusGarnetProd
-- Do not substitute a local/UAT connection. Run before preparing any cleanup.
SET NOCOUNT ON;
SET LOCK_TIMEOUT 15000;
SET DEADLOCK_PRIORITY LOW;
IF DB_NAME() <> N'NexusGarnetProd'
    THROW 51000, 'Wrong database. Select NexusGarnetProd on the confirmed Azure SQL server.', 1;

SELECT DB_NAME() AS DatabaseName,
       CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)) AS ServerName,
       CAST(SERVERPROPERTY('EngineEdition') AS int) AS EngineEdition,
       SYSDATETIMEOFFSET() AS DatabaseServerTime;

-- Table/state inventory. EstimatedRows is metadata, not a deletion allowlist.
SELECT s.name AS SchemaName, t.name AS TableName, t.temporal_type_desc AS TemporalType,
       SUM(p.rows) AS EstimatedRows,
       OBJECT_SCHEMA_NAME(t.history_table_id) AS HistorySchema,
       OBJECT_NAME(t.history_table_id) AS HistoryTable
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
LEFT JOIN sys.partitions AS p ON p.object_id = t.object_id AND p.index_id IN (0,1)
WHERE t.is_ms_shipped = 0
GROUP BY s.name, t.name, t.temporal_type_desc, t.history_table_id
ORDER BY s.name, t.name;

IF OBJECT_ID(N'Migration.MigrationProjects', N'U') IS NULL
BEGIN
    SELECT N'Migration Centre tables are absent. Review deployed schema before any cleanup.' AS AuditResult;
    RETURN;
END;

SELECT Id, Name, SourceSystem, OpeningDate, ClosingDate, Currency,
       Status, Version, FinalizedAt
FROM Migration.MigrationProjects ORDER BY Id;

SELECT ProjectId, Module, Status, COUNT_BIG(*) AS UnitCount,
       SUM(CASE WHEN JournalEntryId IS NOT NULL THEN CONVERT(bigint,1) ELSE 0 END) AS UnitsWithJournal,
       SUM(CASE WHEN ReversalJournalEntryId IS NOT NULL THEN CONVERT(bigint,1) ELSE 0 END) AS UnitsWithReversal
FROM Migration.MigrationUnits GROUP BY ProjectId, Module, Status
ORDER BY ProjectId, Module, Status;

SELECT ProjectId, Status, COUNT_BIG(*) AS RunCount, SUM(CONVERT(bigint,CommittedUnits)) AS CommittedUnits
FROM Migration.MigrationRuns GROUP BY ProjectId, Status ORDER BY ProjectId, Status;

SELECT ProjectId, Kind, Status, COUNT_BIG(*) AS ScopeCount
FROM Migration.MigrationScopes GROUP BY ProjectId, Kind, Status ORDER BY ProjectId, Kind, Status;

SELECT ProjectId, Module, Revision, ClosingDate, FileName, FileHash
FROM Migration.MigrationDatasets WHERE IsCurrent = 1 ORDER BY ProjectId, Module;

SELECT ProjectId, RecordKind, EntityKind, IsCurrent, COUNT_BIG(*) AS OriginCount
FROM Migration.MigrationOrigins
GROUP BY ProjectId, RecordKind, EntityKind, IsCurrent
ORDER BY ProjectId, RecordKind, EntityKind, IsCurrent;

-- Existing mappings/defaults belong to the PROJECT, not only Config.SystemConfigs.
-- Retain an independent protected export of SettingsJson before an eventual reset.
SELECT Id AS ProjectId,
       JSON_VALUE(SettingsJson, '$.OpeningEquityAccountId') AS OpeningEquityAccountId,
       JSON_QUERY(SettingsJson, '$.Defaults') AS ProjectDefaults,
       JSON_QUERY(SettingsJson, '$.Mappings') AS ProjectMappings,
       JSON_QUERY(SettingsJson, '$.Columns') AS SourceColumnMappings
FROM Migration.MigrationProjects ORDER BY Id;

-- Journals not linked to a Migration Centre unit can be ordinary business activity.
-- A nonzero count requires review. It is NOT authorization to delete those journals.
SELECT j.Module, COUNT_BIG(*) AS JournalCount, MIN(j.Date) AS FirstDate, MAX(j.Date) AS LastDate,
       SUM(CASE WHEN linked.JournalId IS NULL THEN CONVERT(bigint,1) ELSE 0 END) AS JournalsOutsideMigrationUnits
FROM Finance.JournalEntries AS j
LEFT JOIN
(
    SELECT JournalEntryId AS JournalId FROM Migration.MigrationUnits WHERE JournalEntryId IS NOT NULL
    UNION
    SELECT ReversalJournalEntryId FROM Migration.MigrationUnits WHERE ReversalJournalEntryId IS NOT NULL
) AS linked ON linked.JournalId = j.Id
GROUP BY j.Module ORDER BY j.Module;

SELECT COUNT_BIG(*) AS JournalsOutsideMigrationUnits
FROM Finance.JournalEntries AS j
WHERE NOT EXISTS
    (SELECT 1 FROM Migration.MigrationUnits AS u
     WHERE u.JournalEntryId = j.Id OR u.ReversalJournalEntryId = j.Id);

SELECT COUNT_BIG(*) AS AccountsWithNonzeroBalance
FROM Finance.Accounts WHERE Balance <> 0;

-- Current imported open-item positions; no individual customer/supplier names are exposed.
SELECT N'CustomerOpenings' AS Position,
       COUNT_BIG(*) AS [RowCount],
       COALESCE(SUM(CASE WHEN Amount > 0 THEN Amount ELSE 0 END),0) AS PositiveDebt,
       COALESCE(SUM(CASE WHEN Amount < 0 THEN Amount ELSE 0 END),0) AS NegativeCredit,
       COALESCE(SUM(Amount),0) AS NetAmount
FROM Sales.CustomerOpeningItems
UNION ALL
SELECT N'SupplierOpenings', COUNT_BIG(*),
       COALESCE(SUM(CASE WHEN Amount > 0 THEN Amount ELSE 0 END),0),
       COALESCE(SUM(CASE WHEN Amount < 0 THEN Amount ELSE 0 END),0),
       COALESCE(SUM(Amount),0)
FROM Purchasing.SupplierOpeningItems;

-- Imported openings already settled or credited need dependency review.
SELECT
    COUNT_BIG(*) AS AllocationsAgainstOpeningItems,
    SUM(CASE WHEN CustomerOpeningItemId IS NOT NULL THEN CONVERT(bigint,1) ELSE 0 END) AS CustomerAllocations,
    SUM(CASE WHEN SupplierOpeningItemId IS NOT NULL THEN CONVERT(bigint,1) ELSE 0 END) AS SupplierAllocations
FROM Finance.PaymentAllocations
WHERE CustomerOpeningItemId IS NOT NULL OR SupplierOpeningItemId IS NOT NULL;
SELECT COUNT_BIG(*) AS CustomerCreditApplications FROM Finance.CustomerCreditApplications;
SELECT COUNT_BIG(*) AS SupplierCreditApplications FROM Finance.SupplierCreditApplications;

SELECT c.Status, COUNT_BIG(*) AS ImportedChequeCount, SUM(c.Amount) AS Amount
FROM Finance.ChequeRegister AS c
WHERE EXISTS (SELECT 1 FROM Migration.MigrationOrigins AS o WHERE o.EntityKind = N'Cheque' AND o.EntityId = c.Id)
GROUP BY c.Status ORDER BY c.Status;

SELECT COUNT_BIG(*) AS ImportedBankItems,
       SUM(CASE WHEN b.ClearedDate IS NOT NULL OR b.CancelledDate IS NOT NULL THEN CONVERT(bigint,1) ELSE 0 END) AS ResolvedBankItems
FROM Finance.OpeningBankItems AS b
WHERE EXISTS (SELECT 1 FROM Migration.MigrationUnits AS u WHERE u.Id = b.MigrationUnitId);

SELECT COUNT_BIG(*) AS ImportedStockLayers,
       SUM(CASE WHEN l.RemainingQty <> l.InitialQty THEN CONVERT(bigint,1) ELSE 0 END) AS LayersWithChangedQuantity,
       COALESCE(SUM(ROUND(l.RemainingQty * l.UnitCost,2)),0) AS RemainingValue
FROM Inventory.StockLayers AS l
WHERE EXISTS (SELECT 1 FROM Migration.MigrationOrigins AS o WHERE o.EntityKind = N'StockLayer' AND o.EntityId = l.Id);

SELECT st.Type, COUNT_BIG(*) AS StockTransactionCount,
       SUM(CASE WHEN u.Id IS NULL THEN CONVERT(bigint,1) ELSE 0 END) AS TransactionsOutsideMigrationReferences
FROM Inventory.StockTransactions AS st
LEFT JOIN Migration.MigrationUnits AS u ON u.Module = N'Stock'
    AND st.ReferenceDocNo = CONCAT(N'MIG-',u.ProjectId,N'-',u.Id)
GROUP BY st.Type ORDER BY st.Type;

-- Restrict to dependency metadata; do not disable or alter constraints.
SELECT fk.name AS ForeignKeyName,
       OBJECT_SCHEMA_NAME(fk.parent_object_id) AS ChildSchema,
       OBJECT_NAME(fk.parent_object_id) AS ChildTable,
       OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS ParentSchema,
       OBJECT_NAME(fk.referenced_object_id) AS ParentTable,
       fk.is_disabled, fk.is_not_trusted
FROM sys.foreign_keys AS fk
WHERE OBJECT_SCHEMA_NAME(fk.parent_object_id) IN (N'Migration',N'Finance',N'Sales',N'Purchasing',N'Inventory')
   OR OBJECT_SCHEMA_NAME(fk.referenced_object_id) = N'Migration'
ORDER BY ChildSchema, ChildTable, ForeignKeyName;

SELECT N'Audit only. No reset, deletion, balance update, import or schema change was executed. Counts alone do not establish that records are safe to delete.' AS AuditResult;