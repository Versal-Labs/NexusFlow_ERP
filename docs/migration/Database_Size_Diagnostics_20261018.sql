/* Read-only database size diagnosis — 18 October 2026.
   Connect SSMS to NexusGarnetProd and run this whole file.
   Returns sizes and counts only; no source/customer payload text is returned.
   No data deletion, transaction changes, shrink, rebuild or schema changes.
   Optional DMV sections report permission errors without stopping other sections.
*/
SET NOCOUNT ON;

-- 1. Confirm which database is being measured and whether this session has an open transaction.
SELECT DB_NAME() AS DatabaseName, SYSUTCDATETIME() AS CapturedAtUtc,
       @@TRANCOUNT AS OpenTransactionsInThisSession;

-- 2. Data and log file allocations. Used/free measurements below apply to ROWS data files.
SELECT name AS LogicalFileName, type_desc AS FileType,
       CAST(size / 128.0 AS decimal(18,2)) AS AllocatedMB,
       CASE WHEN type=0 THEN CAST(FILEPROPERTY(name,'SpaceUsed') / 128.0 AS decimal(18,2)) END AS UsedDataMB,
       CASE WHEN type=0 THEN CAST((size-FILEPROPERTY(name,'SpaceUsed')) / 128.0 AS decimal(18,2)) END AS UnusedAllocatedDataMB
FROM sys.database_files
ORDER BY type,file_id;

-- 3. The largest current AND history tables. Physical used space includes indexes.
--    Count heap/clustered rows once; summing every index's rows would overcount.
BEGIN TRY
    SELECT TOP (25)
           QUOTENAME(s.name)+N'.'+QUOTENAME(t.name) AS TableName,
           CASE t.temporal_type WHEN 1 THEN N'History' WHEN 2 THEN N'Current, versioned' ELSE N'Current' END AS TableType,
           SUM(CASE WHEN p.index_id IN (0,1) THEN p.row_count ELSE CONVERT(bigint,0) END) AS ApproxRows,
           CAST(SUM(p.used_page_count)*8.0/1024 AS decimal(18,2)) AS UsedMB,
           CAST(SUM(p.reserved_page_count)*8.0/1024 AS decimal(18,2)) AS ReservedMB,
           CAST(SUM(p.lob_used_page_count)*8.0/1024 AS decimal(18,2)) AS LargeValuesUsedMB
    FROM sys.dm_db_partition_stats p
    JOIN sys.tables t ON t.object_id=p.object_id
    JOIN sys.schemas s ON s.schema_id=t.schema_id
    WHERE t.is_ms_shipped=0
    GROUP BY s.name,t.name,t.temporal_type
    ORDER BY SUM(p.used_page_count) DESC,s.name,t.name;
END TRY
BEGIN CATCH
    SELECT N'Largest tables' AS DiagnosticSection, ERROR_MESSAGE() AS DiagnosticNotice;
END CATCH;

-- 4. The actual history-table mapping/retention, including a run that has since been deleted.
SELECT QUOTENAME(s.name)+N'.'+QUOTENAME(t.name) AS CurrentTable,
       QUOTENAME(hs.name)+N'.'+QUOTENAME(h.name) AS HistoryTable,
       t.temporal_type_desc, t.history_retention_period, t.history_retention_period_unit_desc
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id=t.schema_id
LEFT JOIN sys.tables h ON h.object_id=t.history_table_id
LEFT JOIN sys.schemas hs ON hs.schema_id=h.schema_id
WHERE s.name=N'Migration'
ORDER BY t.name;

-- 5. Current migration payload lengths, without returning the JSON.
IF OBJECT_ID(N'Migration.MigrationRuns',N'U') IS NOT NULL
BEGIN
    SELECT Id AS RunId, ProjectId, Status, CommittedUnits,
           CAST(DATALENGTH(SelectionJson)/1048576.0 AS decimal(18,3)) AS CurrentPayloadMB,
           CreatedAt, LastModifiedAt
    FROM Migration.MigrationRuns ORDER BY Id;
END;

-- 6. Aggregate repeated payload lengths in the actual run history table.
--    LogicalPayloadMB is uncompressed field length, NOT physical table/file usage.
DECLARE @HistoryTable nvarchar(517), @Sql nvarchar(max);
SELECT @HistoryTable=QUOTENAME(s.name)+N'.'+QUOTENAME(h.name)
FROM sys.tables t JOIN sys.tables h ON h.object_id=t.history_table_id
JOIN sys.schemas s ON s.schema_id=h.schema_id
WHERE t.object_id=OBJECT_ID(N'Migration.MigrationRuns');
IF @HistoryTable IS NOT NULL
BEGIN
    SET @Sql=N'SELECT Id AS RunId,ProjectId,COUNT_BIG(*) AS HistoryVersions,
        CAST(COALESCE(SUM(CONVERT(bigint,DATALENGTH(SelectionJson))),0)/1048576.0 AS decimal(18,2)) AS LogicalPayloadMB,
        CAST(AVG(CONVERT(decimal(28,2),DATALENGTH(SelectionJson)))/1048576.0 AS decimal(18,3)) AS AveragePayloadMB,
        MIN(ValidFrom) AS FirstVersionFromUtc,MAX(ValidTo) AS LastVersionToUtc
        FROM '+@HistoryTable+N' GROUP BY Id,ProjectId ORDER BY LogicalPayloadMB DESC;';
    EXEC sys.sp_executesql @Sql;
END;

-- 7. Log usage is separate from data-file usage.
BEGIN TRY
    SELECT CAST(total_log_size_in_bytes/1048576.0 AS decimal(18,2)) AS AllocatedLogMB,
           CAST(used_log_space_in_bytes/1048576.0 AS decimal(18,2)) AS UsedLogMB,
           CAST(used_log_space_in_percent AS decimal(9,2)) AS LogUsedPercent
    FROM sys.dm_db_log_space_usage;
    SELECT name, log_reuse_wait_desc FROM sys.databases WHERE database_id=DB_ID();
END TRY
BEGIN CATCH
    SELECT N'Transaction log' AS DiagnosticSection, ERROR_MESSAGE() AS DiagnosticNotice;
END CATCH;
