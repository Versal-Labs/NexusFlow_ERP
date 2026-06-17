[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$WebAppName,

    [Parameter(Mandatory = $true)]
    [string]$ImageName,

    [string]$InstanceId = $WebAppName,
    [string]$SetupKey,
    [string]$DefaultConnectionString,
    [string]$AzureBlobConnectionString,
    [switch]$UseAzureBlobState = $true,
    [switch]$UseAzureBlobDataProtection = $true
)

$ErrorActionPreference = "Stop"

function ConvertTo-NexusFlowContainerName {
    param([Parameter(Mandatory = $true)][string]$Value)

    $normalized = ($Value.Trim().ToLowerInvariant().ToCharArray() | ForEach-Object {
        if ([char]::IsLetterOrDigit($_) -or $_ -eq '-') { $_ } else { '-' }
    }) -join ''
    $normalized = (($normalized -split '-') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join '-'
    if ([string]::IsNullOrWhiteSpace($normalized)) { $normalized = 'default' }
    $normalized = "tenant-$normalized"
    if ($normalized.Length -gt 63) { $normalized = $normalized.Substring(0, 63).TrimEnd('-') }
    return $normalized
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) is required."
}

$containerName = ConvertTo-NexusFlowContainerName -Value $InstanceId

Write-Host "Configuring App Service container image for $WebAppName"
az webapp config container set `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --docker-custom-image-name $ImageName `
    --only-show-errors | Out-Null

$settings = @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "WEBSITES_PORT=8080",
    "NEXUSFLOW_INSTANCE_ID=$InstanceId",
    "NEXUSFLOW_INSTANCE_ROOT=/home/nexusflow/state",
    "NEXUSFLOW_DEPLOYMENT_PROFILE=AzureAppService",
    "NEXUSFLOW_SECRET_STORE=Environment",
    "NEXUSFLOW_STORAGE_MODE=AzureBlob",
    "NEXUSFLOW_STORAGE_CONTAINER=$containerName"
)

if ($UseAzureBlobState) {
    $settings += "NEXUSFLOW_STATE_STORE=AzureBlob"
} else {
    $settings += "NEXUSFLOW_STATE_STORE=File"
}

if ($UseAzureBlobDataProtection) {
    $settings += "NEXUSFLOW_DATA_PROTECTION_STORE=AzureBlob"
} else {
    $settings += "NEXUSFLOW_DATA_PROTECTION_STORE=File"
}

if (-not [string]::IsNullOrWhiteSpace($SetupKey)) {
    $settings += "NEXUSFLOW_SETUP_KEY=$SetupKey"
}

Write-Host "Applying App Service settings"
az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --settings $settings `
    --only-show-errors | Out-Null

if (-not [string]::IsNullOrWhiteSpace($DefaultConnectionString)) {
    Write-Warning "Passing connection strings on the command line can expose them through shell history. Prefer Key Vault references for production."
    az webapp config connection-string set `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --connection-string-type SQLAzure `
        --settings DefaultConnection="$DefaultConnectionString" `
        --only-show-errors | Out-Null
}

if (-not [string]::IsNullOrWhiteSpace($AzureBlobConnectionString)) {
    az webapp config connection-string set `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --connection-string-type Custom `
        --settings AzureBlobStorage="$AzureBlobConnectionString" `
        --only-show-errors | Out-Null
}

Write-Host "Restarting $WebAppName"
az webapp restart --resource-group $ResourceGroup --name $WebAppName --only-show-errors | Out-Null
Write-Host "Azure deployment settings applied. Open /install with the setup key to complete first-run provisioning."
