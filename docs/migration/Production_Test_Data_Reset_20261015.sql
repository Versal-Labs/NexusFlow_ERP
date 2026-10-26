/* NexusFlow production test-transaction reset — 15 October 2026
   Target: versalapps.database.windows.net / NexusGarnetProd
   User confirmed: 12 September journals and one payment are disposable test data.

   PREVIEW: reports the proposed current-table scope; changes no application data.
   REHEARSE: deletes inside a transaction, verifies preservation, then ROLLS BACK.
   COMMIT: requires the hash from a successful rehearsal of the same position.

   Stop the existing IIS site AND its pool, other writers and job workers before
   REHEARSE/COMMIT. First create/verify a recoverable Azure database copy and keep
   the existing instance/configuration/keys/storage backup. Review PREVIEW counts:
   unrecognized business rows are NOT automatically established to be test data.

   Retains users, roles, settings, accounts, customers, suppliers, products/SKUs,
   BOMs, lookup/bank/warehouse definitions, financial periods, numbering, audit,
   payroll/HR and Hangfire definitions/history. Account Balance is reset to zero.
   Current sales/purchase/production/stock/treasury/opening records are removed.
   Temporal versioning and existing history remain enabled/retained; current-row
   deletion and balance updates add ordinary history. No identity reseeding,
   constraint disabling, TRUNCATE, history deletion or persistent helper objects.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

DECLARE @Mode varchar(10) = 'PREVIEW'; -- PREVIEW, REHEARSE, COMMIT
DECLARE @BackupReference nvarchar(400) = N''; -- Verified completed Azure copy/restore reference
DECLARE @WritersStopped bit = 0; -- Set 1 only after stopping the site/pool/other workers
DECLARE @ReviewedTestData nvarchar(80) = N''; -- Set ALL_REVIEWED_BUSINESS_ROWS_ARE_TEST_DATA
DECLARE @RehearsalHash varchar(64) = ''; -- COMMIT: paste SnapshotHash from successful REHEARSE

DECLARE @ExpectedDatabase sysname = N'NexusGarnetProd';
DECLARE @ExpectedServer sysname = N'versalapps';
DECLARE @ActualServer nvarchar(128) = CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));
IF DB_NAME() <> @ExpectedDatabase OR
   LOWER(@ActualServer) NOT IN (LOWER(@ExpectedServer),LOWER(@ExpectedServer + N'.database.windows.net'))
    THROW 51000, 'Unexpected database/server. Refusing reset.', 1;
IF @@TRANCOUNT <> 0 THROW 51001, 'Use a fresh query session with no open transaction.', 1;
IF @Mode NOT IN ('PREVIEW','REHEARSE','COMMIT') THROW 51002, 'Invalid mode.', 1;

DROP TABLE IF EXISTS #ResetScope;
DROP TABLE IF EXISTS #ResetSnapshot;
CREATE TABLE #ResetScope
(
    SchemaName sysname NOT NULL, TableName sysname NOT NULL,
    ObjectId int NULL, DeleteOrder int NULL, RowsBefore bigint NULL,
    PRIMARY KEY (SchemaName,TableName)
);
INSERT #ResetScope (SchemaName,TableName) VALUES
(N'Finance',N'JournalEntries'),(N'Finance',N'JournalLines'),
(N'Finance',N'PaymentTransactions'),(N'Finance',N'PaymentAllocations'),
(N'Finance',N'ChequeRegister'),(N'Finance',N'BankReconciliations'),
(N'Finance',N'CustomerMigrationBatches'),(N'Finance',N'CustomerCreditApplications'),
(N'Finance',N'SupplierCreditApplications'),(N'Finance',N'OpeningBankItems'),
(N'Sales',N'SalesInvoices'),(N'Sales',N'SalesInvoiceItems'),
(N'Sales',N'SalesOrders'),(N'Sales',N'SalesOrderItems'),
(N'Sales',N'CreditNotes'),(N'Sales',N'CreditNoteItems'),
(N'Sales',N'CommissionLedger'),(N'Sales',N'CustomerOpeningItems'),(N'Sales',N'CustomerDebitMemos'),
(N'Purchasing',N'PurchaseOrders'),(N'Purchasing',N'PurchaseOrderItems'),
(N'Purchasing',N'GRNs'),(N'Purchasing',N'GRNItems'),
(N'Purchasing',N'SupplierBills'),(N'Purchasing',N'SupplierBillItems'),(N'Purchasing',N'SupplierOpeningItems'),
(N'Inventory',N'StockLayers'),(N'Inventory',N'StockTransactions'),
(N'Inventory',N'StockTakes'),(N'Inventory',N'StockTakeItems'),
(N'Inventory',N'ProductionOrders'),(N'Inventory',N'ProductionOrderComponents'),
(N'Inventory',N'ProductionOrderRevisions'),(N'Inventory',N'ProductionMaterialMovements'),
(N'Inventory',N'ProductionMaterialMovementLines'),(N'Inventory',N'ProductionReceipts'),
(N'Inventory',N'ProductionReceiptConsumptions'),(N'Inventory',N'ProductionSupplierClaims'),
(N'Migration',N'MigrationProjects'),(N'Migration',N'MigrationDatasets'),
(N'Migration',N'MigrationScopes'),(N'Migration',N'MigrationUnits'),
(N'Migration',N'MigrationOrigins'),(N'Migration',N'MigrationRuns'),(N'Migration',N'MigrationAudit');
UPDATE #ResetScope SET ObjectId = OBJECT_ID(QUOTENAME(SchemaName)+N'.'+QUOTENAME(TableName),N'U');
IF EXISTS (SELECT 1 FROM #ResetScope WHERE ObjectId IS NULL)
BEGIN
    SELECT SchemaName,TableName FROM #ResetScope WHERE ObjectId IS NULL;
    THROW 51003, 'Schema differs from the source-reviewed scope. Do not remove this guard.', 1;
END;

DECLARE @ObjectId int,@Schema sysname,@Table sysname,@Statement nvarchar(max),@Count bigint;
DECLARE scope_counts CURSOR LOCAL FAST_FORWARD FOR
    SELECT ObjectId,SchemaName,TableName FROM #ResetScope ORDER BY SchemaName,TableName;
OPEN scope_counts;
FETCH NEXT FROM scope_counts INTO @ObjectId,@Schema,@Table;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Statement = N'SELECT @n=COUNT_BIG(*) FROM '+QUOTENAME(@Schema)+N'.'+QUOTENAME(@Table)+N';';
    EXEC sys.sp_executesql @Statement,N'@n bigint OUTPUT',@n=@Count OUTPUT;
    UPDATE #ResetScope SET RowsBefore=@Count WHERE ObjectId=@ObjectId;
    FETCH NEXT FROM scope_counts INTO @ObjectId,@Schema,@Table;
END;
CLOSE scope_counts; DEALLOCATE scope_counts;
SELECT @Mode AS RequestedMode,DB_NAME() AS DatabaseName,@ActualServer AS ServerName;
SELECT SchemaName,TableName,RowsBefore AS CurrentRowsProposedForRemoval FROM #ResetScope ORDER BY SchemaName,TableName;
SELECT Module,COUNT_BIG(*) AS JournalCount,MIN([Date]) AS FirstDate,MAX([Date]) AS LastDate
FROM Finance.JournalEntries GROUP BY Module ORDER BY Module;
IF OBJECT_ID(N'HangFire.Job',N'U') IS NOT NULL
    EXEC sys.sp_executesql N'SELECT StateName,COUNT_BIG(*) AS JobCount FROM HangFire.Job GROUP BY StateName;';
IF @Mode = 'PREVIEW'
BEGIN
    SELECT N'PREVIEW ONLY: no application rows changed. Review every nonempty scope row. Back up and stop writers before rehearsal.' AS Result;
    RETURN;
END;
IF LEN(LTRIM(RTRIM(@BackupReference)))=0 OR @WritersStopped<>1 OR
   @ReviewedTestData<>N'ALL_REVIEWED_BUSINESS_ROWS_ARE_TEST_DATA'
    THROW 51004, 'Verified recovery reference, stopped writers and reviewed test-data scope are required.', 1;
IF @Mode='COMMIT' AND (LEN(@RehearsalHash)<>64 OR @RehearsalHash LIKE '%[^0-9A-Fa-f]%')
    THROW 51005, 'COMMIT requires SnapshotHash from a successful rehearsal.', 1;

CREATE TABLE #ResetSnapshot
(
    ObjectId int NOT NULL PRIMARY KEY, SchemaName sysname NOT NULL, TableName sysname NOT NULL,
    IsCleared bit NOT NULL, RowCountBefore bigint NULL, HashBefore varbinary(32) NULL,
    RowCountAfter bigint NULL, HashAfter varbinary(32) NULL
);
-- Exclude histories expected to grow from current-row deletes/account balance update.
-- All other user tables, including preserved master histories, are fingerprinted.
INSERT #ResetSnapshot(ObjectId,SchemaName,TableName,IsCleared)
SELECT t.object_id,s.name,t.name,CONVERT(bit,CASE WHEN c.ObjectId IS NULL THEN 0 ELSE 1 END)
FROM sys.tables AS t JOIN sys.schemas AS s ON s.schema_id=t.schema_id
LEFT JOIN #ResetScope AS c ON c.ObjectId=t.object_id
WHERE t.is_ms_shipped=0 AND
    NOT EXISTS (SELECT 1 FROM sys.tables AS parent
                WHERE parent.history_table_id=t.object_id
                  AND (parent.object_id IN (SELECT ObjectId FROM #ResetScope)
                       OR parent.object_id=OBJECT_ID(N'Finance.Accounts')));

DECLARE @Result nvarchar(80),@SnapshotHash varchar(64),@Report nvarchar(max),@SnapshotJson nvarchar(max);
DECLARE @Columns nvarchar(max),@Sort nvarchar(max),@DataHash varbinary(32),@Pass int,@LockResult int;
BEGIN TRY
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;
    EXEC @LockResult=sys.sp_getapplock @Resource=N'NexusFlow.ProductionTestReset',
        @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
    IF @LockResult<0 THROW 51006, 'Another reset is active.', 1;
    EXEC @LockResult=sys.sp_getapplock @Resource=N'NexusFlow.Migration.0',
        @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
    IF @LockResult<0 THROW 51006, 'Migration project creation is active. Stop and retry after maintenance is established.', 1;

    -- Lock every inspected current/history table for stable snapshots and preservation.
    DECLARE lock_tables CURSOR LOCAL FAST_FORWARD FOR
        SELECT SchemaName,TableName FROM #ResetSnapshot ORDER BY SchemaName,TableName;
    OPEN lock_tables; FETCH NEXT FROM lock_tables INTO @Schema,@Table;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Statement=N'SELECT @n=COUNT_BIG(*) FROM '+QUOTENAME(@Schema)+N'.'+QUOTENAME(@Table)+N' WITH (TABLOCKX,HOLDLOCK);';
        EXEC sys.sp_executesql @Statement,N'@n bigint OUTPUT',@n=@Count OUTPUT;
        FETCH NEXT FROM lock_tables INTO @Schema,@Table;
    END;
    CLOSE lock_tables; DEALLOCATE lock_tables;

    IF EXISTS (SELECT 1 FROM sys.triggers WHERE parent_class=1 AND is_disabled=0
               AND parent_id IN (SELECT ObjectId FROM #ResetSnapshot))
        THROW 51007, 'Enabled DML triggers require a separate side-effect review.', 1;
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
        THROW 51008, 'Disabled/untrusted foreign keys require review before this reset.', 1;
    IF EXISTS (SELECT 1 FROM Migration.MigrationProjects) OR EXISTS (SELECT 1 FROM Migration.MigrationUnits)
        THROW 51009, 'Migration state changed from the reported empty position. Stop and re-audit.', 1;
    IF (SELECT COUNT_BIG(*) FROM Finance.JournalEntries)<>12 OR
       (SELECT COUNT_BIG(*) FROM Finance.PaymentTransactions)<>1
        THROW 51010, 'Journal/payment counts changed from the user-reviewed 12/1 position.', 1;
    IF EXISTS
    (
        SELECT Module,COUNT_BIG(*) AS N FROM Finance.JournalEntries GROUP BY Module
        EXCEPT
        SELECT Module,N FROM (VALUES(N'Inventory',CONVERT(bigint,3)),(N'Production',CONVERT(bigint,6)),
            (N'Purchasing',CONVERT(bigint,1)),(N'Sales',CONVERT(bigint,1)),(N'Treasury',CONVERT(bigint,1))) AS e(Module,N)
    ) OR EXISTS (SELECT 1 FROM Finance.JournalEntries WHERE [Date]<'20260913' OR [Date]>='20260917')
        THROW 51011, 'Journal modules/dates differ from the reviewed September test position.', 1;
    IF EXISTS
    (
        SELECT 1 FROM Finance.Accounts AS a
        LEFT JOIN (SELECT AccountId,SUM(Debit-Credit) AS Movement FROM Finance.JournalLines GROUP BY AccountId) AS j
          ON j.AccountId=a.Id
        WHERE a.Balance<>COALESCE(j.Movement,0)
    )
        THROW 51012, 'Account balances include unexplained amounts outside the journal lines. Review before zeroing.', 1;
    IF OBJECT_ID(N'HangFire.Job',N'U') IS NOT NULL
    BEGIN
        DECLARE @PendingJobs bigint;
        EXEC sys.sp_executesql N'SELECT @n=COUNT_BIG(*) FROM HangFire.Job WHERE StateName IS NULL OR StateName NOT IN (N''Succeeded'',N''Deleted'');',
            N'@n bigint OUTPUT',@n=@PendingJobs OUTPUT;
        IF @PendingJobs<>0 THROW 51013, 'Unfinished/failed background jobs need review/cancellation before reset; definitions are retained.', 1;
    END;

    -- Never cascade into a retained table. Conservatively block nonempty retained children.
    DECLARE retained_dependencies CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT OBJECT_SCHEMA_NAME(fk.parent_object_id),OBJECT_NAME(fk.parent_object_id)
        FROM sys.foreign_keys AS fk
        WHERE fk.referenced_object_id IN (SELECT ObjectId FROM #ResetScope)
          AND fk.parent_object_id NOT IN (SELECT ObjectId FROM #ResetScope);
    OPEN retained_dependencies; FETCH NEXT FROM retained_dependencies INTO @Schema,@Table;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Statement=N'SELECT @n=COUNT_BIG(*) FROM '+QUOTENAME(@Schema)+N'.'+QUOTENAME(@Table)+N';';
        EXEC sys.sp_executesql @Statement,N'@n bigint OUTPUT',@n=@Count OUTPUT;
        IF @Count<>0
        BEGIN
            SELECT @Schema AS RetainedDependentSchema,@Table AS RetainedDependentTable,@Count AS RowsNeedingReview;
            THROW 51014, 'A nonempty retained table depends on transaction scope. No cascade/extra deletion is authorized.', 1;
        END;
        FETCH NEXT FROM retained_dependencies INTO @Schema,@Table;
    END;
    CLOSE retained_dependencies; DEALLOCATE retained_dependencies;

    -- Child-before-parent order derived from actual enabled foreign keys.
    DECLARE @Next int=0,@Leaf int;
    WHILE EXISTS (SELECT 1 FROM #ResetScope WHERE DeleteOrder IS NULL)
    BEGIN
        SET @Leaf=NULL;
        SELECT TOP(1) @Leaf=c.ObjectId FROM #ResetScope AS c WHERE c.DeleteOrder IS NULL
          AND NOT EXISTS
          (SELECT 1 FROM sys.foreign_keys AS fk JOIN #ResetScope AS child ON child.ObjectId=fk.parent_object_id
           WHERE fk.referenced_object_id=c.ObjectId AND fk.parent_object_id<>c.ObjectId AND child.DeleteOrder IS NULL)
        ORDER BY c.SchemaName,c.TableName;
        IF @Leaf IS NULL THROW 51015, 'Foreign-key cycle requires a separate reviewed plan. Constraints remain enabled.', 1;
        SET @Next+=1;
        UPDATE #ResetScope SET DeleteOrder=@Next WHERE ObjectId=@Leaf;
    END;

    -- Full SHA-256 fingerprints. Account definitions exclude only Balance,
    -- temporal period columns and rowversion changed by the deliberate balance update.
    SET @Pass=1;
    WHILE @Pass<=2
    BEGIN
        DECLARE snapshots CURSOR LOCAL FAST_FORWARD FOR
            SELECT ObjectId,SchemaName,TableName FROM #ResetSnapshot ORDER BY SchemaName,TableName;
        OPEN snapshots; FETCH NEXT FROM snapshots INTO @ObjectId,@Schema,@Table;
        WHILE @@FETCH_STATUS=0
        BEGIN
            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@ObjectId AND system_type_id IN (34,35,99,98,240,241))
                THROW 51016, 'An unsupported fingerprint column type requires review.', 1;
            SELECT @Columns=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),N',') WITHIN GROUP(ORDER BY c.column_id)
            FROM sys.columns AS c WHERE c.object_id=@ObjectId AND
                NOT (@ObjectId=OBJECT_ID(N'Finance.Accounts') AND
                    (c.name=N'Balance' OR c.system_type_id=189 OR
                     c.column_id IN (SELECT start_column_id FROM sys.periods WHERE object_id=@ObjectId
                                    UNION ALL SELECT end_column_id FROM sys.periods WHERE object_id=@ObjectId)));
            SELECT @Sort=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),N',') WITHIN GROUP(ORDER BY ic.key_ordinal)
            FROM sys.indexes AS i JOIN sys.index_columns AS ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns AS c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE i.object_id=@ObjectId AND i.is_primary_key=1;
            IF @Sort IS NULL SET @Sort=@Columns;
            SET @Statement=N'DECLARE @j nvarchar(max); SELECT @n=COUNT_BIG(*) FROM '+QUOTENAME(@Schema)+N'.'+QUOTENAME(@Table)+
                N'; SET @j=(SELECT '+@Columns+N' FROM '+QUOTENAME(@Schema)+N'.'+QUOTENAME(@Table)+N' ORDER BY '+@Sort+
                N' FOR JSON PATH,INCLUDE_NULL_VALUES); SET @h=HASHBYTES(''SHA2_256'',COALESCE(@j,N''[]''));';
            EXEC sys.sp_executesql @Statement,N'@n bigint OUTPUT,@h varbinary(32) OUTPUT',@n=@Count OUTPUT,@h=@DataHash OUTPUT;
            IF @Pass=1 UPDATE #ResetSnapshot SET RowCountBefore=@Count,HashBefore=@DataHash WHERE ObjectId=@ObjectId;
            ELSE UPDATE #ResetSnapshot SET RowCountAfter=@Count,HashAfter=@DataHash WHERE ObjectId=@ObjectId;
            FETCH NEXT FROM snapshots INTO @ObjectId,@Schema,@Table;
        END;
        CLOSE snapshots; DEALLOCATE snapshots;
        IF @Pass=1
        BEGIN
            SET @SnapshotJson=(SELECT SchemaName,TableName,RowCountBefore,CONVERT(varchar(64),HashBefore,2) AS DataHash
                FROM #ResetSnapshot WHERE IsCleared=1 ORDER BY SchemaName,TableName FOR JSON PATH);
            SET @SnapshotJson+= (SELECT Id,Balance FROM Finance.Accounts ORDER BY Id FOR JSON PATH);
            SET @SnapshotHash=CONVERT(varchar(64),HASHBYTES('SHA2_256',@SnapshotJson),2);
            IF @Mode='COMMIT' AND @SnapshotHash<>UPPER(@RehearsalHash)
                THROW 51017, 'Data changed since rehearsal. Back up and rehearse the new position; do not reuse the old hash.', 1;
            DECLARE delete_tables CURSOR LOCAL FAST_FORWARD FOR
                SELECT SchemaName,TableName FROM #ResetScope ORDER BY DeleteOrder;
            OPEN delete_tables; FETCH NEXT FROM delete_tables INTO @Schema,@Table;
            WHILE @@FETCH_STATUS=0
            BEGIN
                SET @Statement=N'DELETE FROM '+QUOTENAME(@Schema)+N'.'+QUOTENAME(@Table)+N';';
                EXEC sys.sp_executesql @Statement;
                FETCH NEXT FROM delete_tables INTO @Schema,@Table;
            END;
            CLOSE delete_tables; DEALLOCATE delete_tables;
            UPDATE Finance.Accounts SET Balance=0 WHERE Balance<>0;
        END;
        SET @Pass+=1;
    END;
    IF EXISTS (SELECT 1 FROM #ResetSnapshot WHERE IsCleared=1 AND RowCountAfter<>0)
        THROW 51018, 'Transaction rows remain. Rolling back.', 1;
    IF EXISTS (SELECT 1 FROM Finance.Accounts WHERE Balance<>0)
        THROW 51019, 'Nonzero account balances remain. Rolling back.', 1;
    IF EXISTS (SELECT 1 FROM #ResetSnapshot WHERE IsCleared=0
               AND (RowCountBefore<>RowCountAfter OR HashBefore<>HashAfter OR HashBefore IS NULL OR HashAfter IS NULL))
    BEGIN
        DECLARE @PreservationError nvarchar(2048);
        SELECT TOP(1) @PreservationError=N'Retained table fingerprint mismatch: '+QUOTENAME(SchemaName)+N'.'+QUOTENAME(TableName)+
            N'; before='+CONVERT(nvarchar(30),RowCountBefore)+N'; after='+CONVERT(nvarchar(30),RowCountAfter)+N'; hashes='+COALESCE(CONVERT(nvarchar(64),HashBefore,2),N'NULL')+N'/'+COALESCE(CONVERT(nvarchar(64),HashAfter,2),N'NULL')+N'. Rolling back.'
        FROM #ResetSnapshot WHERE IsCleared=0
            AND (RowCountBefore<>RowCountAfter OR HashBefore<>HashAfter OR HashBefore IS NULL OR HashAfter IS NULL)
        ORDER BY SchemaName,TableName;
        THROW 51020, @PreservationError, 1;
    END;
    SET @Report=(SELECT SchemaName,TableName,IsCleared,RowCountBefore,RowCountAfter,
                        CONVERT(varchar(64),HashBefore,2) AS HashBefore,CONVERT(varchar(64),HashAfter,2) AS HashAfter
                 FROM #ResetSnapshot ORDER BY IsCleared DESC,SchemaName,TableName FOR JSON PATH);
    IF @Mode='COMMIT'
    BEGIN
        COMMIT;
        SET @Result=N'COMMITTED';
    END
    ELSE
    BEGIN
        ROLLBACK;
        SET @Result=N'REHEARSED_ROLLED_BACK';
    END;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    SELECT @Result AS Result,@SnapshotHash AS SnapshotHash,@BackupReference AS RecoveryReference;
    SELECT SchemaName,TableName,IsCleared,RowCountBefore,RowCountAfter
    FROM OPENJSON(@Report) WITH
        (SchemaName nvarchar(128),TableName nvarchar(128),IsCleared bit,RowCountBefore bigint,RowCountAfter bigint)
    ORDER BY IsCleared DESC,SchemaName,TableName;
    SELECT N'Retained table fingerprints matched. Temporal history was retained; current table removals shown above are durable only for COMMITTED.' AS Verification;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    THROW;
END CATCH;