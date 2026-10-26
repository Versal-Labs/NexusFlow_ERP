-- Follow-up to the user-reported successful 84-mapping save for project 3, version 4.
-- User-supplied current targets: WH_MAIN / ID 2 / Main Warehouse / active / no override;
-- 1151 / ID 5027 / Cheque in Hand / active Asset posting account.
-- Saves three JSON settings and fresh audit rows. No master data or balances change.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

DECLARE @ProjectId int=3, @ExpectedVersion int=4;
DECLARE @Old nvarchar(max),@New nvarchar(max),@Now datetime2(7)=SYSUTCDATETIME();
DECLARE @Actor nvarchar(450)=LEFT(CONCAT(N'SQL:',ORIGINAL_LOGIN()),450);
DECLARE @LockResult int,@NewAuditId int;
IF DB_NAME()<>N'NexusGarnetProd'
 OR LOWER(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')))
    NOT IN(N'versalapps',N'versalapps.database.windows.net')
    THROW 51000,'Wrong server/database. Select NexusGarnetProd on versalapps.',1;
IF @@TRANCOUNT<>0 THROW 51000,'Run outside an existing transaction.',1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @Resource nvarchar(255)=CONCAT(N'NexusFlow.Migration.',@ProjectId);
    EXEC @LockResult=sys.sp_getapplock @Resource=@Resource,@LockMode='Exclusive',
         @LockOwner='Transaction',@LockTimeout=15000;
    IF @LockResult<0 THROW 51000,'Migration project is busy. Retry after its current work completes.',1;

    SELECT @Old=SettingsJson FROM Migration.MigrationProjects WITH(UPDLOCK,HOLDLOCK)
    WHERE Id=@ProjectId AND SourceSystem=N'TESTTTT' AND Status=N'Preparing'
      AND Version=@ExpectedVersion AND Currency=N'LKR'
      AND OpeningDate=CONVERT(date,'2026-10-01') AND ClosingDate=CONVERT(date,'2026-09-30');
    IF @Old IS NULL OR ISJSON(@Old)<>1
        THROW 51000,'Expected project 3 at Preparing/version 4 was not found. Check the current version; do not bypass this guard.',1;
    IF EXISTS(SELECT 1 FROM Migration.MigrationRuns WITH(UPDLOCK,HOLDLOCK)
              WHERE ProjectId=@ProjectId AND Status IN(N'Queued',N'Running'))
        THROW 51000,'An import run is active. Wait for it to finish before changing settings.',1;
    IF EXISTS(SELECT 1 FROM Migration.MigrationUnits WITH(UPDLOCK,HOLDLOCK)
              WHERE ProjectId=@ProjectId AND (Module IS NULL OR Module NOT IN(N'Accounts',N'CustomerMaster',N'SupplierMaster')))
        THROW 51000,'Posting beyond the master stage has started. Review before changing these settings.',1;
    IF NOT EXISTS(SELECT 1 FROM Master.Warehouses WITH(HOLDLOCK)
                  WHERE Id=2 AND Code=N'WH_MAIN' AND Name=N'Main Warehouse'
                    AND IsActive=1 AND OverrideInventoryAccountId IS NULL)
        THROW 51000,'Warehouse 2 differs from the supplied WH_MAIN target or has an inventory override.',1;
    IF NOT EXISTS(SELECT 1 FROM Finance.Accounts WITH(HOLDLOCK)
                  WHERE Id=5027 AND Code=N'1151' AND Name=N'Cheque in Hand'
                    AND Type=1 AND IsActive=1 AND IsTransactionAccount=1)
        THROW 51000,'Account 5027 is not the supplied active Asset posting account 1151 / Cheque in Hand.',1;

    IF NOT EXISTS(SELECT 1 FROM OPENJSON(@Old) WHERE [key]=N'Mappings' AND [type]=5)
       OR NOT EXISTS(SELECT 1 FROM OPENJSON(@Old) WHERE [key]=N'Defaults' AND [type]=5)
        THROW 51000,'Mappings and Defaults must already be JSON objects.',1;
    IF EXISTS(SELECT 1 FROM OPENJSON(@Old,'$.Mappings') WHERE [key]=N'Warehouse' AND [type]<>5)
        THROW 51000,'Existing Warehouse mappings are not a JSON object.',1;

    -- Refuse conflicting choices, including an explicit account mapping overriding the CSV code.
    IF EXISTS(SELECT 1 FROM OPENJSON(@Old,'$.Mappings.Warehouse') WHERE [key]=N'WH_MAIN'
              AND ([type]<>2 OR TRY_CONVERT(int,[value]) IS NULL OR TRY_CONVERT(int,[value])<>2))
       OR EXISTS(SELECT 1 FROM OPENJSON(@Old,'$.Mappings.Account') WHERE [key]=N'1151'
              AND ([type]<>2 OR TRY_CONVERT(int,[value]) IS NULL OR TRY_CONVERT(int,[value])<>5027))
       OR EXISTS(SELECT 1 FROM OPENJSON(@Old,'$.Defaults') WHERE [key]=N'StockWarehouse'
              AND ([type]<>2 OR TRY_CONVERT(int,[value]) IS NULL OR TRY_CONVERT(int,[value]) NOT IN(0,2)))
       OR EXISTS(SELECT 1 FROM OPENJSON(@Old,'$.Defaults') WHERE [key]=N'SafeAccount'
              AND ([type]<>2 OR TRY_CONVERT(int,[value]) IS NULL OR TRY_CONVERT(int,[value]) NOT IN(0,5027)))
        THROW 51000,'A saved warehouse/account choice conflicts with the supplied targets. Nothing was overwritten.',1;

    SET @New=@Old;
    IF JSON_QUERY(@New,'$.Mappings.Warehouse') IS NULL
        SET @New=JSON_MODIFY(@New,'$.Mappings.Warehouse',JSON_QUERY(N'{}'));
    SET @New=JSON_MODIFY(@New,'$.Mappings.Warehouse.WH_MAIN',2);
    SET @New=JSON_MODIFY(@New,'$.Defaults.StockWarehouse',2);
    SET @New=JSON_MODIFY(@New,'$.Defaults.SafeAccount',5027);

    UPDATE Migration.MigrationProjects
       SET SettingsJson=@New,Version=Version+1,ApprovedHash=NULL,ApprovedAt=NULL,ApprovedBy=NULL,
           LastModifiedAt=@Now,LastModifiedBy=@Actor
    WHERE Id=@ProjectId AND Version=@ExpectedVersion AND Status=N'Preparing';
    IF @@ROWCOUNT<>1 THROW 51000,'Project changed concurrently. Nothing was saved.',1;
    DECLARE @Detail nvarchar(max)=CONCAT(
        N'Operator-run SQL: WH_MAIN -> warehouse 2 (Main Warehouse, no inventory override); ',
        N'StockWarehouse=2; SafeAccount=5027 (1151 / Cheque in Hand). ',
        N'User-supplied current target results dated 2026-10-20. Version 4 -> 5. ',
        N'Existing mappings/defaults/evidence preserved. No master or balance changes. ',
        N'Before settings SHA256=',CONVERT(varchar(64),HASHBYTES('SHA2_256',@Old),2),
        N'; after=',CONVERT(varchar(64),HASHBYTES('SHA2_256',@New),2),N'.');
    INSERT Migration.MigrationAudit(ProjectId,Action,Detail,CreatedAt,CreatedBy)
    VALUES(@ProjectId,N'Mappings',@Detail,@Now,@Actor);
    SET @NewAuditId=CONVERT(int,SCOPE_IDENTITY());
    INSERT [System].AuditLogs(UserId,EntityName,Details,[Timestamp],Action,IPAddress)
    VALUES(@Actor,N'Migration.MigrationProjects',CONCAT(@Detail,N' MigrationAuditId=',@NewAuditId),
           @Now,N'MIGRATION_MAPPINGS_SQL',NULL);
    COMMIT;

    SELECT N'SAVED WAREHOUSE AND CHEQUE SETTINGS' AS Result,Id AS ProjectId,Version,
           JSON_VALUE(SettingsJson,'$.Mappings.Warehouse.WH_MAIN') AS WarehouseMapping,
           JSON_VALUE(SettingsJson,'$.Defaults.StockWarehouse') AS StockWarehouse,
           JSON_VALUE(SettingsJson,'$.Defaults.SafeAccount') AS SafeAccount,
           @NewAuditId AS NewMigrationAuditId
    FROM Migration.MigrationProjects WHERE Id=@ProjectId;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
