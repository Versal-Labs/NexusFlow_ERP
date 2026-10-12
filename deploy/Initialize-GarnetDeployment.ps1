[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DeploymentAccount,
    [Parameter(Mandatory)][string[]]$LocalStoragePaths,
    [Parameter(Mandatory)][switch]$ConfirmAllLocalStoragePaths,
    [switch]$Refresh
)
$ErrorActionPreference = 'Stop'
if (!$ConfirmAllLocalStoragePaths) { throw 'Confirm the local storage paths from the existing application settings first.' }
Import-Module (Join-Path $PSScriptRoot 'Garnet.Release.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Garnet.Iis.psm1') -Force
$target = Get-GarnetIisTarget
if ($target.installationMode -notin @('Installed', '2')) { throw 'Finish the existing installation upgrade before registering this deployment target.' }
$stateRoot = 'C:\NexusFlow_Setup\GarnetDeployment'
$backupRoot = 'C:\NexusFlow\Backups'
Assert-GarnetNoReparsePoint $stateRoot
Assert-GarnetNoReparsePoint $backupRoot
$configPath = Join-Path $stateRoot 'target.json'
[void](New-Item -ItemType Directory -Path $stateRoot -Force)
$lock = [IO.File]::Open((Join-Path $stateRoot 'release.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
if (Test-Path -LiteralPath (Join-Path $stateRoot 'pending-release.json')) { throw 'Resolve the pending release before refreshing target settings.' }
if ((Test-Path -LiteralPath $configPath) -and !$Refresh) { throw 'Target already exists; use -Refresh only after reviewing configuration changes.' }
$storage = @($LocalStoragePaths | ForEach-Object { Assert-GarnetBackupPath $_ } | Select-Object -Unique)
if (!$storage.Count) { throw 'At least one confirmed local storage directory is required.' }
[void](Assert-GarnetBackupPath $target.sitePath)
[void](Assert-GarnetBackupPath $target.instanceRoot)
foreach ($path in @($stateRoot, $backupRoot)) {
    [void](New-Item -ItemType Directory -Path $path -Force)
    & icacls.exe $path /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)(F)' '*S-1-5-32-544:(OI)(CI)(F)' "${DeploymentAccount}:(OI)(CI)(M)"
    if ($LASTEXITCODE -ne 0) { throw 'Could not protect deployment state/backup directory.' }
}
$value = [ordered]@{ schemaVersion = 1 }
foreach ($property in $target.PSObject.Properties) { if ($property.Name -ne 'installationMode') { $value[$property.Name] = $property.Value } }
$value['localStoragePaths'] = $storage
$value['registeredAtUtc'] = [DateTime]::UtcNow.ToString('o')
Write-GarnetJson $configPath $value
Write-Host "Registered existing Garnet target: $($target.siteName), pool $($target.poolName)."
Write-Host "Protected target configuration: $configPath"
} finally { $lock.Dispose() }
