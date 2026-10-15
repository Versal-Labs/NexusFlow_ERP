$ErrorActionPreference='Stop'
. 'F:\source\NexusFlow_ERP\artifacts\local-migration-reset-20261008\LocalDatabase.ps1'
$taskRoot='F:\source\NexusFlow_ERP\docs\migration\stock_validation_20261013'
$taskWork=Join-Path $taskRoot '.work'
$taskAliases=Get-Content -Raw -LiteralPath (Join-Path $taskWork 'additional_stock_mappings.json') | ConvertFrom-Json
if($taskAliases.Count -ne 33){throw 'Expected 33 reviewed Stock aliases.'}
$taskDb=Open-ConfirmedLocalDatabase
$taskTx=$null
function TaskQuery([string]$sql) { Invoke-LocalQuery $taskDb $sql 90 $taskTx }
function TaskName([string]$name){'['+$name.Replace(']',']]')+']'}
function TaskHash([string]$text){
 $sha=[Security.Cryptography.SHA256]::Create()
 try{[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace('-','')}finally{$sha.Dispose()}
}
function TaskRows($dataTable){
 @(foreach($r in $dataTable.Rows){$v=[ordered]@{};foreach($c in $dataTable.Columns){$v[$c.ColumnName]=if($r.IsNull($c)){$null}else{$r[$c]}};[pscustomobject]$v})
}
function TaskFingerprint($table){
 $cols=@($taskColumns | Where-Object {$_['ObjectId'] -eq $table['ObjectId']} | ForEach-Object {TaskName $_['ColumnName']})
 $name=(TaskName $table['SchemaName'])+'.'+(TaskName $table['TableName'])
 $data=TaskQuery ('SELECT '+($cols -join ',')+' FROM '+$name+' ORDER BY [Id] FOR JSON PATH,INCLUDE_NULL_VALUES')
 $json=(@($data.Tables[0].Rows | ForEach-Object {[string]$_[0]}) -join '')
 $count=TaskQuery ('SELECT COUNT_BIG(*) AS Records FROM '+$name)
 [pscustomobject]@{Table=[string]$table['SchemaName']+'.'+[string]$table['TableName'];Rows=[long]$count.Tables[0].Rows[0]['Records'];SHA256=(TaskHash $json)}
}
try {
 $taskTx=$taskDb.BeginTransaction([System.Data.IsolationLevel]::Serializable)
 $taskCmd=$taskDb.CreateCommand();$taskCmd.Transaction=$taskTx;$taskCmd.CommandTimeout=30
 $taskCmd.CommandText="SET XACT_ABORT ON; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'NexusFlow.Migration.4',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Migration project is busy.',1;"
 [void]$taskCmd.ExecuteNonQuery();$taskCmd.Dispose()
 $taskData=TaskQuery "SELECT * FROM Migration.MigrationProjects WITH(UPDLOCK,HOLDLOCK) WHERE Id=4; SELECT COUNT(*) AS ActiveRuns FROM Migration.MigrationRuns WHERE ProjectId=4 AND Status IN ('Queued','Running'); SELECT COUNT(*) AS StockUnits FROM Migration.MigrationUnits WHERE ProjectId=4 AND Module='Stock';"
 if($taskData.Tables[0].Rows.Count -ne 1){throw 'Expected local migration project 4.'}
 $taskProject=$taskData.Tables[0].Rows[0]
 if($taskProject['Name'] -cne 'NexusFlow UAT 2026-10-12' -or $taskProject['SourceSystem'] -cne 'TESTTTT' -or $taskProject['Status'] -cne 'Preparing'){throw 'Unexpected project; no changes made.'}
 if([int]$taskData.Tables[1].Rows[0]['ActiveRuns'] -ne 0 -or [int]$taskData.Tables[2].Rows[0]['StockUnits'] -ne 0){throw 'Refusing active runs or already-imported Stock.'}
 $taskBeforeJson=[string]$taskProject['SettingsJson']
 $taskSettings=$taskBeforeJson | ConvertFrom-Json -AsHashtable
 $taskBeforeSettings=$taskBeforeJson | ConvertFrom-Json -AsHashtable
 $taskBeforeVersion=[int]$taskProject['Version']
 $taskMeta=TaskQuery @'
SELECT s.name AS SchemaName,t.name AS TableName,t.object_id AS ObjectId FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name;
SELECT t.object_id AS ObjectId,c.name AS ColumnName FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id WHERE t.is_ms_shipped=0 ORDER BY t.object_id,c.column_id;
'@
 $taskTables=@($taskMeta.Tables[0].Rows);$taskColumns=@($taskMeta.Tables[1].Rows)
 $taskByTable=@{}
 foreach($t in $taskTables | Where-Object {$_['TableName'] -in @('ProductVariants','Products','Categories','Accounts','Warehouses','UnitOfMeasures','Brands')}){
  if($taskByTable.ContainsKey([string]$t['TableName'])){throw 'Ambiguous master table.'}
  $taskByTable[[string]$t['TableName']]=(TaskName $t['SchemaName'])+'.'+(TaskName $t['TableName'])
 }
 $taskMaster=TaskQuery ('SELECT Id,SKU,ProductId,IsActive FROM '+$taskByTable['ProductVariants']+'; SELECT Id,Type,CategoryId,UnitOfMeasureId,BrandId FROM '+$taskByTable['Products']+'; SELECT Id,Code,Name,InventoryAccountId FROM '+$taskByTable['Categories']+'; SELECT Id,Code,Name,Type,IsActive,IsTransactionAccount FROM '+$taskByTable['Accounts']+'; SELECT Id,Code,IsActive,OverrideInventoryAccountId FROM '+$taskByTable['Warehouses']+'; SELECT Id FROM '+$taskByTable['UnitOfMeasures']+'; SELECT Id FROM '+$taskByTable['Brands']+';')
 $taskVariants=@($taskMaster.Tables[0].Rows);$taskProductIds=@{};$taskCategoryIds=@{};$taskAccountIds=@{};$taskWarehouseIds=@{}
 foreach($r in $taskMaster.Tables[1].Rows){$taskProductIds[[int]$r['Id']]=$r}
 foreach($r in $taskMaster.Tables[2].Rows){$taskCategoryIds[[int]$r['Id']]=$r}
 foreach($r in $taskMaster.Tables[3].Rows){$taskAccountIds[[int]$r['Id']]=$r}
 foreach($r in $taskMaster.Tables[4].Rows){$taskWarehouseIds[[int]$r['Id']]=$r}
 if(-not $taskSettings['Mappings'].Contains('Stock')){$taskSettings['Mappings']['Stock']=[ordered]@{}}
 $taskAdded=@()
 foreach($a in $taskAliases){
  $matching=@($taskVariants | Where-Object {[int]$_['Id'] -eq [int]$a.target_id})
  if($matching.Count -ne 1 -or [string]$matching[0]['SKU'] -cne [string]$a.target_sku -or -not $matching[0]['IsActive']){throw ('Unexpected SKU master: '+$a.code)}
  $normal=@($taskVariants | Where-Object {([string]$_['SKU']).Trim().ToUpperInvariant() -ceq $a.code})
  if($normal.Count -ne 1 -or [int]$normal[0]['Id'] -ne [int]$a.target_id){throw 'Ambiguous normalized SKU.'}
  if($taskSettings['Mappings']['Stock'].Contains([string]$a.code)){
   if([int]$taskSettings['Mappings']['Stock'][[string]$a.code] -ne [int]$a.target_id){throw 'Existing Stock mapping differs; refusing overwrite.'}
  }else{$taskSettings['Mappings']['Stock'][[string]$a.code]=[int]$a.target_id;$taskAdded+=[pscustomobject]@{Type='Stock';Code=$a.code;TargetId=[int]$a.target_id}}
 }
 $taskAccessories=@($taskMaster.Tables[2].Rows | Where-Object {([string]$_['Code']).ToUpperInvariant() -ceq 'ACCESSORIES'})
 if($taskAccessories.Count -ne 1 -or [int]$taskAccessories[0]['Id'] -ne 53){throw 'Unexpected Accessories master.'}
 $taskAccessoryAccount=$taskAccountIds[[int]$taskAccessories[0]['InventoryAccountId']]
 if($taskAccessoryAccount['Code'] -cne '1131' -or [int]$taskAccessoryAccount['Type'] -ne 1 -or -not $taskAccessoryAccount['IsActive'] -or -not $taskAccessoryAccount['IsTransactionAccount']){throw 'Accessories inventory account must be active Asset 1131.'}
 if(-not $taskSettings['Mappings'].Contains('Category')){$taskSettings['Mappings']['Category']=[ordered]@{}}
 if($taskSettings['Mappings']['Category'].Contains('ACCESSORIES')){
  if([int]$taskSettings['Mappings']['Category']['ACCESSORIES'] -ne 53){throw 'Existing Accessories mapping differs; refusing overwrite.'}
 }else{$taskSettings['Mappings']['Category']['ACCESSORIES']=53;$taskAdded+=[pscustomobject]@{Type='Category';Code='ACCESSORIES';TargetId=53}}
 foreach($kind in @('Customer','Supplier','Stock')){
  if($taskSettings['Mappings'].Contains($kind)){
   $values=@($taskSettings['Mappings'][$kind].Values)
   if(@($values | Sort-Object -Unique).Count -ne $values.Count){throw 'Legacy mappings must use distinct master targets.'}
  }
 }
 foreach($field in $taskBeforeSettings.Keys | Where-Object {$_ -cne 'Mappings'}){
  if(($taskBeforeSettings[$field] | ConvertTo-Json -Depth 20 -Compress) -cne ($taskSettings[$field] | ConvertTo-Json -Depth 20 -Compress)){throw 'Unrelated project setting changed.'}
 }
 foreach($type in $taskBeforeSettings['Mappings'].Keys){
  foreach($code in $taskBeforeSettings['Mappings'][$type].Keys){
   if([int]$taskBeforeSettings['Mappings'][$type][$code] -ne [int]$taskSettings['Mappings'][$type][$code]){throw 'Existing mapping changed.'}
  }
 }
 $taskDatasets=TaskQuery 'SELECT Module,RowsJson FROM Migration.MigrationDatasets WHERE ProjectId=4 AND IsCurrent=1; SELECT Kind,Code FROM Migration.MigrationScopes WHERE ProjectId=4;'
 $taskExpectedScopes=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 $taskKinds=@{CustomerMaster='Customer';CustomerOpenings='Customer';ReceivedCheques='Customer';SupplierMaster='Supplier';SupplierOpenings='Supplier';IssuedCheques='Supplier';Stock='Stock';BankOpening='Bank';BankRecon='Bank'}
 $taskStockRows=@()
 foreach($d in $taskDatasets.Tables[0].Rows){
  $module=[string]$d['Module'];if(-not $taskKinds.ContainsKey($module)){continue}
  if($taskSettings['Columns'].Contains($module) -and $taskSettings['Columns'][$module].Count){throw 'Column profile requires separate review.'}
  $parsed=([string]$d['RowsJson'] | ConvertFrom-Json -AsHashtable)
  foreach($r in $parsed){[void]$taskExpectedScopes.Add($taskKinds[$module]+'|'+([string]$r['Data']['Code']).Trim().ToUpperInvariant())}
  if($module -ceq 'Stock'){$taskStockRows=@($parsed)}
 }
 $taskExistingScopes=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 foreach($r in $taskDatasets.Tables[1].Rows){[void]$taskExistingScopes.Add([string]$r['Kind']+'|'+[string]$r['Code'])}
 if(-not $taskExpectedScopes.SetEquals($taskExistingScopes) -or $taskStockRows.Count -ne 938){throw 'Scope refresh would change staging; refusing this narrow mapping update.'}
 foreach($r in $taskStockRows){
  $d=$r['Data'];$code=([string]$d['Code']).Trim().ToUpperInvariant();$v=$null
  if($taskSettings['Mappings']['Stock'].Contains($code)){$v=@($taskVariants | Where-Object {[int]$_['Id'] -eq [int]$taskSettings['Mappings']['Stock'][$code]})[0]}
  else{$v=@($taskVariants | Where-Object {[string]$_['SKU'] -ceq $code}) | Select-Object -First 1}
  $unitCode=([string]$d['UnitCode']).Trim().ToUpperInvariant()
  if(-not $taskSettings['Mappings']['Unit'].Contains($unitCode)){throw ('Missing Unit: '+$code)}
  if($v){
   $p=$taskProductIds[[int]$v['ProductId']];$category=$taskCategoryIds[[int]$p['CategoryId']]
   if([int]$p['UnitOfMeasureId'] -ne [int]$taskSettings['Mappings']['Unit'][$unitCode]){throw ('Unit mismatch: '+$code)}
   $type=if([int]$p['Type'] -eq 1){'RawMaterial'}elseif([int]$p['Type'] -eq 2){'FinishedGood'}else{'Unsupported'}
   if($d['ProductType'] -cne $type){throw ('Product type mismatch: '+$code)}
  }else{
   $catCode=([string]$d['CategoryCode']).Trim().ToUpperInvariant()
   if(-not $taskSettings['Mappings']['Category'].Contains($catCode)){throw ('Missing Category: '+$code)}
   $category=$taskCategoryIds[[int]$taskSettings['Mappings']['Category'][$catCode]]
   if(-not $taskSettings['Mappings']['Brand'].Contains(([string]$d['BrandCode']).Trim().ToUpperInvariant())){throw ('Missing Brand: '+$code)}
  }
  $whCode=([string]$d['WarehouseCode']).Trim().ToUpperInvariant();$warehouse=$taskWarehouseIds[[int]$taskSettings['Mappings']['Warehouse'][$whCode]]
  if(-not $warehouse -or -not $warehouse['IsActive']){throw 'Invalid warehouse.'}
  $inv=if(-not $warehouse.IsNull('OverrideInventoryAccountId')){[int]$warehouse['OverrideInventoryAccountId']}elseif(-not $category.IsNull('InventoryAccountId')){[int]$category['InventoryAccountId']}else{0}
  $account=$taskAccountIds[$inv]
  if(-not $account -or -not $account['IsActive'] -or -not $account['IsTransactionAccount'] -or [int]$account['Type'] -ne 1 -or $account['Code'] -cne $d['InventoryAccountCode']){throw ('Inventory account mismatch: '+$code)}
 }
 $taskPreserveNames=@('Accounts','Customers','Suppliers','Products','ProductVariants','Categories','Warehouses','UnitOfMeasures','Brands','Banks','BankBranches','SystemLookups','JournalEntries','JournalLines','CustomerOpeningItems','SupplierOpeningItems','ChequeRegister','OpeningBankItems','StockLayers','StockTransactions','MigrationDatasets','MigrationScopes','MigrationUnits','MigrationOrigins','MigrationRuns')
 $taskPreserve=@($taskTables | Where-Object {$_['TableName'] -in $taskPreserveNames})
 if($taskPreserve.Count -ne $taskPreserveNames.Count){throw 'Unexpected preservation table set.'}
 # Preserve all business tables plus staged datasets, scopes, units, origins, runs and their histories.
 $taskBeforeHashes=@($taskPreserve | ForEach-Object {TaskFingerprint $_})
 $taskBackupFile=Join-Path $taskWork ('project4-before-mappings-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'.json')
 if(Test-Path -LiteralPath $taskBackupFile){throw 'Refusing to overwrite a configuration backup.'}
 $taskProjectBackup=TaskRows $taskData.Tables[0]
 [IO.File]::WriteAllText($taskBackupFile,($taskProjectBackup | ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
 $taskAfterJson=$taskSettings | ConvertTo-Json -Depth 20 -Compress
 $taskActor='Codex - user-authorized local mapping update'
 $taskDetail='User authorized direct local database update after slow browser entry. Added 33 Stock aliases linking uppercase legacy codes to exact existing lowercase SKUs, plus Category ACCESSORIES -> Accessories (53). Existing settings and mapping targets preserved. No data import or financial posting. Version '+$taskBeforeVersion+' -> '+($taskBeforeVersion+1)+'. Additions: '+($taskAdded | ConvertTo-Json -Compress)
 $taskCmd=$taskDb.CreateCommand();$taskCmd.Transaction=$taskTx;$taskCmd.CommandTimeout=30
 $taskCmd.CommandText=@'
UPDATE Migration.MigrationProjects SET SettingsJson=@Settings,Version=Version+1,ApprovedHash=NULL,ApprovedBy=NULL,ApprovedAt=NULL,LastModifiedAt=SYSUTCDATETIME(),LastModifiedBy=@Actor WHERE Id=4 AND Version=@Version AND SettingsJson=@Before AND Status='Preparing';
IF @@ROWCOUNT<>1 THROW 51000,'Project changed concurrently.',1;
INSERT Migration.MigrationAudit(ProjectId,Action,Detail,CreatedAt,CreatedBy,LastModifiedAt,LastModifiedBy) VALUES(4,'Mappings',@Detail,SYSUTCDATETIME(),@Actor,NULL,NULL);
'@
 foreach($pair in @(@('Settings',$taskAfterJson),@('Before',$taskBeforeJson),@('Actor',$taskActor),@('Detail',$taskDetail))){
  [void]$taskCmd.Parameters.Add('@'+$pair[0],[System.Data.SqlDbType]::NVarChar,-1);$taskCmd.Parameters['@'+$pair[0]].Value=$pair[1]
 }
 [void]$taskCmd.Parameters.Add('@Version',[System.Data.SqlDbType]::Int);$taskCmd.Parameters['@Version'].Value=$taskBeforeVersion
 [void]$taskCmd.ExecuteNonQuery();$taskCmd.Dispose()
 $taskAfterHashes=@($taskPreserve | ForEach-Object {TaskFingerprint $_})
 if(($taskBeforeHashes | ConvertTo-Json -Depth 5 -Compress) -cne ($taskAfterHashes | ConvertTo-Json -Depth 5 -Compress)){throw 'Business/staging/posting data changed; rolling back.'}
 $check=TaskQuery 'SELECT SettingsJson,Version,ApprovedHash FROM Migration.MigrationProjects WHERE Id=4'
 if([int]$check.Tables[0].Rows[0]['Version'] -ne $taskBeforeVersion+1 -or [string]$check.Tables[0].Rows[0]['SettingsJson'] -cne $taskAfterJson -or -not $check.Tables[0].Rows[0].IsNull('ApprovedHash')){throw 'Saved project verification failed.'}
 $taskTx.Commit();$taskTx.Dispose();$taskTx=$null
 $taskSaved=Invoke-LocalQuery $taskDb 'SELECT SettingsJson,Version FROM Migration.MigrationProjects WHERE Id=4'
 if([string]$taskSaved.Tables[0].Rows[0]['SettingsJson'] -cne $taskAfterJson){throw 'Independent saved-settings check failed.'}
 $taskReport=[ordered]@{Database='NexusERPLocal';Server='SAKEELPC';ProjectId=4;CommittedAtUtc=[DateTime]::UtcNow.ToString('o');Status='Committed';StockMappings=$taskSettings['Mappings']['Stock'].Count;AccessoriesTarget=53;Added=$taskAdded;VersionBefore=$taskBeforeVersion;VersionAfter=$taskBeforeVersion+1;PriorApprovalCleared=$true;ExistingMappingsAndSettingsPreserved=$true;PreservedTables=$taskAfterHashes;PreservedTableCount=$taskAfterHashes.Count;StockRowsChecked=938;RemainingMappingErrors=0;ScopeRefreshDelta=0;ImportExecuted=$false;BackupFile=$taskBackupFile;BackupSHA256=(Get-FileHash -LiteralPath $taskBackupFile -Algorithm SHA256).Hash}
 [IO.File]::WriteAllText((Join-Path $taskWork 'direct-mapping-update-report.json'),($taskReport | ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
 [IO.File]::WriteAllText((Join-Path $taskWork 'saved-settings-after.json'),$taskAfterJson,[Text.UTF8Encoding]::new($false))
 [pscustomobject]@{Status='Committed';Project=4;StockMappings=$taskReport.StockMappings;Accessories=53;Version=$taskReport.VersionAfter;PreservedTables=$taskReport.PreservedTableCount;StockRowsChecked=938;ImportExecuted=$false;Backup=$taskBackupFile} | ConvertTo-Json -Compress
}catch{if($taskTx){try{$taskTx.Rollback()}catch{};$taskTx.Dispose()};throw}finally{$taskDb.Dispose()}
