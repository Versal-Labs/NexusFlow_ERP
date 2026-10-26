-- NexusFlow: SAVE VERIFIED PILOT MAPPINGS AND DEFAULTS.
-- Operator-run write script. This task has NOT run it against Azure.
-- Target: versalapps.database.windows.net / NexusGarnetProd.
-- NEW sole project / any project name / TESTTTT / Preparing / version 3 after uploading 01_Masters.zip.
-- Resolves the new ProjectId automatically; never assumes the old ID 2.
-- Saves 84 confirmed mappings; adds fresh migration/system audit rows.
-- DOES NOT create masters, change account/warehouse setup, import or post.
-- Earlier hosted checks found WH_MAIN missing and 1151 named Salary Advances Receivable.
-- These unresolved choices require a fresh check before Cheques/Stock/TB.
-- Therefore Warehouse/StockWarehouse/SafeAccount remain unset.
-- Refresh the entire browser page afterwards before saving any UI settings. Do not post Cheques/Stock/TB until resolved.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

DECLARE @ExpectedDatabase sysname=N'NexusGarnetProd';
DECLARE @ExpectedServer sysname=N'versalapps';
DECLARE @ProjectId int=NULL, @ExpectedVersion int=3; -- NULL selects the sole new project.
DECLARE @Actor nvarchar(450)=LEFT(CONCAT(N'SQL:',ORIGINAL_LOGIN()),450);
DECLARE @Now datetime2(7)=SYSUTCDATETIME();
DECLARE @Old nvarchar(max), @New nvarchar(max), @Version int, @Status nvarchar(24);
DECLARE @NewAuditId int, @LockResult int;

IF DB_NAME()<>@ExpectedDatabase
    THROW 51000,'Wrong database. Select NexusGarnetProd.',1;
