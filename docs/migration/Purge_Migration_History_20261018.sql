/*
  Remove retained Migration Centre test-history copies — 18 October 2026.
  Target: versalapps.database.windows.net / NexusGarnetProd.

  Deletes ALL rows from the seven Migration.*_History tables only.
  Works before or after the separate current-project reset. Run it LAST if
  resetting the current project too, because reset deletions create history.
  Current migration rows and all other current/history tables are retained.
  Azure recovery settings and physical backup/source files are not changed.

  PREVIEW: reports the exact history tables/counts; no application changes.
  REHEARSE: truncates history inside a transaction, verifies and rolls back.
  PURGE: commits the removal. No backup reference or rehearsal hash required.

  Stop all application sites, IIS pools, worker processes and integrations
  before REHEARSE/PURGE, then set @AllWritersStopped=1 below.
  History tracking is restored after cleanup; this does not fix the source
  design that can generate large history again on subsequent imports.
  Data-file allocation is not shrunk by this script.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

DECLARE @Mode varchar(10) = 'PREVIEW'; -- PREVIEW, REHEARSE (optional), PURGE
DECLARE @AllWritersStopped bit = 0;
DECLARE @ExpectedDatabase sysname = N'NexusGarnetProd';
DECLARE @ExpectedServer sysname = N'versalapps';
DECLARE @ActualServer nvarchar(128) = CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));

IF DB_NAME()<>@ExpectedDatabase OR LOWER(@ActualServer) NOT IN
   (LOWER(@ExpectedServer),LOWER(@ExpectedServer+N'.database.windows.net'))
    THROW 52000, 'Wrong database/server. Refusing migration-history purge.', 1;
IF @@TRANCOUNT<>0 THROW 52001, 'Use a session with no existing transaction.', 1;
IF @Mode NOT IN ('PREVIEW','REHEARSE','PURGE') THROW 52002, 'Invalid mode.', 1;
IF @Mode<>'PREVIEW' AND @AllWritersStopped<>1
    THROW 52003, 'Stop all application/worker processes, then set @AllWritersStopped=1.', 1;

DROP TABLE IF EXISTS #HistoryTargets;
CREATE TABLE #HistoryTargets
(
    CurrentTable sysname COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
    CurrentObjectId int NULL,
    HistoryObjectId int NULL,
    HistoryName nvarchar(517) COLLATE DATABASE_DEFAULT NULL,
    RetentionValue int NULL,
    RetentionUnit nvarchar(60) COLLATE DATABASE_DEFAULT NULL,
    RetentionClause nvarchar(100) COLLATE DATABASE_DEFAULT NULL,
    CurrentRowsBefore bigint NULL,
    CurrentHashBefore varbinary(32) NULL,
    HistoryRowsBefore bigint NULL,
    HistoryRowsAfter bigint NULL
);
INSERT #HistoryTargets(CurrentTable) VALUES
    (N'MigrationProjects'),(N'MigrationDatasets'),(N'MigrationScopes'),
    (N'MigrationRuns'),(N'MigrationUnits'),(N'MigrationOrigins'),(N'MigrationAudit');

DECLARE @Table sysname,@History nvarchar(517),@Sql nvarchar(max),@Rows bigint,@HistoryRows bigint;
DECLARE @Hash varbinary(32),@Retention nvarchar(100),@LockResult int,@ProjectId int,@Resource nvarchar(255);
DECLARE @Report nvarchar(max),@Result varchar(40);
BEGIN TRY
    BEGIN TRANSACTION;
    EXEC @LockResult=sys.sp_getapplock @Resource=N'NexusFlow.Migration.0',
        @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
    IF @LockResult<0 THROW 52004, 'Migration project creation is busy.', 1;

    UPDATE x SET CurrentObjectId=t.object_id,HistoryObjectId=t.history_table_id,
        HistoryName=QUOTENAME(hs.name)+N'.'+QUOTENAME(h.name),
        RetentionValue=t.history_retention_period,RetentionUnit=t.history_retention_period_unit_desc
    FROM #HistoryTargets x
    LEFT JOIN sys.tables t ON t.object_id=OBJECT_ID(N'Migration.'+QUOTENAME(x.CurrentTable))
    LEFT JOIN sys.tables h ON h.object_id=t.history_table_id
    LEFT JOIN sys.schemas hs ON hs.schema_id=h.schema_id
    WHERE t.temporal_type=2 AND hs.name=N'Migration' AND h.name COLLATE DATABASE_DEFAULT=x.CurrentTable+N'_History';
    IF EXISTS (SELECT 1 FROM #HistoryTargets WHERE CurrentObjectId IS NULL OR HistoryObjectId IS NULL OR HistoryName IS NULL)
        THROW 52005, 'Expected seven enabled migration temporal tables and their exact history mappings.', 1;

    UPDATE #HistoryTargets SET RetentionClause=CASE
        WHEN RetentionValue=-1 THEN N'INFINITE'
        WHEN RetentionValue>0 AND RetentionUnit IN (N'DAY',N'DAYS',N'WEEK',N'WEEKS',N'MONTH',N'MONTHS',N'YEAR',N'YEARS')
            THEN CONVERT(nvarchar(20),RetentionValue)+N' '+RetentionUnit END;
    IF EXISTS (SELECT 1 FROM #HistoryTargets WHERE RetentionClause IS NULL)
        THROW 52005, 'Unrecognized retention configuration. No history removed.', 1;

    DECLARE project_locks CURSOR LOCAL FAST_FORWARD FOR SELECT Id FROM Migration.MigrationProjects ORDER BY Id;
    OPEN project_locks; FETCH NEXT FROM project_locks INTO @ProjectId;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Resource=N'NexusFlow.Migration.'+CONVERT(nvarchar(20),@ProjectId);
        EXEC @LockResult=sys.sp_getapplock @Resource=@Resource,
            @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
        IF @LockResult<0 THROW 52004, 'A migration project is busy.', 1;
        FETCH NEXT FROM project_locks INTO @ProjectId;
    END;
    CLOSE project_locks; DEALLOCATE project_locks;

    -- Fingerprint current records so clearing copies cannot silently alter current data.
    DECLARE before_counts CURSOR LOCAL FAST_FORWARD FOR
        SELECT CurrentTable,HistoryName FROM #HistoryTargets ORDER BY CurrentTable;
    OPEN before_counts; FETCH NEXT FROM before_counts INTO @Table,@History;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'SELECT @n=COUNT_BIG(*) FROM Migration.'+QUOTENAME(@Table)+N' WITH (UPDLOCK,HOLDLOCK); '+
            N'SELECT @h=HASHBYTES(''SHA2_256'',COALESCE((SELECT * FROM Migration.'+QUOTENAME(@Table)+
            N' ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES),N''[]'')); '+
            N'SELECT @v=COUNT_BIG(*) FROM '+@History+N';';
        EXEC sys.sp_executesql @Sql,N'@n bigint OUTPUT,@h varbinary(32) OUTPUT,@v bigint OUTPUT',
            @n=@Rows OUTPUT,@h=@Hash OUTPUT,@v=@HistoryRows OUTPUT;
        UPDATE #HistoryTargets SET CurrentRowsBefore=@Rows,CurrentHashBefore=@Hash,HistoryRowsBefore=@HistoryRows
            WHERE CurrentTable=@Table;
        FETCH NEXT FROM before_counts INTO @Table,@History;
    END;
    CLOSE before_counts; DEALLOCATE before_counts;

    IF @Mode='PREVIEW'
    BEGIN
        SET @Report=(SELECT CurrentTable,HistoryName,CurrentRowsBefore,HistoryRowsBefore FROM #HistoryTargets ORDER BY CurrentTable FOR JSON PATH);
        ROLLBACK;
        SELECT 'PREVIEW_ONLY' AS Result,DB_NAME() AS DatabaseName;
        SELECT * FROM OPENJSON(@Report) WITH
            (CurrentTable nvarchar(128),HistoryName nvarchar(517),CurrentRowsBefore bigint,HistoryRowsBefore bigint);
        RETURN;
    END;

    -- One transaction covers versioning OFF, history TRUNCATE and exact restoration.
    DECLARE disable_history CURSOR LOCAL FAST_FORWARD FOR SELECT CurrentTable FROM #HistoryTargets ORDER BY CurrentTable;
    OPEN disable_history; FETCH NEXT FROM disable_history INTO @Table;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'ALTER TABLE Migration.'+QUOTENAME(@Table)+N' SET (SYSTEM_VERSIONING=OFF);';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM disable_history INTO @Table;
    END;
    CLOSE disable_history; DEALLOCATE disable_history;

    DECLARE clear_history CURSOR LOCAL FAST_FORWARD FOR SELECT HistoryName FROM #HistoryTargets ORDER BY CurrentTable;
    OPEN clear_history; FETCH NEXT FROM clear_history INTO @History;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'TRUNCATE TABLE '+@History+N';';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM clear_history INTO @History;
    END;
    CLOSE clear_history; DEALLOCATE clear_history;

    -- Restore versioning with the SAME history objects and retention periods.
    DECLARE restore_history CURSOR LOCAL FAST_FORWARD FOR
        SELECT CurrentTable,HistoryName,RetentionClause FROM #HistoryTargets ORDER BY CurrentTable;
    OPEN restore_history; FETCH NEXT FROM restore_history INTO @Table,@History,@Retention;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'ALTER TABLE Migration.'+QUOTENAME(@Table)+N' SET (SYSTEM_VERSIONING=ON '+
            N'(HISTORY_TABLE='+@History+N',DATA_CONSISTENCY_CHECK=ON,HISTORY_RETENTION_PERIOD='+@Retention+N'));';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM restore_history INTO @Table,@History,@Retention;
    END;
    CLOSE restore_history; DEALLOCATE restore_history;

    IF EXISTS (SELECT 1 FROM #HistoryTargets x JOIN sys.tables t ON t.object_id=x.CurrentObjectId
        WHERE t.temporal_type<>2 OR t.history_table_id<>x.HistoryObjectId
           OR t.history_retention_period<>x.RetentionValue OR t.history_retention_period_unit_desc COLLATE DATABASE_DEFAULT<>x.RetentionUnit)
        THROW 52006, 'Temporal history configuration did not restore exactly. Rolling back.', 1;

    DECLARE after_counts CURSOR LOCAL FAST_FORWARD FOR SELECT CurrentTable,HistoryName FROM #HistoryTargets ORDER BY CurrentTable;
    OPEN after_counts; FETCH NEXT FROM after_counts INTO @Table,@History;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'SELECT @n=COUNT_BIG(*) FROM Migration.'+QUOTENAME(@Table)+N'; '+
            N'SELECT @h=HASHBYTES(''SHA2_256'',COALESCE((SELECT * FROM Migration.'+QUOTENAME(@Table)+
            N' ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES),N''[]'')); '+
            N'SELECT @v=COUNT_BIG(*) FROM '+@History+N';';
        EXEC sys.sp_executesql @Sql,N'@n bigint OUTPUT,@h varbinary(32) OUTPUT,@v bigint OUTPUT',
            @n=@Rows OUTPUT,@h=@Hash OUTPUT,@v=@HistoryRows OUTPUT;
        IF @HistoryRows<>0 THROW 52007, 'Migration history rows remain. Rolling back.', 1;
        IF NOT EXISTS (SELECT 1 FROM #HistoryTargets WHERE CurrentTable=@Table AND CurrentRowsBefore=@Rows AND CurrentHashBefore=@Hash)
            THROW 52008, 'Current migration records changed. Rolling back.', 1;
        UPDATE #HistoryTargets SET HistoryRowsAfter=@HistoryRows WHERE CurrentTable=@Table;
        FETCH NEXT FROM after_counts INTO @Table,@History;
    END;
    CLOSE after_counts; DEALLOCATE after_counts;
    SET @Report=(SELECT CurrentTable,HistoryName,CurrentRowsBefore,HistoryRowsBefore,HistoryRowsAfter FROM #HistoryTargets ORDER BY CurrentTable FOR JSON PATH);
    IF @Mode='PURGE'
    BEGIN
        COMMIT;
        SET @Result='PURGED';
    END
    ELSE
    BEGIN
        ROLLBACK;
        SET @Result='REHEARSED_ROLLED_BACK';
    END;
    SELECT @Result AS Result,DB_NAME() AS DatabaseName;
    SELECT * FROM OPENJSON(@Report) WITH
        (CurrentTable nvarchar(128),HistoryName nvarchar(517),CurrentRowsBefore bigint,HistoryRowsBefore bigint,HistoryRowsAfter bigint);
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
