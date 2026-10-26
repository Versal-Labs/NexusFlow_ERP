-- READ ONLY for application data. Finds every missing active posting account
-- and shows whether Trial Balance has already posted. No balances are changed.
SET NOCOUNT ON;
IF DB_NAME()<>N'NexusGarnetProd' OR LOWER(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')))
   NOT IN(N'versalapps',N'versalapps.database.windows.net')
    THROW 54000,'Select NexusGarnetProd on versalapps.',1;
DECLARE @ProjectId int=3,@Settings nvarchar(max),@Rows nvarchar(max),@DatasetId int;
SELECT @Settings=SettingsJson FROM Migration.MigrationProjects WHERE Id=@ProjectId;
IF @Settings IS NULL OR ISJSON(@Settings)<>1 THROW 54001,'Project 3 settings were not found or are invalid.',1;

SELECT N'PROJECT' AS ResultSet,Id,Name,Status,Version,OpeningDate,ClosingDate,
       JSON_QUERY(SettingsJson,'$.Mappings.Account') AS AccountMappings,
       JSON_QUERY(SettingsJson,'$.Columns.TrialBalance') AS TrialBalanceColumnProfile
FROM Migration.MigrationProjects WHERE Id=@ProjectId;
SELECT N'TRIAL_BALANCE_UNITS' AS ResultSet,Id AS UnitId,ScopeKey,Status,
       JournalEntryId,ReversalJournalEntryId,Hash,CreatedAt
FROM Migration.MigrationUnits WHERE ProjectId=@ProjectId AND Module=N'TrialBalance' ORDER BY Id;
SELECT N'RUNS' AS ResultSet,Id AS RunId,JobId,Status,CommittedUnits,CompletedAt,LEFT(Error,1000) AS Error
FROM Migration.MigrationRuns WHERE ProjectId=@ProjectId ORDER BY Id;
SELECT N'TRIAL_BALANCE_FILES' AS ResultSet,Id AS DatasetId,Module,Revision,IsCurrent,FileName,FileHash,ClosingDate
FROM Migration.MigrationDatasets WHERE ProjectId=@ProjectId AND Module=N'TrialBalance' ORDER BY Revision;

IF EXISTS(SELECT 1 FROM OPENJSON(@Settings,'$.Columns.TrialBalance'))
    THROW 54002,'A Trial Balance column profile exists. Return the profile above for review; this code-coverage check expects the supplied canonical headers.',1;
IF (SELECT COUNT(*) FROM Migration.MigrationDatasets WHERE ProjectId=@ProjectId AND Module=N'TrialBalance' AND IsCurrent=1)<>1
    THROW 54003,'Exactly one current Trial Balance dataset is required.',1;
SELECT @Rows=RowsJson,@DatasetId=Id FROM Migration.MigrationDatasets
WHERE ProjectId=@ProjectId AND Module=N'TrialBalance' AND IsCurrent=1;
IF ISJSON(@Rows)<>1 THROW 54004,'Trial Balance parsed rows are invalid JSON.',1;

DROP TABLE IF EXISTS #TbCoverage;
SELECT CONVERT(int,j.[key])+1 AS ParsedRow,
       c.SourceCode,COALESCE(NULLIF(m.MappedId,0),a.Id) AS ResolvedAccountId,
       TRY_CONVERT(decimal(28,2),JSON_VALUE(j.value,'$.Data.Debit')) AS SourceDebit,
       TRY_CONVERT(decimal(28,2),JSON_VALUE(j.value,'$.Data.Credit')) AS SourceCredit
INTO #TbCoverage
FROM OPENJSON(@Rows) j
CROSS APPLY(SELECT UPPER(LTRIM(RTRIM(JSON_VALUE(j.value,'$.Data.Code')))) AS SourceCode) c
OUTER APPLY(SELECT TRY_CONVERT(int,[value]) AS MappedId FROM OPENJSON(@Settings,'$.Mappings.Account')
            WHERE [key] COLLATE DATABASE_DEFAULT=c.SourceCode COLLATE DATABASE_DEFAULT) m
LEFT JOIN Finance.Accounts a ON a.Code=c.SourceCode;

SELECT N'MISSING_ACTIVE_POSTING_ACCOUNTS' AS ResultSet,a.Id AS AccountId,a.Code,a.Name,a.Type,
       a.Balance AS CurrentApplicationBalance,
       N'Closing source balance needs confirmation; current application balance is not the source closing balance.' AS Note
FROM Finance.Accounts a
WHERE a.IsActive=1 AND a.IsTransactionAccount=1
  AND NOT EXISTS(SELECT 1 FROM #TbCoverage r WHERE r.ResolvedAccountId=a.Id)
ORDER BY a.Code;
SELECT N'UNRESOLVED_SOURCE_ROWS' AS ResultSet,r.*
FROM #TbCoverage r LEFT JOIN Finance.Accounts a ON a.Id=r.ResolvedAccountId AND a.IsActive=1 AND a.IsTransactionAccount=1
WHERE a.Id IS NULL ORDER BY r.ParsedRow;
SELECT N'COVERAGE_SUMMARY' AS ResultSet,@DatasetId AS DatasetId,
       (SELECT COUNT(*) FROM #TbCoverage) AS SourceRows,
       (SELECT COUNT(*) FROM Finance.Accounts WHERE IsActive=1 AND IsTransactionAccount=1) AS ActivePostingAccounts,
       (SELECT COUNT(*) FROM Finance.Accounts a WHERE a.IsActive=1 AND a.IsTransactionAccount=1
          AND NOT EXISTS(SELECT 1 FROM #TbCoverage r WHERE r.ResolvedAccountId=a.Id)) AS MissingAccounts;
