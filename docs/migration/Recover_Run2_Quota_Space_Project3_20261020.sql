/*
  Emergency UAT space recovery, 20 October 2026. No project reset.
  Target: versalapps.database.windows.net / NexusGarnetProd.
  Reviewed: project 3, run 2 Running, job 50 Failed, 500 committed units;
  MigrationRuns_History = 1,984.55 MB, data used = 2,045.31 MB of 2,048 MB.

  APPLY deletes ONLY MigrationRuns_History and leaves SYSTEM_VERSIONING OFF
  for MigrationRuns. This prevents another copy of its ~3.863 MB payload on
  every progress update. Current run, approval, payload, units and all other
  current/history data are retained. Period columns remain for EF updates.
  This is a targeted UAT schema mitigation; the application model still marks
  runs temporal. A later schema deployment must account for this difference.
  No backup reference, full reset, shrink, JobId edit or run-status edit.

  Stop ALL application/worker/integration processes first. Confirm exact job
  50 is Failed in this installation's Hangfire storage. Then replace the
  three declarations below with APPLY / 1 / 1 and run the WHOLE file once.
  After success, allow deferred page deallocation and verify used data falls.
  Restart the app/workers, refresh Migration Centre and use Resume Run 2.
  Do not Execute a new run or use the earlier reset/purge scripts afterwards.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

DECLARE @Mode varchar(10) = 'PREVIEW'; -- PREVIEW or APPLY
DECLARE @AllWritersStopped bit = 0;
DECLARE @Job50ConfirmedFailed bit = 0;
DECLARE @ExpectedDatabase sysname = N'NexusGarnetProd';
DECLARE @ExpectedServer sysname = N'versalapps';
DECLARE @ObservedModifiedAt datetime2(7)='2026-10-20T05:30:40.2844831';
IF DB_NAME()<>@ExpectedDatabase OR LOWER(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')))
    NOT IN(LOWER(@ExpectedServer),LOWER(@ExpectedServer+N'.database.windows.net'))
    THROW 53000,'Wrong server/database. Refusing run-history recovery.',1;
IF @@TRANCOUNT<>0 THROW 53001,'Use a session with no open transaction.',1;
IF @Mode NOT IN('PREVIEW','APPLY') THROW 53002,'Invalid mode.',1;
IF @Mode='APPLY' AND (@AllWritersStopped<>1 OR @Job50ConfirmedFailed<>1)
    THROW 53003,'Stop all application/worker processes and confirm exact job 50 is Failed before APPLY.',1;

DROP TABLE IF EXISTS #Preserve;
CREATE TABLE #Preserve(ObjectName nvarchar(300) COLLATE DATABASE_DEFAULT PRIMARY KEY,
    RowsBefore bigint NOT NULL,HashBefore varbinary(32) NOT NULL);
DECLARE @Tables table(ObjectName nvarchar(300) COLLATE DATABASE_DEFAULT PRIMARY KEY);
INSERT @Tables VALUES(N'Migration.MigrationProjects'),(N'Migration.MigrationDatasets'),
    (N'Migration.MigrationScopes'),(N'Migration.MigrationRuns'),(N'Migration.MigrationUnits'),
    (N'Migration.MigrationOrigins'),(N'Migration.MigrationAudit'),
    (N'Sales.Customers'),(N'Purchasing.Suppliers'),(N'Finance.Accounts');
DECLARE @Object nvarchar(300),@Sql nvarchar(max),@Rows bigint,@Hash varbinary(32),@LockResult int;
DECLARE @MetadataBefore varbinary(32),@MetadataAfter varbinary(32),@HistoryRows bigint;
DECLARE @HistoryObjectId int=OBJECT_ID(N'Migration.MigrationRuns_History',N'U');
BEGIN TRY
    BEGIN TRANSACTION;
    EXEC @LockResult=sys.sp_getapplock @Resource=N'NexusFlow.Migration.0',@LockMode='Exclusive',
         @LockOwner='Transaction',@LockTimeout=15000;
    IF @LockResult<0 THROW 53004,'Migration creation is busy.',1;
    EXEC @LockResult=sys.sp_getapplock @Resource=N'NexusFlow.Migration.3',@LockMode='Exclusive',
         @LockOwner='Transaction',@LockTimeout=15000;
    IF @LockResult<0 THROW 53004,'Migration project is busy.',1;
    IF NOT EXISTS(SELECT 1 FROM Migration.MigrationProjects p WITH(UPDLOCK,HOLDLOCK)
                  JOIN Migration.MigrationRuns r WITH(UPDLOCK,HOLDLOCK) ON r.ProjectId=p.Id
                  WHERE p.Id=3 AND p.Name=N'Data Migrate' AND p.SourceSystem=N'TESTTTT'
                    AND p.Status=N'Preparing' AND p.FinalizedAt IS NULL
                    AND p.OpeningDate=CONVERT(date,'2026-10-01') AND p.ClosingDate=CONVERT(date,'2026-09-30')
                    AND p.ApprovedHash=r.ApprovedHash AND r.Id=2 AND r.JobId=N'50'
                    AND r.Status=N'Running' AND r.CommittedUnits=500 AND r.CompletedAt IS NULL
                    AND r.Error IS NULL AND r.LastModifiedAt=@ObservedModifiedAt)
       OR (SELECT COUNT(*) FROM Migration.MigrationRuns WITH(UPDLOCK,HOLDLOCK))<>1
        THROW 53005,'Project/run changed from the reviewed project 3 / run 2 / job 50. Inspect again.',1;
    IF (SELECT COUNT(*) FROM Migration.MigrationUnits WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId=3)<>500
       OR (SELECT COUNT(*) FROM Migration.MigrationUnits WHERE ProjectId=3 AND Status=N'Posted'
            AND Module=N'Accounts' AND JournalEntryId IS NULL)<>1
       OR (SELECT COUNT(*) FROM Migration.MigrationUnits WHERE ProjectId=3 AND Status=N'Posted'
            AND Module=N'CustomerMaster' AND JournalEntryId IS NULL)<>499
        THROW 53006,'Committed units differ from the reviewed 500 master units. Nothing was changed.',1;
    IF @HistoryObjectId IS NULL OR NOT EXISTS(SELECT 1 FROM sys.tables
        WHERE object_id=OBJECT_ID(N'Migration.MigrationRuns') AND temporal_type=2 AND history_table_id=@HistoryObjectId)
        THROW 53007,'Expected enabled MigrationRuns history binding not found. This recovery may already have been applied.',1;
    IF NOT EXISTS(SELECT 1 FROM sys.periods WHERE object_id=OBJECT_ID(N'Migration.MigrationRuns'))
        THROW 53007,'MigrationRuns period definition is missing.',1;
    IF EXISTS(SELECT 1 FROM Migration.MigrationRuns_History WHERE Id<>2 OR ProjectId<>3)
        THROW 53007,'Unreviewed run history exists. Refusing to clear another run history.',1;
    IF OBJECT_ID(N'HangFire.Job',N'U') IS NOT NULL
        EXEC sys.sp_executesql N'IF NOT EXISTS(SELECT 1 FROM HangFire.Job WHERE Id=50 AND StateName=N''Failed'')
            THROW 53008,''The local Hangfire job 50 is not Failed. Confirm the configured storage before recovery.'',1;';

    DECLARE preserve_before CURSOR LOCAL FAST_FORWARD FOR SELECT ObjectName FROM @Tables ORDER BY ObjectName;
    OPEN preserve_before; FETCH NEXT FROM preserve_before INTO @Object;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'SELECT @n=COUNT_BIG(*) FROM '+@Object+N' WITH(UPDLOCK,HOLDLOCK); '+
            N'SELECT @h=HASHBYTES(''SHA2_256'',COALESCE((SELECT * FROM '+@Object+N' ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES),N''[]''));';
        EXEC sys.sp_executesql @Sql,N'@n bigint OUTPUT,@h varbinary(32) OUTPUT',@n=@Rows OUTPUT,@h=@Hash OUTPUT;
        INSERT #Preserve VALUES(@Object,@Rows,@Hash);
        FETCH NEXT FROM preserve_before INTO @Object;
    END;
    CLOSE preserve_before; DEALLOCATE preserve_before;
    SELECT @MetadataBefore=HASHBYTES('SHA2_256',(SELECT object_id,temporal_type,history_table_id,
        history_retention_period,history_retention_period_unit FROM sys.tables
        WHERE object_id NOT IN(OBJECT_ID(N'Migration.MigrationRuns'),@HistoryObjectId)
        ORDER BY object_id FOR JSON PATH,INCLUDE_NULL_VALUES));
    SELECT @HistoryRows=COUNT_BIG(*) FROM Migration.MigrationRuns_History;
    IF @Mode='PREVIEW'
    BEGIN
        ROLLBACK;
        SELECT N'PREVIEW_ONLY' AS Result,2 AS RunId,3 AS ProjectId,500 AS CommittedUnits,
               @HistoryRows AS RunHistoryRowsToClear,
               N'APPLY clears only run history and leaves run history capture OFF. Current data is preserved.' AS PlannedAction;
        RETURN;
    END;

    ALTER TABLE Migration.MigrationRuns SET (SYSTEM_VERSIONING=OFF);
    TRUNCATE TABLE Migration.MigrationRuns_History;
    -- Deliberately leave run history capture OFF; do not restore it here.
    IF EXISTS(SELECT 1 FROM Migration.MigrationRuns_History)
       OR NOT EXISTS(SELECT 1 FROM sys.tables WHERE object_id=OBJECT_ID(N'Migration.MigrationRuns') AND temporal_type=0)
       OR NOT EXISTS(SELECT 1 FROM sys.periods WHERE object_id=OBJECT_ID(N'Migration.MigrationRuns'))
        THROW 53009,'History removal/capture-off/period-preservation verification failed.',1;

    DECLARE preserve_after CURSOR LOCAL FAST_FORWARD FOR SELECT ObjectName FROM @Tables ORDER BY ObjectName;
    OPEN preserve_after; FETCH NEXT FROM preserve_after INTO @Object;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'SELECT @n=COUNT_BIG(*) FROM '+@Object+N'; '+
            N'SELECT @h=HASHBYTES(''SHA2_256'',COALESCE((SELECT * FROM '+@Object+N' ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES),N''[]''));';
        EXEC sys.sp_executesql @Sql,N'@n bigint OUTPUT,@h varbinary(32) OUTPUT',@n=@Rows OUTPUT,@h=@Hash OUTPUT;
        IF NOT EXISTS(SELECT 1 FROM #Preserve WHERE ObjectName=@Object AND RowsBefore=@Rows AND HashBefore=@Hash)
            THROW 53010,'A current record changed. Recovery rolled back.',1;
        FETCH NEXT FROM preserve_after INTO @Object;
    END;
    CLOSE preserve_after; DEALLOCATE preserve_after;
    SELECT @MetadataAfter=HASHBYTES('SHA2_256',(SELECT object_id,temporal_type,history_table_id,
        history_retention_period,history_retention_period_unit FROM sys.tables
        WHERE object_id NOT IN(OBJECT_ID(N'Migration.MigrationRuns'),@HistoryObjectId)
        ORDER BY object_id FOR JSON PATH,INCLUDE_NULL_VALUES));
    IF @MetadataBefore<>@MetadataAfter THROW 53011,'Another table temporal configuration changed. Rolling back.',1;
    COMMIT;
    SELECT N'RUN_HISTORY_CLEARED_CAPTURE_OFF' AS Result,Id AS RunId,ProjectId,Status,JobId,CommittedUnits,
           @HistoryRows AS HistoryRowsRemoved FROM Migration.MigrationRuns WHERE Id=2 AND ProjectId=3;
    SELECT name,CAST(size/128.0 AS decimal(18,2)) AS AllocatedMB,
           CAST(FILEPROPERTY(name,'SpaceUsed')/128.0 AS decimal(18,2)) AS UsedMB,
           CAST((size-FILEPROPERTY(name,'SpaceUsed'))/128.0 AS decimal(18,2)) AS FreeInsideFileMB
    FROM sys.database_files WHERE type=0;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
