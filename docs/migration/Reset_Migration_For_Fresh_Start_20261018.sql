/*
  Fresh-start migration reset prepared 18 October 2026.
  Includes deletion of all seven Migration temporal histories in the same transaction.
  Target: versalapps.database.windows.net / NexusGarnetProd.
  Baseline: Data Migrate (project 2), run 1 Running, missing job 44,
  500 Posted units: 1 Accounts + 499 CustomerMaster; no financial postings.

  PREVIEW (default) changes no application data.
  REHEARSE performs the reset and preservation checks, then ROLLS BACK.
  COMMIT requires the successful rehearsal's SnapshotHash for the SAME data.

  Clears all CURRENT migration tables AND their histories for this sole project, and deletes ONLY
  master records proved to have been created by its import, with no later
  edits or retained dependencies. Pre-existing masters remain. No unrelated
  transactions are deleted. Physical uploads/source CSVs live outside SQL;
  this script reports their references but does not delete those files.
  Users/settings and other Hangfire jobs remain. No identity reseed.

  Before REHEARSE/COMMIT: stop ALL IIS sites/pools/worker processes and integrations for this target;
  confirm job 44 is missing in the actual configured Hangfire storage.
  No backup or backup reference is required by this script.
  Run the WHOLE file in SSMS against the hosted APPLICATION database.
  Do not use the 20261015 production test-transaction reset for this operation.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

DECLARE @Mode varchar(10) = 'PREVIEW'; -- PREVIEW, REHEARSE, COMMIT
DECLARE @AllWritersStopped bit = 0;
DECLARE @Job44ConfirmedMissing bit = 0;
DECLARE @RehearsalHash varchar(64) = '';

DECLARE @ExpectedDatabase sysname = N'NexusGarnetProd';
DECLARE @ExpectedServer sysname = N'versalapps';
DECLARE @ActualServer nvarchar(128) = CONVERT(nvarchar(128), SERVERPROPERTY('ServerName'));
IF DB_NAME() <> @ExpectedDatabase OR LOWER(@ActualServer) NOT IN
   (LOWER(@ExpectedServer), LOWER(@ExpectedServer + N'.database.windows.net'))
    THROW 51000, 'Wrong server/database. Refusing hosted migration reset.', 1;
IF @@TRANCOUNT <> 0 THROW 51001, 'Use a query session with no open transaction.', 1;
IF @Mode NOT IN ('PREVIEW','REHEARSE','COMMIT') THROW 51002, 'Invalid mode.', 1;
IF @Mode <> 'PREVIEW' AND (@AllWritersStopped <> 1 OR @Job44ConfirmedMissing <> 1)
    THROW 51003, 'Stopped writers and exact missing-job confirmation are required.', 1;
IF @Mode = 'COMMIT' AND (LEN(@RehearsalHash) <> 64 OR @RehearsalHash LIKE '%[^0-9A-Fa-f]%')
    THROW 51004, 'COMMIT requires SnapshotHash from successful REHEARSE.', 1;

DROP TABLE IF EXISTS #Scope;
DROP TABLE IF EXISTS #RemoveCustomers;
DROP TABLE IF EXISTS #RemoveAccounts;
DROP TABLE IF EXISTS #Snapshots;
CREATE TABLE #Scope (TableName sysname NOT NULL PRIMARY KEY, DeleteOrder int NOT NULL);
INSERT #Scope VALUES (N'MigrationOrigins',1),(N'MigrationRuns',2),(N'MigrationAudit',3),
    (N'MigrationScopes',4),(N'MigrationDatasets',5),(N'MigrationUnits',6),(N'MigrationProjects',7);
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

CREATE TABLE #RemoveCustomers (Id int NOT NULL PRIMARY KEY);
CREATE TABLE #RemoveAccounts (Id int NOT NULL PRIMARY KEY);
CREATE TABLE #Snapshots (ObjectName nvarchar(300) NOT NULL PRIMARY KEY,
    RowsBefore bigint NOT NULL, HashBefore varbinary(32) NOT NULL);

DECLARE @RunStarted datetime2(7), @StoppedAt datetime2(7), @LockResult int;
DECLARE @Table sysname, @Sql nvarchar(max), @Rows bigint, @Hash varbinary(32), @Json nvarchar(max);
DECLARE @SnapshotHash varchar(64), @Report nvarchar(max), @Result varchar(40);
DECLARE @History nvarchar(517), @Retention nvarchar(100);
DECLARE @CustomersBefore bigint, @AccountsBefore bigint;
DECLARE @RetainedCustomerHash varbinary(32), @RetainedAccountHash varbinary(32);

BEGIN TRY
    BEGIN TRANSACTION;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'NexusFlow.Migration.0',
        @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
    IF @LockResult < 0 THROW 51005, 'Migration creation is busy.', 1;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'NexusFlow.Migration.2',
        @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
    IF @LockResult < 0 THROW 51005, 'Migration project is busy.', 1;

    UPDATE x SET CurrentObjectId=t.object_id,HistoryObjectId=t.history_table_id,
        HistoryName=QUOTENAME(hs.name)+N'.'+QUOTENAME(h.name),
        RetentionValue=t.history_retention_period,RetentionUnit=t.history_retention_period_unit_desc
    FROM #HistoryTargets x
    LEFT JOIN sys.tables t ON t.object_id=OBJECT_ID(N'Migration.'+QUOTENAME(x.CurrentTable))
    LEFT JOIN sys.tables h ON h.object_id=t.history_table_id
    LEFT JOIN sys.schemas hs ON hs.schema_id=h.schema_id
    WHERE t.temporal_type=2 AND hs.name=N'Migration' AND h.name COLLATE DATABASE_DEFAULT=x.CurrentTable+N'_History';
    IF EXISTS (SELECT 1 FROM #HistoryTargets WHERE CurrentObjectId IS NULL OR HistoryObjectId IS NULL OR HistoryName IS NULL)
        THROW 51021, 'Expected seven enabled migration temporal tables and their exact history mappings.', 1;

    UPDATE #HistoryTargets SET RetentionClause=CASE
        WHEN RetentionValue=-1 THEN N'INFINITE'
        WHEN RetentionValue>0 AND RetentionUnit IN (N'DAY',N'DAYS',N'WEEK',N'WEEKS',N'MONTH',N'MONTHS',N'YEAR',N'YEARS')
            THEN CONVERT(nvarchar(20),RetentionValue)+N' '+RetentionUnit END;
    IF EXISTS (SELECT 1 FROM #HistoryTargets WHERE RetentionClause IS NULL)
        THROW 51021, 'Unrecognized retention configuration. No history removed.', 1;

    IF (SELECT COUNT(*) FROM Migration.MigrationProjects WITH (UPDLOCK,HOLDLOCK)) <> 1
       OR NOT EXISTS (SELECT 1 FROM Migration.MigrationProjects WHERE Id=2 AND Name=N'Data Migrate'
           AND SourceSystem=N'TESTTTT' AND Status=N'Preparing' AND FinalizedAt IS NULL
           AND ApprovedHash=N'6D98B942542541F7CFB2E9487EDFA8346BBE0C93BFB7C0EA1F12CD06D06E04F1')
        THROW 51006, 'Project differs from the reviewed sole project 2. Diagnose again.', 1;
    IF (SELECT COUNT(*) FROM Migration.MigrationRuns WITH (UPDLOCK,HOLDLOCK)) <> 1
       OR NOT EXISTS (SELECT 1 FROM Migration.MigrationRuns WHERE Id=1 AND ProjectId=2
           AND Status=N'Running' AND JobId=N'44' AND CommittedUnits=500 AND CompletedAt IS NULL
           AND LastModifiedAt=CONVERT(datetime2(7),'2026-10-15T16:15:24.3060948') AND Error IS NULL)
        THROW 51007, 'Run changed from the reviewed interrupted run. Diagnose again.', 1;
    SELECT @RunStarted=CreatedAt,@StoppedAt=LastModifiedAt FROM Migration.MigrationRuns WHERE Id=1;
    IF EXISTS (SELECT 1 FROM OPENJSON((SELECT SettingsJson FROM Migration.MigrationProjects WHERE Id=2),'$.Mappings.Customer'))
        THROW 51008, 'Explicit existing-customer mappings need a separate restore review.', 1;
    IF (SELECT COUNT(*) FROM Migration.MigrationUnits WITH (UPDLOCK,HOLDLOCK)) <> 500
       OR (SELECT COUNT(*) FROM Migration.MigrationUnits WHERE ProjectId=2 AND Module=N'Accounts' AND Status=N'Posted' AND JournalEntryId IS NULL) <> 1
       OR (SELECT COUNT(*) FROM Migration.MigrationUnits WHERE ProjectId=2 AND Module=N'CustomerMaster' AND Status=N'Posted' AND JournalEntryId IS NULL) <> 499
        THROW 51009, 'Units changed or financial/reversed units exist. Diagnose again.', 1;
    IF EXISTS (SELECT 1 FROM Finance.JournalEntries WITH (HOLDLOCK))
       OR EXISTS (SELECT 1 FROM Finance.Accounts WITH (HOLDLOCK) WHERE Balance<>0)
       OR EXISTS (SELECT 1 FROM Finance.CustomerMigrationBatches WITH (HOLDLOCK))
       OR EXISTS (SELECT 1 FROM Sales.CustomerOpeningItems WITH (HOLDLOCK))
       OR EXISTS (SELECT 1 FROM Purchasing.SupplierOpeningItems WITH (HOLDLOCK))
       OR EXISTS (SELECT 1 FROM Finance.OpeningBankItems WITH (HOLDLOCK))
        THROW 51010, 'Financial/opening data exists. This master-import reset will not erase it.', 1;
    IF EXISTS (SELECT 1 FROM Migration.MigrationOrigins WITH (UPDLOCK,HOLDLOCK)
        WHERE ProjectId<>2 OR IsCurrent<>1 OR NOT
            (RecordKind=N'Accounts' AND EntityKind=N'Account' OR RecordKind=N'CustomerMaster' AND EntityKind=N'Customer'))
       OR (SELECT COUNT(*) FROM Migration.MigrationOrigins WHERE ProjectId=2 AND EntityKind=N'Customer') <> 499
       OR (SELECT COUNT(DISTINCT EntityId) FROM Migration.MigrationOrigins WHERE ProjectId=2 AND EntityKind=N'Customer') <> 499
        THROW 51011, 'Origin records differ from the reviewed master-only import.', 1;
    IF EXISTS (SELECT 1 FROM Migration.MigrationOrigins o LEFT JOIN Migration.MigrationUnits u ON u.Id=o.UnitId
        WHERE u.Id IS NULL OR u.ProjectId<>2 OR u.Module<>o.RecordKind OR o.SourceSystem<>N'TESTTTT')
        THROW 51011, 'Origin ownership does not match the imported units.', 1;
    IF EXISTS (SELECT 1 FROM Migration.MigrationOrigins o LEFT JOIN Sales.Customers c ON c.Id=o.EntityId
        WHERE o.EntityKind=N'Customer' AND (c.Id IS NULL OR NOT EXISTS
            (SELECT 1 FROM Migration.MigrationUnits u CROSS APPLY OPENJSON(u.RowsJson) j
             WHERE u.Id=o.UnitId AND JSON_VALUE(j.value,'$.Data.Code')=c.LegacyCustomerCode)))
        THROW 51012, 'An imported customer is missing or its legacy identity changed.', 1;
    IF EXISTS (SELECT 1 FROM Migration.MigrationOrigins o LEFT JOIN Finance.Accounts a ON a.Id=o.EntityId
        WHERE o.EntityKind=N'Account' AND (a.Id IS NULL OR NOT EXISTS
            (SELECT 1 FROM Migration.MigrationUnits u CROSS APPLY OPENJSON(u.RowsJson) j
             WHERE u.Id=o.UnitId AND JSON_VALUE(j.value,'$.Data.Code')=a.Code)))
        THROW 51012, 'An imported account is missing or its code changed.', 1;

    -- A master linked by an origin may have existed BEFORE import: retain it.
    INSERT #RemoveCustomers
    SELECT DISTINCT c.Id FROM Sales.Customers c WITH (UPDLOCK,HOLDLOCK)
    JOIN Migration.MigrationOrigins o ON o.EntityId=c.Id AND o.EntityKind=N'Customer'
    WHERE c.CreatedAt>=@RunStarted;
    INSERT #RemoveAccounts
    SELECT DISTINCT a.Id FROM Finance.Accounts a WITH (UPDLOCK,HOLDLOCK)
    JOIN Migration.MigrationOrigins o ON o.EntityId=a.Id AND o.EntityKind=N'Account'
    WHERE a.CreatedAt>=@RunStarted;
    IF EXISTS (SELECT 1 FROM Sales.Customers c JOIN #RemoveCustomers d ON d.Id=c.Id
        WHERE c.CreatedAt>@StoppedAt OR COALESCE(c.CreatedBy,N'')<>N'SYSTEM'
           OR c.LastModifiedAt IS NOT NULL
           OR c.InternalNotes<>N'Migrated master; complete genuine missing details through customer maintenance.')
       OR EXISTS (SELECT 1 FROM Finance.Accounts a JOIN #RemoveAccounts d ON d.Id=a.Id
        WHERE a.CreatedAt>@StoppedAt OR COALESCE(a.CreatedBy,N'')<>N'SYSTEM' OR a.LastModifiedAt IS NOT NULL)
        THROW 51013, 'A master has later edits or unproven creation ownership. Review before removal.', 1;

    IF EXISTS (SELECT 1 FROM sys.triggers WHERE parent_class=1 AND is_disabled=0 AND
        (parent_id IN (OBJECT_ID(N'Sales.Customers'),OBJECT_ID(N'Finance.Accounts')) OR
         parent_id IN (SELECT OBJECT_ID(N'Migration.'+QUOTENAME(TableName)) FROM #Scope)))
        THROW 51014, 'Enabled triggers require a side-effect review.', 1;
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
        THROW 51014, 'Disabled/untrusted foreign keys require review.', 1;

    -- Block retained business rows referencing masters proposed for deletion.
    DECLARE @Fk int,@Child int,@Referenced int,@Predicate nvarchar(max),@Exclude nvarchar(max),@ChildName nvarchar(300);
    DECLARE dependencies CURSOR LOCAL FAST_FORWARD FOR
        SELECT object_id,parent_object_id,referenced_object_id FROM sys.foreign_keys
        WHERE referenced_object_id IN (OBJECT_ID(N'Sales.Customers'),OBJECT_ID(N'Finance.Accounts'));
    OPEN dependencies;
    FETCH NEXT FROM dependencies INTO @Fk,@Child,@Referenced;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SELECT @Predicate=STRING_AGG(CONVERT(nvarchar(max),N'c.'+QUOTENAME(COL_NAME(@Child,parent_column_id))+
            N'=m.'+QUOTENAME(COL_NAME(@Referenced,referenced_column_id))),N' AND ')
            FROM sys.foreign_key_columns WHERE constraint_object_id=@Fk;
        SET @ChildName=QUOTENAME(OBJECT_SCHEMA_NAME(@Child))+N'.'+QUOTENAME(OBJECT_NAME(@Child));
        SET @Exclude=CASE WHEN @Child=OBJECT_ID(N'Sales.Customers') THEN N'c.Id NOT IN (SELECT Id FROM #RemoveCustomers)'
            WHEN @Child=OBJECT_ID(N'Finance.Accounts') THEN N'c.Id NOT IN (SELECT Id FROM #RemoveAccounts)' ELSE N'1=1' END;
        SET @Sql=N'IF EXISTS (SELECT 1 FROM '+@ChildName+N' c WITH (HOLDLOCK) JOIN '+
            QUOTENAME(OBJECT_SCHEMA_NAME(@Referenced))+N'.'+QUOTENAME(OBJECT_NAME(@Referenced))+N' m ON '+@Predicate+
            N' JOIN '+CASE WHEN @Referenced=OBJECT_ID(N'Sales.Customers') THEN N'#RemoveCustomers' ELSE N'#RemoveAccounts' END+
            N' d ON d.Id=m.Id WHERE '+@Exclude+N') THROW 51015, ''Retained dependency in '+
            REPLACE(@ChildName,N'''',N'''''')+N'. No additional deletion is authorized by this script.'', 1;';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM dependencies INTO @Fk,@Child,@Referenced;
    END;
    CLOSE dependencies; DEALLOCATE dependencies;

    IF OBJECT_ID(N'HangFire.Job',N'U') IS NOT NULL
        EXEC sys.sp_executesql N'IF EXISTS (SELECT 1 FROM HangFire.Job WHERE Id=44 OR
            (InvocationData LIKE N''%MigrationExecutionJob%'' AND (StateName IS NULL OR StateName NOT IN (N''Succeeded'',N''Deleted''))))
            THROW 51016, ''Migration job still exists or unfinished migration jobs remain. Resolve job storage first.'', 1;';

    DECLARE snapshot_tables CURSOR LOCAL FAST_FORWARD FOR SELECT TableName FROM #Scope ORDER BY TableName;
    OPEN snapshot_tables; FETCH NEXT FROM snapshot_tables INTO @Table;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'SELECT @n=COUNT_BIG(*) FROM Migration.'+QUOTENAME(@Table)+N' WITH (UPDLOCK,HOLDLOCK); '+
            N'SELECT @h=HASHBYTES(''SHA2_256'',COALESCE((SELECT * FROM Migration.'+QUOTENAME(@Table)+N' ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES),N''[]''));';
        EXEC sys.sp_executesql @Sql,N'@n bigint OUTPUT,@h varbinary(32) OUTPUT',@n=@Rows OUTPUT,@h=@Hash OUTPUT;
        INSERT #Snapshots VALUES (N'Migration.'+@Table,@Rows,@Hash);
        FETCH NEXT FROM snapshot_tables INTO @Table;
    END;
    CLOSE snapshot_tables; DEALLOCATE snapshot_tables;
    SELECT @CustomersBefore=COUNT_BIG(*) FROM Sales.Customers WITH (UPDLOCK,HOLDLOCK);
    SELECT @AccountsBefore=COUNT_BIG(*) FROM Finance.Accounts WITH (UPDLOCK,HOLDLOCK);
    SELECT @RetainedCustomerHash=HASHBYTES('SHA2_256',COALESCE((SELECT * FROM Sales.Customers
        WHERE Id NOT IN (SELECT Id FROM #RemoveCustomers) ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES),N'[]'));
    SELECT @RetainedAccountHash=HASHBYTES('SHA2_256',COALESCE((SELECT * FROM Finance.Accounts
        WHERE Id NOT IN (SELECT Id FROM #RemoveAccounts) ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES),N'[]'));
    SELECT @Hash=HASHBYTES('SHA2_256',COALESCE((SELECT * FROM Sales.Customers ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES),N'[]'));
    INSERT #Snapshots VALUES (N'Sales.Customers',@CustomersBefore,@Hash);
    SELECT @Hash=HASHBYTES('SHA2_256',COALESCE((SELECT * FROM Finance.Accounts ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES),N'[]'));
    INSERT #Snapshots VALUES (N'Finance.Accounts',@AccountsBefore,@Hash);
    SET @Json=(SELECT ObjectName,RowsBefore,CONVERT(varchar(64),HashBefore,2) AS DataHash FROM #Snapshots ORDER BY ObjectName FOR JSON PATH);
    SET @SnapshotHash=CONVERT(varchar(64),HASHBYTES('SHA2_256',@Json),2);
    SELECT @Mode AS RequestedMode,DB_NAME() AS DatabaseName,@ActualServer AS ServerName;
    SELECT ObjectName,RowsBefore FROM #Snapshots ORDER BY ObjectName;
    SELECT N'New imported customers to remove' AS Scope,COUNT(*) AS RowsToRemove FROM #RemoveCustomers
    UNION ALL SELECT N'New imported accounts to remove',COUNT(*) FROM #RemoveAccounts;
    -- Original uploaded files are outside SQL. Report their exact references before clearing metadata.
    SELECT Id AS DatasetId,ProjectId,Module,FileName,StorageReference
    FROM Migration.MigrationDatasets ORDER BY Id;
    IF @Mode='PREVIEW'
    BEGIN
        ROLLBACK;
        SELECT 'PREVIEW_ONLY' AS Result,@SnapshotHash AS SnapshotHash;
        RETURN;
    END;
    IF @Mode='COMMIT' AND @SnapshotHash<>UPPER(@RehearsalHash)
        THROW 51017, 'Data changed since rehearsal. Rehearse the new position; do not reuse the old hash.', 1;

    DECLARE clear_tables CURSOR LOCAL FAST_FORWARD FOR SELECT TableName FROM #Scope ORDER BY DeleteOrder;
    OPEN clear_tables; FETCH NEXT FROM clear_tables INTO @Table;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'DELETE FROM Migration.'+QUOTENAME(@Table)+N';';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM clear_tables INTO @Table;
    END;
    CLOSE clear_tables; DEALLOCATE clear_tables;
    DELETE c FROM Sales.Customers c JOIN #RemoveCustomers d ON d.Id=c.Id;
    -- Delete newly imported account children before their imported parents.
    WHILE EXISTS (SELECT 1 FROM Finance.Accounts a JOIN #RemoveAccounts d ON d.Id=a.Id)
    BEGIN
        DELETE a FROM Finance.Accounts a JOIN #RemoveAccounts d ON d.Id=a.Id
        WHERE NOT EXISTS (SELECT 1 FROM Finance.Accounts child WHERE child.ParentAccountId=a.Id);
        IF @@ROWCOUNT=0 THROW 51018, 'Account dependency cycle detected. Reset rolled back.', 1;
    END;

    IF (SELECT COUNT_BIG(*) FROM Sales.Customers)<>@CustomersBefore-(SELECT COUNT_BIG(*) FROM #RemoveCustomers)
       OR (SELECT COUNT_BIG(*) FROM Finance.Accounts)<>@AccountsBefore-(SELECT COUNT_BIG(*) FROM #RemoveAccounts)
        THROW 51019, 'Unexpected master deletion count. Reset rolled back.', 1;
    SELECT @Hash=HASHBYTES('SHA2_256',COALESCE((SELECT * FROM Sales.Customers ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES),N'[]'));
    IF @Hash<>@RetainedCustomerHash THROW 51019, 'Retained customer fingerprint changed. Reset rolled back.', 1;
    SELECT @Hash=HASHBYTES('SHA2_256',COALESCE((SELECT * FROM Finance.Accounts ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES),N'[]'));
    IF @Hash<>@RetainedAccountHash THROW 51019, 'Retained account fingerprint changed. Reset rolled back.', 1;
    IF EXISTS (SELECT 1 FROM Migration.MigrationProjects) OR EXISTS (SELECT 1 FROM Migration.MigrationDatasets)
       OR EXISTS (SELECT 1 FROM Migration.MigrationScopes) OR EXISTS (SELECT 1 FROM Migration.MigrationRuns)
       OR EXISTS (SELECT 1 FROM Migration.MigrationUnits) OR EXISTS (SELECT 1 FROM Migration.MigrationOrigins)
       OR EXISTS (SELECT 1 FROM Migration.MigrationAudit)
        THROW 51020, 'Current migration data remains. Reset rolled back.', 1;
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
        THROW 51022, 'Temporal history configuration did not restore exactly. Rolling back.', 1;

    DECLARE verify_empty_history CURSOR LOCAL FAST_FORWARD FOR
        SELECT HistoryName FROM #HistoryTargets ORDER BY CurrentTable;
    OPEN verify_empty_history; FETCH NEXT FROM verify_empty_history INTO @History;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'IF EXISTS (SELECT 1 FROM '+@History+N') THROW 51023, ''Migration history remains. Reset rolled back.'', 1;';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM verify_empty_history INTO @History;
    END;
    CLOSE verify_empty_history; DEALLOCATE verify_empty_history;

    SET @Report=(SELECT ObjectName,RowsBefore FROM #Snapshots ORDER BY ObjectName FOR JSON PATH);
    IF @Mode='COMMIT'
    BEGIN
        COMMIT;
        SET @Result='COMMITTED';
    END
    ELSE
    BEGIN
        ROLLBACK;
        SET @Result='REHEARSED_ROLLED_BACK';
    END;
    SELECT @Result AS Result,@SnapshotHash AS SnapshotHash;
    SELECT ObjectName,RowsBefore FROM OPENJSON(@Report) WITH (ObjectName nvarchar(300),RowsBefore bigint);
    SELECT N'All seven current migration tables and all seven migration histories cleared in the reset transaction. Only proved import-created masters were removed; retained master fingerprints matched. History tracking restored with original bindings and retention. Physical uploads remain outside SQL. Changes are durable only for COMMITTED.' AS Verification;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