IF LOWER(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')))
     NOT IN (@ExpectedServer,@ExpectedServer+N'.database.windows.net')
    THROW 51000,'Wrong server. This script targets versalapps.',1;
IF @@TRANCOUNT<>0
    THROW 51000,'Run this script outside an existing transaction.',1;

DECLARE @Desired table
(
    MappingType nvarchar(40) NOT NULL,
    LegacyCode nvarchar(100) NOT NULL,
    TargetId int NOT NULL,
    TargetCode nvarchar(200) NOT NULL,
    PRIMARY KEY(MappingType,LegacyCode)
);
INSERT INTO @Desired(MappingType,LegacyCode,TargetId,TargetCode)
SELECT [Type],Code,TargetId,TargetCode
FROM OPENJSON(N'[{"Type":"BankBranch","Code":"7010-058","TargetId":1,"TargetCode":"7010-001"},{"Type":"BankBranch","Code":"7010-501","TargetId":1,"TargetCode":"7010-001"},{"Type":"BankBranch","Code":"7056-006","TargetId":84,"TargetCode":"7056-006"},{"Type":"BankBranch","Code":"7056-152","TargetId":79,"TargetCode":"7056-001"},{"Type":"BankBranch","Code":"7083-064","TargetId":109,"TargetCode":"7083-001"},{"Type":"BankBranch","Code":"7083-094","TargetId":109,"TargetCode":"7083-001"},{"Type":"BankBranch","Code":"7135-040","TargetId":41,"TargetCode":"7135-001"},{"Type":"BankBranch","Code":"7135-123","TargetId":41,"TargetCode":"7135-001"},{"Type":"BankBranch","Code":"7135-200","TargetId":41,"TargetCode":"7135-001"},{"Type":"BankBranch","Code":"7135-329","TargetId":41,"TargetCode":"7135-001"},{"Type":"BankBranch","Code":"7162-012","TargetId":224,"TargetCode":"7162-012"},{"Type":"BankBranch","Code":"7278-929","TargetId":159,"TargetCode":"7278-001"},{"Type":"BankBranch","Code":"7287-085","TargetId":189,"TargetCode":"7287-001"},{"Type":"BankBranch","Code":"7311-085","TargetId":287,"TargetCode":"7311-001"},{"Type":"BankBranch","Code":"7454-026","TargetId":272,"TargetCode":"7454-001"},{"Type":"BankBranch","Code":"7463-008","TargetId":319,"TargetCode":"7463-008"},{"Type":"BankBranch","Code":"7463-021","TargetId":312,"TargetCode":"7463-001"},{"Type":"Brand","Code":"UNBRANDED","TargetId":5,"TargetCode":"UNBRANDED"},{"Type":"Category","Code":"ACCESSORIES","TargetId":37,"TargetCode":"Accessories"},{"Type":"Category","Code":"BLOUSE","TargetId":21,"TargetCode":"BLOUSE"},{"Type":"Category","Code":"CASUAL TROUSER","TargetId":12,"TargetCode":"CASUAL TROUSER"},{"Type":"Category","Code":"COOL KHAKIES","TargetId":31,"TargetCode":"COOL KHAKIES"},{"Type":"Category","Code":"DENIM BOYS TROUSER","TargetId":34,"TargetCode":"DENIM BOYS TROUSER"},{"Type":"Category","Code":"DENIM FROCK","TargetId":32,"TargetCode":"DENIM FROCK"},{"Type":"Category","Code":"DENIM GENTS TROUSER","TargetId":33,"TargetCode":"DENIM GENTS TROUSER"},{"Type":"Category","Code":"DENIM JACKET","TargetId":26,"TargetCode":"DENIM JACKET"},{"Type":"Category","Code":"DENIM LADIES TROUSER","TargetId":18,"TargetCode":"DENIM LADIES TROUSER"},{"Type":"Category","Code":"DENIM SHORTS","TargetId":30,"TargetCode":"DENIM SHORTS"},{"Type":"Category","Code":"DENIM SKIRT","TargetId":27,"TargetCode":"DENIM SKIRT"},{"Type":"Category","Code":"FROCK","TargetId":23,"TargetCode":"FROCK"},{"Type":"Category","Code":"GARNET CASUAL WEAR","TargetId":28,"TargetCode":"GARNET CASUAL WEAR"},{"Type":"Category","Code":"GLAMOUR","TargetId":14,"TargetCode":"GLAMOUR"},{"Type":"Category","Code":"H-DIAZ COTTON TROUSER","TargetId":25,"TargetCode":"H-DIAZ COTTON TROUSER"},{"Type":"Category","Code":"JOGGER PANT","TargetId":29,"TargetCode":"JOGGER PANT"},{"Type":"Category","Code":"MSH","TargetId":1,"TargetCode":"MSH"},{"Type":"Category","Code":"OFFICE SHIRT REGULAR FIT","TargetId":20,"TargetCode":"OFFICE SHIRT REGULAR FIT"},{"Type":"Category","Code":"OFFICE SHIRT SIDE OPEN","TargetId":16,"TargetCode":"OFFICE SHIRT SIDE OPEN"},{"Type":"Category","Code":"OFFICE SHIRT SLIM FIT","TargetId":15,"TargetCode":"OFFICE SHIRT SLIM FIT"},{"Type":"Category","Code":"OFFICIAL SLIM FIT TROUSER","TargetId":19,"TargetCode":"OFFICIAL SLIM FIT TROUSER"},{"Type":"Category","Code":"PUTLAM CASUAL SHIRT","TargetId":11,"TargetCode":"PUTLAM CASUAL SHIRT"},{"Type":"Category","Code":"SKIRT","TargetId":24,"TargetCode":"SKIRT"},{"Type":"Category","Code":"SLIM FIT","TargetId":9,"TargetCode":"SLIM FIT"},{"Type":"Category","Code":"T SHIRT","TargetId":17,"TargetCode":"T SHIRT"},{"Type":"Category","Code":"TOMMY TROUSER","TargetId":13,"TargetCode":"TOMMY TROUSER"},{"Type":"Category","Code":"TOP","TargetId":22,"TargetCode":"TOP"},{"Type":"Category","Code":"VESTI SET","TargetId":10,"TargetCode":"VESTI SET"},{"Type":"CustomerGroup","Code":"TRADE","TargetId":11,"TargetCode":"TRADE"},{"Type":"PaymentTerm","Code":"NET30","TargetId":14,"TargetCode":"NET 30 DAYS"},{"Type":"PriceLevel","Code":"STANDARD TIER","TargetId":13,"TargetCode":"STANDARD TIER"},{"Type":"Stock","Code":",JH,GH","TargetId":649,"TargetCode":",jh,gh"},{"Type":"Stock","Code":",LIYUK","TargetId":834,"TargetCode":",liyuk"},{"Type":"Stock","Code":"AFE","TargetId":636,"TargetCode":"afe"},{"Type":"Stock","Code":"AWE","TargetId":716,"TargetCode":"awe"},{"Type":"Stock","Code":"BFDB","TargetId":863,"TargetCode":"bfdb"},{"Type":"Stock","Code":"BRH","TargetId":799,"TargetCode":"brh"},{"Type":"Stock","Code":"ERT","TargetId":908,"TargetCode":"ert"},{"Type":"Stock","Code":"FGFG","TargetId":661,"TargetCode":"fgfg"},{"Type":"Stock","Code":"FSDF","TargetId":844,"TargetCode":"fsdf"},{"Type":"Stock","Code":"GDS","TargetId":936,"TargetCode":"gds"},{"Type":"Stock","Code":"GVC","TargetId":924,"TargetCode":"gvc"},{"Type":"Stock","Code":"HDFG","TargetId":609,"TargetCode":"hdfg"},{"Type":"Stock","Code":"HGTJHGF","TargetId":610,"TargetCode":"hgtjhgf"},{"Type":"Stock","Code":"HYT","TargetId":637,"TargetCode":"hyt"},{"Type":"Stock","Code":"JHTRGF","TargetId":613,"TargetCode":"jhtrgf"},{"Type":"Stock","Code":"JKYT","TargetId":815,"TargetCode":"jkyt"},{"Type":"Stock","Code":"JUJT","TargetId":735,"TargetCode":"jujt"},{"Type":"Stock","Code":"LKUI","TargetId":783,"TargetCode":"lkui"},{"Type":"Stock","Code":"OKYUIK","TargetId":899,"TargetCode":"okyuik"},{"Type":"Stock","Code":"RFGRG","TargetId":612,"TargetCode":"rfgrg"},{"Type":"Stock","Code":"RTYER","TargetId":615,"TargetCode":"rtyer"},{"Type":"Stock","Code":"RYTJRT","TargetId":885,"TargetCode":"rytjrt"},{"Type":"Stock","Code":"SDASF","TargetId":611,"TargetCode":"sdasf"},{"Type":"Stock","Code":"SDF","TargetId":614,"TargetCode":"sdf"},{"Type":"Stock","Code":"SRG","TargetId":699,"TargetCode":"srg"},{"Type":"Stock","Code":"STHFG","TargetId":671,"TargetCode":"sthfg"},{"Type":"Stock","Code":"STHSDFG","TargetId":679,"TargetCode":"sthsdfg"},{"Type":"Stock","Code":"VBCV","TargetId":893,"TargetCode":"vbcv"},{"Type":"Stock","Code":"WEFD","TargetId":791,"TargetCode":"wefd"},{"Type":"Stock","Code":"WEFWE","TargetId":829,"TargetCode":"wefwe"},{"Type":"Stock","Code":"WER","TargetId":928,"TargetCode":"wer"},{"Type":"Stock","Code":"WRE","TargetId":764,"TargetCode":"wre"},{"Type":"Stock","Code":"YJT","TargetId":706,"TargetCode":"yjt"},{"Type":"SupplierGroup","Code":"DOM","TargetId":12,"TargetCode":"DOM"},{"Type":"Unit","Code":"PCS","TargetId":1,"TargetCode":"Pcs"}]')
WITH([Type] nvarchar(40),Code nvarchar(100),TargetId int,TargetCode nvarchar(200));
IF (SELECT COUNT(*) FROM @Desired)<>84
    THROW 51000,'Expected exactly 84 verified mapping requests.',1;

DECLARE @Defaults nvarchar(max)=N'{"ReceivableAccount":1001,"RevenueAccount":9,"PayableAccount":2001,"ExpenseAccount":5001,"CustomerGroup":11,"SupplierGroup":12,"PriceLevel":13,"CustomerPaymentTerm":14,"SupplierPaymentTerm":14,"StockUnit":1}';
DECLARE @RequiredAccounts table(Id int,Code nvarchar(20),AccountType int);
INSERT INTO @RequiredAccounts VALUES
 (1001,N'1120',1),(9,N'4110',4),(2001,N'2110',2),(5001,N'5110',5),(3002,N'3200',3);

BEGIN TRY
    BEGIN TRANSACTION;
    EXEC @LockResult=sys.sp_getapplock @Resource=N'NexusFlow.Migration.0',@LockMode='Exclusive',
         @LockOwner='Transaction',@LockTimeout=15000;
    IF @LockResult<0 THROW 51000,'Migration creation is busy. Refresh and retry.',1;
    IF (SELECT COUNT(*) FROM Migration.MigrationProjects WITH(UPDLOCK,HOLDLOCK))<>1
        THROW 51000,'Create exactly one fresh project, then upload only 01_Masters.zip before running this script.',1;
    IF @ProjectId IS NULL SELECT @ProjectId=Id FROM Migration.MigrationProjects;
    DECLARE @Resource nvarchar(255)=CONCAT(N'NexusFlow.Migration.',@ProjectId);
    EXEC @LockResult=sys.sp_getapplock @Resource=@Resource,@LockMode='Exclusive',
         @LockOwner='Transaction',@LockTimeout=15000;
    IF @LockResult<0 THROW 51000,'Migration project is busy. Refresh and retry.',1;

    SELECT @Old=p.SettingsJson,@Version=p.Version,@Status=p.Status
    FROM Migration.MigrationProjects p WITH(UPDLOCK,HOLDLOCK)
    WHERE p.Id=@ProjectId AND p.SourceSystem=N'TESTTTT'
      AND p.OpeningDate=CONVERT(date,'2026-10-01')
      AND p.ClosingDate=CONVERT(date,'2026-09-30') AND p.Currency=N'LKR';
    IF @Old IS NULL THROW 51000,'Expected project identity/dates not found.',1;
    IF @Status<>N'Preparing' THROW 51000,'Project must still be Preparing; finalized projects are read-only.',1;
    IF @Version<>@ExpectedVersion THROW 51000,'Expected a fresh project with only the three stage-1 uploads (version 3). Do not bypass; inspect the current setup.',1;
    IF ISJSON(@Old)<>1 THROW 51000,'Project settings are invalid JSON.',1;
    IF EXISTS(SELECT 1 FROM Migration.MigrationRuns WITH(UPDLOCK,HOLDLOCK)
              WHERE ProjectId=@ProjectId AND Status IN(N'Queued',N'Running'))
        THROW 51000,'An import run is active. This script cannot change its settings.',1;
    IF EXISTS(SELECT 1 FROM Migration.MigrationUnits WITH(UPDLOCK,HOLDLOCK)
              WHERE ProjectId=@ProjectId)
        THROW 51000,'Posting has started. This script is restricted to the unposted setup observed.',1;

    -- Require exactly the three untouched stage-1 files supplied in the user's output.
    IF (SELECT COUNT(*) FROM Migration.MigrationDatasets WITH(HOLDLOCK)
        WHERE ProjectId=@ProjectId AND IsCurrent=1)<>3
        THROW 51000,'Dataset set changed; rerun discovery before updating settings.',1;
    IF NOT EXISTS(SELECT 1 FROM Migration.MigrationDatasets WITH(HOLDLOCK)
        WHERE ProjectId=@ProjectId AND IsCurrent=1 AND Module=N'Accounts' AND Revision=1
          AND FileHash=N'C82990DBE4D3F677B5171994A6F3CF2016CD706C206C5194446C7957D681C0C0')
       OR NOT EXISTS(SELECT 1 FROM Migration.MigrationDatasets WITH(HOLDLOCK)
        WHERE ProjectId=@ProjectId AND IsCurrent=1 AND Module=N'CustomerMaster' AND Revision=1
          AND FileHash=N'09902A2963B9CD68B42FCB69C3E3A97E94C1448C855601F1E15EA0F9A67C2B3C')
       OR NOT EXISTS(SELECT 1 FROM Migration.MigrationDatasets WITH(HOLDLOCK)
        WHERE ProjectId=@ProjectId AND IsCurrent=1 AND Module=N'SupplierMaster' AND Revision=1
          AND FileHash=N'8D6508A9D5DBF3549D4186938A8803BD749491856A5D50EB0ED97E462275BA9E')
        THROW 51000,'Uploaded file hashes/revisions differ from the supplied output.',1;

    -- Confirm temporal audit period columns are engine-generated; leave them out of INSERT.
    IF NOT EXISTS(SELECT 1 FROM sys.columns
                  WHERE object_id=OBJECT_ID(N'Migration.MigrationAudit')
                    AND name=N'Id' AND is_identity=1)
        THROW 51000,'Unexpected audit identity schema.',1;
    IF EXISTS(SELECT 1 FROM sys.columns
              WHERE object_id=OBJECT_ID(N'Migration.MigrationAudit')
                AND name IN(N'ValidFrom',N'ValidTo') AND generated_always_type NOT IN(1,2))
        THROW 51000,'Unexpected audit period columns. Review generated values before inserting.',1;

    DECLARE @Master table
    (MappingType nvarchar(40),Id int,Code nvarchar(200),IsActive bit);
    INSERT INTO @Master
    SELECT l.Type,l.Id,l.Code,l.IsActive FROM Config.SystemLookups l WITH(HOLDLOCK)
    WHERE l.Type IN(N'CustomerGroup',N'SupplierGroup',N'PaymentTerm',N'PriceLevel')
    UNION ALL SELECT N'Category',c.Id,c.Code,CONVERT(bit,1) FROM Master.Categories c WITH(HOLDLOCK)
    UNION ALL SELECT N'Brand',b.Id,b.Name,CONVERT(bit,1) FROM Master.Brands b WITH(HOLDLOCK)
    UNION ALL SELECT N'Unit',u.Id,u.Symbol,CONVERT(bit,1) FROM Master.UnitOfMeasures u WITH(HOLDLOCK)
    UNION ALL SELECT N'Stock',v.Id,v.SKU,v.IsActive FROM Master.ProductVariants v WITH(HOLDLOCK)
    UNION ALL
    SELECT N'BankBranch',br.Id,CONCAT(b.BankCode,N'-',br.BranchCode),
           CONVERT(bit,CASE WHEN br.IsActive=1 AND b.IsActive=1 THEN 1 ELSE 0 END)
    FROM Finance.BankBranches br WITH(HOLDLOCK)
    JOIN Finance.Banks b WITH(HOLDLOCK) ON b.Id=br.BankId;

    IF EXISTS
    (
        SELECT 1 FROM @Desired d
        WHERE (SELECT COUNT(*) FROM @Master m
               WHERE m.MappingType=d.MappingType AND m.Id=d.TargetId AND m.IsActive=1
                 AND UPPER(m.Code)=UPPER(d.TargetCode)
                 AND (d.MappingType<>N'Stock' OR
                      m.Code COLLATE Latin1_General_100_BIN2=
                      d.TargetCode COLLATE Latin1_General_100_BIN2))<>1
    ) THROW 51000,'One or more master IDs/codes/active states changed. Nothing was saved.',1;
    IF EXISTS(SELECT 1 FROM @RequiredAccounts r
              LEFT JOIN Finance.Accounts a WITH(HOLDLOCK) ON a.Id=r.Id
              WHERE a.Id IS NULL OR a.Code<>r.Code OR a.Type<>r.AccountType
                 OR a.IsActive<>1 OR a.IsTransactionAccount<>1)
        THROW 51000,'A verified default account changed or is no longer an active posting account.',1;

    SET @New=@Old;
    IF NOT EXISTS(SELECT 1 FROM OPENJSON(@New) WHERE [key]=N'Mappings')
        SET @New=JSON_MODIFY(@New,'$.Mappings',JSON_QUERY(N'{}'));
    IF NOT EXISTS(SELECT 1 FROM OPENJSON(@New) WHERE [key]=N'Defaults')
        SET @New=JSON_MODIFY(@New,'$.Defaults',JSON_QUERY(N'{}'));
    IF NOT EXISTS(SELECT 1 FROM OPENJSON(@New) WHERE [key]=N'Mappings' AND [type]=5)
       OR NOT EXISTS(SELECT 1 FROM OPENJSON(@New) WHERE [key]=N'Defaults' AND [type]=5)
        THROW 51000,'Mappings and Defaults must be JSON objects.',1;

    -- Merge only checked keys. Existing unrelated settings and dataset states survive.
    DECLARE @Work table(MappingType nvarchar(40),LegacyCode nvarchar(100),TargetId int);
    INSERT INTO @Work SELECT MappingType,LegacyCode,TargetId FROM @Desired;
    DECLARE @Type nvarchar(40),@Legacy nvarchar(100),@Id int;
    DECLARE @TypePath nvarchar(400),@Path nvarchar(600),@Existing nvarchar(100);
    WHILE EXISTS(SELECT 1 FROM @Work)
    BEGIN
        SELECT TOP(1) @Type=MappingType,@Legacy=LegacyCode,@Id=TargetId
        FROM @Work ORDER BY MappingType,LegacyCode;
        SET @TypePath=N'$.Mappings."'+STRING_ESCAPE(@Type,'json')+N'"';
        IF JSON_QUERY(@New,@TypePath) IS NULL
        BEGIN
            IF JSON_VALUE(@New,@TypePath) IS NOT NULL
                THROW 51000,'Existing mapping group is not an object.',1;
            SET @New=JSON_MODIFY(@New,@TypePath,JSON_QUERY(N'{}'));
        END;
        SET @Path=@TypePath+N'."'+STRING_ESCAPE(@Legacy,'json')+N'"';
        SET @Existing=JSON_VALUE(@New,@Path);
        IF @Existing IS NOT NULL AND
           (TRY_CONVERT(int,@Existing) IS NULL OR TRY_CONVERT(int,@Existing)<>@Id)
            THROW 51000,'An existing mapping conflicts with a supplied ID. Nothing was overwritten.',1;
        SET @New=JSON_MODIFY(@New,@Path,@Id);
        DELETE FROM @Work WHERE MappingType=@Type AND LegacyCode=@Legacy;
    END;

    DECLARE @DefaultWork table([Key] nvarchar(100),Id int);
    INSERT INTO @DefaultWork SELECT [key],CONVERT(int,value) FROM OPENJSON(@Defaults);
    WHILE EXISTS(SELECT 1 FROM @DefaultWork)
    BEGIN
        SELECT TOP(1) @Legacy=[Key],@Id=Id FROM @DefaultWork ORDER BY [Key];
        SET @Path=N'$.Defaults."'+STRING_ESCAPE(@Legacy,'json')+N'"';
        SET @Existing=JSON_VALUE(@New,@Path);
        IF @Existing IS NOT NULL AND
           (TRY_CONVERT(int,@Existing) IS NULL OR TRY_CONVERT(int,@Existing)<>@Id)
            THROW 51000,'An existing default differs from the checked selection.',1;
        SET @New=JSON_MODIFY(@New,@Path,@Id);
        DELETE FROM @DefaultWork WHERE [Key]=@Legacy;
    END;
    IF COALESCE(TRY_CONVERT(int,JSON_VALUE(@New,'$.OpeningEquityAccountId')),0) NOT IN(0,3002)
        THROW 51000,'Existing opening equity differs from verified 3200.',1;
    IF JSON_VALUE(@New,'$.Defaults.SafeAccount') IS NOT NULL
       OR JSON_VALUE(@New,'$.Defaults.StockWarehouse') IS NOT NULL
       OR JSON_QUERY(@New,'$.Mappings.Warehouse') IS NOT NULL
        THROW 51000,'Cheque/warehouse settings changed from the unresolved state. Review them first.',1;
    SET @New=JSON_MODIFY(@New,'$.OpeningEquityAccountId',3002);
    SET @New=JSON_MODIFY(@New,'$.SupplierTermDays',30);

    IF EXISTS
    (
      SELECT 1 FROM @Desired d
      WHERE TRY_CONVERT(int,JSON_VALUE(@New,
         N'$.Mappings."'+STRING_ESCAPE(d.MappingType,'json')+N'"."'+
         STRING_ESCAPE(d.LegacyCode,'json')+N'"'))<>d.TargetId
         OR JSON_VALUE(@New,N'$.Mappings."'+STRING_ESCAPE(d.MappingType,'json')+
              N'"."'+STRING_ESCAPE(d.LegacyCode,'json')+N'"') IS NULL
    ) THROW 51000,'Mapping merge verification failed.',1;
    IF JSON_QUERY(@Old,'$.DatasetStates')<>JSON_QUERY(@New,'$.DatasetStates')
        THROW 51000,'Dataset states must remain unchanged.',1;

    UPDATE Migration.MigrationProjects
       SET SettingsJson=@New,Version=Version+1,
           ApprovedHash=NULL,ApprovedAt=NULL,ApprovedBy=NULL,
           LastModifiedAt=@Now,LastModifiedBy=@Actor
    WHERE Id=@ProjectId AND Version=@ExpectedVersion AND Status=N'Preparing';
    IF @@ROWCOUNT<>1 THROW 51000,'Project changed concurrently. Nothing was saved.',1;

    DECLARE @Detail nvarchar(max)=CONCAT(
        N'Operator-run SQL for fresh project: 84 confirmed pilot mappings and 10 verified defaults; ',
        N'opening equity 3200, supplier term 30 days. Version 3 -> 4. ',
        N'Unresolved: WH_MAIN missing; 1151 is Salary Advances Receivable. ',
        N'Warehouse and safe defaults left unset. No imports/postings or master changes. ',
        N'Before settings SHA256=',CONVERT(varchar(64),HASHBYTES('SHA2_256',@Old),2),
        N'; after=',CONVERT(varchar(64),HASHBYTES('SHA2_256',@New),2),N'.');
    INSERT INTO Migration.MigrationAudit
        (ProjectId,Action,Detail,CreatedAt,CreatedBy)
    VALUES(@ProjectId,N'Mappings',@Detail,@Now,@Actor);
    SET @NewAuditId=CONVERT(int,SCOPE_IDENTITY());
    INSERT INTO [System].AuditLogs(UserId,EntityName,Details,[Timestamp],Action,IPAddress)
    VALUES(@Actor,N'Migration.MigrationProjects',CONCAT(@Detail,N' MigrationAuditId=',@NewAuditId),
           @Now,N'MIGRATION_MAPPINGS_SQL',NULL);
    COMMIT TRANSACTION;

    SELECT N'SAVED FRESH PROJECT MAPPINGS' AS Result,p.Id AS ProjectId,p.Name,p.Version,
           @NewAuditId AS NewMigrationAuditId,
           (SELECT COUNT(*) FROM @Desired) AS MappingsSaved,
           N'PARTIAL SETUP: resolve warehouse and safe account before financial/stock import' AS NextStep
    FROM Migration.MigrationProjects p WHERE p.Id=@ProjectId;
    SELECT p.SettingsJson FROM Migration.MigrationProjects p WHERE p.Id=@ProjectId;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
