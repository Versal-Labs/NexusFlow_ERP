[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Deploy', 'Verify')][string]$Action,
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$CommitSha,
    [Parameter(Mandatory)][ValidatePattern('^\d+$')][string]$CiRunId,
    [Parameter(Mandatory)][ValidatePattern('^\d+$')][string]$CiRunAttempt,
    [string]$DatabaseBackupReference,
    [switch]$BackupConfirmed
)
$ErrorActionPreference = 'Stop'
Import-Module WebAdministration -ErrorAction Stop
Import-Module (Join-Path $PSScriptRoot 'Garnet.Release.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Garnet.Iis.psm1') -Force
$stateRoot = 'C:\NexusFlow_Setup\GarnetDeployment'
$backupRoot = 'C:\NexusFlow\Backups'
Assert-GarnetNoReparsePoint $stateRoot
Assert-GarnetNoReparsePoint $backupRoot
$config = Get-Content -LiteralPath (Join-Path $stateRoot 'target.json') -Raw | ConvertFrom-Json
$lock = $null
$pendingPath = Join-Path $stateRoot 'pending-release.json'
$latestPath = Join-Path $stateRoot 'latest-release.json'
$status = 'Failed'
try {
    # Cross-process lock also covers manual invocations outside GitHub Actions.
    $lock = [IO.File]::Open((Join-Path $stateRoot 'release.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $target = Get-GarnetIisTarget
    Assert-GarnetPinnedTarget $config $target
    if (Test-Path -LiteralPath (Join-Path $target.sitePath 'app_offline.htm')) { throw 'An existing offline file requires operator review before this release.' }
    $zip = (Resolve-Path -LiteralPath $PackagePath).Path
    $expectedHash = (Get-Content -LiteralPath ($zip + '.sha256') -Raw).Trim()
    if ($expectedHash -notmatch '^[a-fA-F0-9]{64}$' -or (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ine $expectedHash) {
        throw 'Release ZIP checksum mismatch.'
    }
    $stage = Join-Path $stateRoot ('staging\' + [Guid]::NewGuid().ToString('N'))
    Expand-GarnetSafeZip $zip $stage
    $manifest = Test-GarnetPackage $stage $CommitSha $CiRunId $CiRunAttempt
    $protectedPaths = @($target.instanceRoot) + @($config.localStoragePaths)
    Assert-GarnetDestination $target.sitePath $manifest $protectedPaths
    if ($Action -eq 'Verify') {
        $recordPath = $pendingPath
        if (!(Test-Path -LiteralPath $recordPath)) { $recordPath = $latestPath }
        $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
        Assert-GarnetVerificationRecord $record $CommitSha $CiRunId $CiRunAttempt
        Test-GarnetInstalledFiles $target.sitePath $manifest
        $status = Wait-GarnetHealth
    } else {
        if (!$BackupConfirmed -or [string]::IsNullOrWhiteSpace($DatabaseBackupReference)) {
            throw 'A current restorable database backup and its reference are required.'
        }
        if (Test-Path -LiteralPath $pendingPath) { throw 'Previous release is unresolved. Use Verify or recover it first.' }
        if ($target.installationMode -notin @('Installed', '2') -or (Wait-GarnetHealth) -ne 'Ready') {
            throw 'Existing installation must be ready before a new deployment.'
        }
        if ((Get-Website -Name $target.siteName).State -ne 'Started' -or (Get-WebAppPoolState -Name $target.poolName).Value -ne 'Started') {
            throw 'Start and verify the current installation before deploying.'
        }
        $backupSources = @($target.sitePath, $target.instanceRoot) + @($config.localStoragePaths)
        foreach ($source in $backupSources) { [void](Assert-GarnetBackupPath $source) }
        $backup = Join-Path $backupRoot ("Garnet-$CiRunId-$CiRunAttempt-" + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
        $payload = Join-Path $stage 'payload'
        $operations = @{
            Stop = { Stop-GarnetIis $target }.GetNewClosure()
            Backup = {
                [void](New-Item -ItemType Directory -Path $backup)
                Write-GarnetJson (Join-Path $backup 'release-manifest.json') $manifest
                Write-GarnetJson (Join-Path $backup 'recovery.json') ([ordered]@{
                    databaseBackupReference = $DatabaseBackupReference; siteName = $target.siteName
                    poolName = $target.poolName; sitePath = $target.sitePath; instanceRoot = $target.instanceRoot
                    storagePaths = @($config.localStoragePaths); backedUpAtUtc = [DateTime]::UtcNow.ToString('o')
                })
                Backup-GarnetDirectory $target.sitePath (Join-Path $backup 'site')
                Backup-GarnetDirectory $target.instanceRoot (Join-Path $backup 'instance')
                $index = 0
                foreach ($storage in $config.localStoragePaths) {
                    if ($storage -ine $target.instanceRoot -and $storage -ine $target.sitePath) {
                        Backup-GarnetDirectory $storage (Join-Path $backup "storage-$index")
                    }
                    $index++
                }
                $backup
            }.GetNewClosure()
            Copy = { Copy-GarnetPayload $payload $target.sitePath $manifest }.GetNewClosure()
            ValidateFiles = {
                Test-GarnetInstalledFiles $target.sitePath $manifest
                Assert-GarnetPinnedTarget $config (Get-GarnetIisTarget)
            }.GetNewClosure()
            Restore = { Restore-GarnetPayload (Join-Path $backup 'site') $target.sitePath $manifest }.GetNewClosure()
            Start = { Start-GarnetIis $target }.GetNewClosure()
            Health = { Wait-GarnetHealth }
        }
        $record = Invoke-GarnetFileUpdate $operations $pendingPath $manifest
        $status = $record.status
    }
    if ($status -eq 'Ready') {
        # Re-check configuration after startup too; app settings/secrets must remain server-owned.
        Assert-GarnetPinnedTarget $config (Get-GarnetIisTarget)
        Complete-GarnetVerification $record $pendingPath $latestPath
        Write-Host "Garnet release $CommitSha is ready."
    } elseif ($status -eq 'AwaitingUpgrade') {
        Write-Host 'Files deployed; awaiting the controlled database upgrade at https://nexus.garnetnation.lk/maintenance/upgrade.'
        throw 'Awaiting database upgrade. Run Verify with the same CI run after completing the maintenance screen.'
    } else { throw 'Release verification failed. Review server logs and the recorded release; do not reinstall.' }
} finally {
    if ($env:GITHUB_OUTPUT) { Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "release_status=$status" }
    if ($env:GITHUB_STEP_SUMMARY) {
        $summary = @("## Garnet release: $status", "", "- Action: $Action", "- CI run: $CiRunId / attempt $CiRunAttempt", "- Commit: $CommitSha", "- Target: C:\inetpub\wwwroot\Nexus-Garnet-Prod", "- Upgrade: https://nexus.garnetnation.lk/maintenance/upgrade", "- After an upgrade, run **Verify** for this CI run. Do not deploy another package or run /install.") -join "`n"
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary
    }
    if ($lock) { $lock.Dispose() }
}
