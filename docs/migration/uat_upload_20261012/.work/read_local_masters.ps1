$ErrorActionPreference = 'Stop'
$taskConfig = Get-Content -LiteralPath 'F:\source\NexusFlow_ERP\NexusFlow.Web\appsettings.json' -Raw | ConvertFrom-Json
$taskDev = Get-Content -LiteralPath 'F:\source\NexusFlow_ERP\NexusFlow.Web\appsettings.Development.json' -Raw | ConvertFrom-Json
$taskConnection = $taskConfig.ConnectionStrings.DefaultConnection
if ($taskDev.ConnectionStrings.DefaultConnection) { $taskConnection = $taskDev.ConnectionStrings.DefaultConnection }
if (-not $taskConnection) { $taskConnection = 'Data Source=SAKEELPC;Initial Catalog=NexusERPLocal;Integrated Security=True;Encrypt=False' }
$taskBuilder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $taskConnection
$taskBuilder['Connect Timeout'] = 5
$taskBuilder['ApplicationIntent'] = 'ReadOnly'
$taskDb = New-Object System.Data.SqlClient.SqlConnection $taskBuilder.ConnectionString
try {
    $taskDb.Open()
    $taskResult = @{ Database=$taskBuilder.InitialCatalog; Server=$taskBuilder.DataSource; ReadAtUtc=[DateTime]::UtcNow.ToString('s'); Tables=@{} }
    $taskQueries = @{}
    $taskColumns = @{
        Accounts='Id,Code,Name,Type,IsTransactionAccount,IsActive,RequiresReconciliation,Balance';
        Customers='Id,Name,LegacyCustomerCode,IsActive,CustomerGroupId,PaymentTermId,PriceLevelId,DefaultReceivableAccountId,DefaultRevenueAccountId,CreditPeriodDays';
        Suppliers='Id,Name,LegacySupplierCode,IsActive,SupplierGroupId,PaymentTermId,DefaultPayableAccountId,DefaultExpenseAccountId';
        SystemLookups='Id,Type,Code,Value,IsActive';
        SystemConfigs='[Key],Value'; Warehouses='Id,Code,Name,IsActive,OverrideInventoryAccountId';
        UnitOfMeasures='Id,Name,Symbol'; Brands='Id,Name'; Categories='Id,Code,Name,InventoryAccountId,SalesAccountId,CogsAccountId';
        BankBranches='Id,BankId,BranchCode,BranchName,IsActive'; Banks='*';
        ProductVariants='Id,ProductId,SKU,Name,IsActive'; Products='Id,Name,BrandId,CategoryId,UnitOfMeasureId,Type';
        MigrationProjects='Id,Name,SourceSystem,OpeningDate,ClosingDate,Status,SettingsJson';
        MigrationScopes='Id,Kind,Code,Status,EntityId,RequiredModulesJson'; FinancialPeriods='Id,Name,StartDate,EndDate,IsClosed'; JournalEntries='Id,Date,Module,ReferenceNo,TotalAmount'
    }
    foreach ($taskName in $taskColumns.Keys) {
        $taskSchemaCmd=$taskDb.CreateCommand(); $taskSchemaCmd.CommandText='SELECT TABLE_SCHEMA FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME=@name AND TABLE_TYPE=''BASE TABLE'''
        [void]$taskSchemaCmd.Parameters.AddWithValue('@name',$taskName); $taskSchemaName=$taskSchemaCmd.ExecuteScalar()
        if ($taskSchemaName) {
            $taskFilter=if($taskName -eq 'SystemConfigs') { " WHERE [Key] LIKE 'Account.%'" } else { '' }
            $taskQueries[$taskName]='SELECT '+$taskColumns[$taskName]+' FROM ['+$taskSchemaName+'].['+$taskName+']'+$taskFilter
        }
    }
    foreach ($taskKey in $taskQueries.Keys) {
        $taskCmd = $taskDb.CreateCommand(); $taskCmd.CommandText = $taskQueries[$taskKey]; $taskCmd.CommandTimeout = 10
        $taskReader=$taskCmd.ExecuteReader(); $taskRows = @()
        while ($taskReader.Read()) {
            $taskRow = [ordered]@{}
            for ($taskCol=0; $taskCol -lt $taskReader.FieldCount; $taskCol++) { $taskRow[$taskReader.GetName($taskCol)] = if ($taskReader.IsDBNull($taskCol)) { $null } else { $taskReader.GetValue($taskCol) } }
            $taskRows += [pscustomobject]$taskRow
        }
        $taskReader.Close(); $taskResult.Tables[$taskKey]=$taskRows
    }
    $taskResult | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath 'F:\source\NexusFlow_ERP\docs\migration\uat_upload_20261012\.work\local_masters.json' -Encoding UTF8
    foreach ($taskName in $taskResult.Tables.Keys) { Write-Output ($taskName+': '+$taskResult.Tables[$taskName].Count) }
} catch {
    Write-Output ('Read-only database check failed: ' + $_.Exception.GetType().Name)
} finally { $taskDb.Close() }
