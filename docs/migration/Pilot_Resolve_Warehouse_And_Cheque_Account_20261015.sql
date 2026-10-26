-- READ ONLY. Follow-up to the provided project-2 Azure results.
SET NOCOUNT ON;
IF DB_NAME()<>N'NexusGarnetProd' THROW 51000,'Select NexusGarnetProd.',1;
IF LOWER(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')))
 NOT IN(N'versalapps',N'versalapps.database.windows.net') THROW 51000,'Wrong server.',1;

SELECT N'WAREHOUSES' AS Result,w.Id,w.Code,w.Name,w.Location,w.IsActive,
 w.OverrideInventoryAccountId,a.Code AS OverrideAccountCode,a.Name AS OverrideAccountName
FROM Master.Warehouses w LEFT JOIN Finance.Accounts a ON a.Id=w.OverrideInventoryAccountId
ORDER BY w.Id;

SELECT N'CHEQUE_ACCOUNT_CANDIDATES' AS Result,a.Id,a.Code,a.Name,a.Type,a.IsActive,
 a.IsTransactionAccount,a.ParentAccountId,p.Code AS ParentCode,p.Name AS ParentName
FROM Finance.Accounts a LEFT JOIN Finance.Accounts p ON p.Id=a.ParentAccountId
WHERE a.Type=1 OR a.Name LIKE N'%cheq%' OR a.Name LIKE N'%undeposit%'
ORDER BY a.Code;

SELECT N'SAFE_CONFIGURATION' AS Result,c.[Key],c.Value,
 a.Id AS ResolvedAccountId,a.Code,a.Name,a.IsActive,a.IsTransactionAccount
FROM Config.SystemConfigs c LEFT JOIN Finance.Accounts a ON a.Code=c.Value
WHERE c.[Key]=N'Account.Asset.UndepositedFunds';

-- The local pilot TB covered 39 accounts. Return the full destination posting list
-- so actual code/name/type differences and extra accounts can be reconciled.
SELECT N'ACTIVE_POSTING_ACCOUNTS' AS Result,Id,Code,Name,Type,ParentAccountId
FROM Finance.Accounts WHERE IsActive=1 AND IsTransactionAccount=1
ORDER BY Code;
