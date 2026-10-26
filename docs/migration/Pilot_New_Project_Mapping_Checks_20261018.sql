-- READ ONLY: current project/version and the two unresolved destination selections.
SET NOCOUNT ON;
IF DB_NAME()<>N'NexusGarnetProd' THROW 51000,'Select NexusGarnetProd.',1;
IF LOWER(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')))
 NOT IN(N'versalapps',N'versalapps.database.windows.net') THROW 51000,'Wrong server.',1;

SELECT Id AS ProjectId,Name,SourceSystem,OpeningDate,ClosingDate,Status,Version,
       JSON_QUERY(SettingsJson,'$.Mappings') AS Mappings,
       JSON_QUERY(SettingsJson,'$.Defaults') AS Defaults
FROM Migration.MigrationProjects ORDER BY Id;

SELECT ProjectId,Module,Revision,IsCurrent,FileName,FileHash
FROM Migration.MigrationDatasets ORDER BY ProjectId,Module,Revision;

SELECT w.Id AS WarehouseId,w.Code,w.Name,w.IsActive,w.OverrideInventoryAccountId,
       a.Code AS OverrideAccountCode,a.Name AS OverrideAccountName
FROM Master.Warehouses w
LEFT JOIN Finance.Accounts a ON a.Id=w.OverrideInventoryAccountId
ORDER BY w.Code;

SELECT Id AS AccountId,Code,Name,Type,IsActive,IsTransactionAccount
FROM Finance.Accounts
WHERE Type=1 AND IsActive=1 AND IsTransactionAccount=1
ORDER BY Code;
