$ErrorActionPreference='Stop'
$taskConfig=Get-Content -LiteralPath 'F:\source\NexusFlow_ERP\NexusFlow.Web\appsettings.json' -Raw | ConvertFrom-Json
$taskDev=Get-Content -LiteralPath 'F:\source\NexusFlow_ERP\NexusFlow.Web\appsettings.Development.json' -Raw | ConvertFrom-Json
$taskConnection=$taskConfig.ConnectionStrings.DefaultConnection
if($taskDev.ConnectionStrings.DefaultConnection){$taskConnection=$taskDev.ConnectionStrings.DefaultConnection}
if(-not $taskConnection){$taskConnection='Data Source=SAKEELPC;Initial Catalog=NexusERPLocal;Integrated Security=True;Encrypt=False'}
$taskBuilder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder $taskConnection
$taskBuilder['ApplicationIntent']='ReadOnly';$taskBuilder['Connect Timeout']=5
$taskDb=New-Object System.Data.SqlClient.SqlConnection $taskBuilder.ConnectionString
try {
 $taskDb.Open();$taskCounts=@{}
 foreach($taskName in @('MigrationUnits','MigrationOrigins','StockTransactions','StockLayers','SalesInvoices','SupplierBills','PaymentTransactions')){
  $taskCmd=$taskDb.CreateCommand();$taskCmd.CommandText='SELECT TABLE_SCHEMA FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME=@n AND TABLE_TYPE=''BASE TABLE'''
  [void]$taskCmd.Parameters.AddWithValue('@n',$taskName);$taskSchema=$taskCmd.ExecuteScalar()
  if($taskSchema){
   $taskCountCmd=$taskDb.CreateCommand();$taskCountCmd.CommandText='SELECT COUNT_BIG(*) FROM ['+$taskSchema+'].['+$taskName+']'
   $taskCounts[$taskName]=$taskCountCmd.ExecuteScalar()
   if($taskName -eq 'StockLayers'){$taskCountCmd.CommandText+=' WHERE RemainingQty <> 0';$taskCounts['LiveStockLayers']=$taskCountCmd.ExecuteScalar()}
  }
 }
 $taskCounts | ConvertTo-Json | Set-Content -LiteralPath 'F:\source\NexusFlow_ERP\docs\migration\uat_upload_20261012\.work\activity.json' -Encoding UTF8
 $taskCounts | ConvertTo-Json
} finally {$taskDb.Dispose()}