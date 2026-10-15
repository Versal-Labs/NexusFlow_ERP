# Update an existing NexusFlow IIS site — 15 October 2026

Keep the **existing server database**. Publish application files, retain server configuration and persistent data, and run a controlled schema upgrade only if pending migrations exist. Do not move the local UAT database or rerun imports.

This guide was checked against the current source. No server deployment has been executed. The hosted update has not been rehearsed here.

## Before starting

- Arrange downtime and stop users, integrations and other application instances from writing to the same database during backup/update.
- The commands target an existing Windows IIS site with a dedicated x64 pool, no child applications, and file-based installation state. Cloud state/storage needs its own backup procedure.
- Keep the same pool identity, bindings, certificate, environment variables and SQL connection. Existing Windows-encrypted credentials may depend on the machine/account.
- The server needs the **.NET 10 Hosting Bundle**, including the ASP.NET Core IIS module and x64 runtime. The PC needs the .NET 10 SDK. Listing runtimes alone does not prove that the IIS module is installed. [Microsoft IIS hosting guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0).
- The saved folder publish profiles currently have `DeleteExistingFiles=true`. Publish to a separate staging folder. Do not deploy with `/MIR` or `/PURGE`: these can delete destination files. [Robocopy documentation](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy).
- Do not use `deploy/Deploy-NexusFlow.ps1`, generate a setup key or run `/install` for an existing-site update.

The repository documents the Garnet target as `C:\inetpub\wwwroot\Nexus-Garnet-Prod`, URL `https://nexus.garnetnation.lk`. Confirm the actual target. The latest dashboard/performance fixes are currently in the working tree; a manual publish includes them. GitHub CI requires committing/pushing them first. If the registered release pipeline is already in use, follow [Garnet_Release_Workflow.md](Garnet_Release_Workflow.md) instead of mixing package formats.

## 1. IIS server: identify the existing site

Use **64-bit Windows PowerShell as Administrator**. Keep this window open: later server commands use its variables. These commands inspect the configuration.

```powershell
$ErrorActionPreference = 'Stop'
Import-Module WebAdministration
Get-Website | Select-Object Name, State, PhysicalPath, ApplicationPool

$siteName = Read-Host 'Exact existing IIS site name'
$sites = @(Get-Website | Where-Object { $_.Name -eq $siteName })
if ($sites.Count -ne 1) { throw 'Exactly one existing site must match.' }
$site = $sites[0]
$poolName = [string]$site.ApplicationPool
$sitePath = [IO.Path]::GetFullPath(
    [Environment]::ExpandEnvironmentVariables([string]$site.PhysicalPath)
).TrimEnd('\')
$pool = Get-Item -LiteralPath "IIS:\AppPools\$poolName"
if ($pool.enable32BitAppOnWin64) { throw 'This guide builds win-x64. Review the existing x86 deployment first.' }
if (@(Get-Website | Where-Object { $_.ApplicationPool -eq $poolName }).Count -ne 1 -or
    @(Get-WebApplication | Where-Object { $_.ApplicationPool -eq $poolName }).Count -ne 0 -or
    @(Get-WebApplication -Site $siteName).Count -ne 0) {
    throw 'These commands require a dedicated pool with no child IIS applications.'
}
if ($site.State -ne 'Started' -or
    (Get-WebAppPoolState -Name $poolName).Value -ne 'Started' -or
    (Test-Path -LiteralPath (Join-Path $sitePath 'app_offline.htm'))) {
    throw 'The site is already stopped/offline. Resolve its current maintenance state first.'
}

[xml]$webConfig = Get-Content -LiteralPath (Join-Path $sitePath 'web.config') -Raw
$asp = $webConfig.SelectSingleNode('//system.webServer/aspNetCore')
if (!$asp -or $asp.processPath -notin @('dotnet', '.\NexusFlow.Web.exe', 'NexusFlow.Web.exe') -or
    ($asp.processPath -eq 'dotnet' -and $asp.arguments -notmatch '^\.?[\\/]?NexusFlow\.Web\.dll$')) {
    throw 'Review the existing web.config launch command before using this package.'
}

# Read instance-location metadata only. Do not print credentials/environment dumps.
$instanceMetadata = @{}
$metadataNames = @('NEXUSFLOW_INSTANCE_ID', 'NEXUSFLOW_INSTANCE_ROOT')
foreach ($name in $metadataNames) {
    $value = [Environment]::GetEnvironmentVariable($name, 'Machine')
    if ($value) { $instanceMetadata[$name] = $value }
}
foreach ($entry in $pool.environmentVariables.Collection) {
    if ($entry.name -in $metadataNames) { $instanceMetadata[[string]$entry.name] = [string]$entry.value }
}
foreach ($entry in $asp.SelectNodes('environmentVariables/environmentVariable')) {
    if ($entry.name -in $metadataNames) { $instanceMetadata[[string]$entry.name] = [string]$entry.value }
}
$instanceId = [string]$instanceMetadata['NEXUSFLOW_INSTANCE_ID']
if (!$instanceId) { throw 'Confirm the existing server instance ID before proceeding.' }
$instanceRoot = Join-Path $env:ProgramData "NexusFlow\ERP\$instanceId"
if ($instanceMetadata['NEXUSFLOW_INSTANCE_ROOT']) {
    $instanceRoot = [Environment]::ExpandEnvironmentVariables([string]$instanceMetadata['NEXUSFLOW_INSTANCE_ROOT'])
}
$instanceRoot = [IO.Path]::GetFullPath($instanceRoot).TrimEnd('\')
if (!(Test-Path -LiteralPath (Join-Path $instanceRoot 'installation-state.json'))) {
    throw 'File installation state not found. Confirm deployment profile/state location.'
}
[pscustomobject]@{
    Site = $siteName; Pool = $poolName; Folder = $sitePath
    Instance = $instanceId; InstanceRoot = $instanceRoot
}
dotnet --list-runtimes
```

Record a working login and the current **server** A/R, A/P and stock totals. In **System Settings**, identify all actual local storage/upload roots, including custom paths outside the instance directory. Back up any remote storage separately. Do not assume the local UAT balances should appear on the server.

## 2. Development PC: publish the current application

Run on the PC. The existing release payload filter excludes workstation configuration, installation state, secrets, uploads, input spreadsheets and deployment scripts. It preserves application binaries, static assets and packaged document templates.

```powershell
$ErrorActionPreference = 'Stop'
Set-Location 'F:\source\NexusFlow_ERP'
Import-Module '.\deploy\Garnet.Release.psm1' -Force
$releaseRoot = Join-Path (Get-Location).Path (
    'artifacts\iis-manual-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N')
)
$published = Join-Path $releaseRoot 'publish'
$payload = Join-Path $releaseRoot 'payload'
New-Item -ItemType Directory -Path $payload -Force | Out-Null

dotnet publish '.\NexusFlow.Web\NexusFlow.Web.csproj' `
    --configuration Release --runtime win-x64 --self-contained false `
    -p:UseAppHost=true --output $published
if ($LASTEXITCODE -ne 0) { throw 'Publish failed. Do not deploy this output.' }

foreach ($file in Get-ChildItem -LiteralPath $published -File -Recurse -Force) {
    $relative = $file.FullName.Substring($published.Length + 1).Replace('\', '/')
    if (Test-GarnetPayloadPath $relative) {
        $destination = Join-Path $payload $relative
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
}
foreach ($required in @('NexusFlow.Web.dll', 'NexusFlow.Web.exe', 'NexusFlow.Web.runtimeconfig.json')) {
    if (!(Test-Path -LiteralPath (Join-Path $payload $required))) { throw "Incomplete package: $required" }
}
$zipPath = Join-Path $releaseRoot 'NexusFlow-IIS-Update.zip'
Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $zipPath
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
Write-Host "Copy this ZIP to the IIS server: $zipPath"
```

Keep the SHA-256 hash. This is a **manual update ZIP**, not a CI ZIP or older review-kit ZIP. Do not deploy the unfiltered `publish` folder or copy local configuration/database files.

## 3. IIS server: receive and check the package

Copy the ZIP to a setup folder outside the live site, for example `C:\NexusFlow_Setup`. Continue in the same server PowerShell window as step 1.

```powershell
$zipPath = Read-Host 'Full server path of NexusFlow-IIS-Update.zip'
$expectedHash = (Read-Host 'SHA-256 displayed on the PC').Trim()
if ($expectedHash -notmatch '^[A-Fa-f0-9]{64}$' -or
    (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash -ine $expectedHash) {
    throw 'Package hash mismatch. Do not deploy.'
}
$stamp = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N')
$stage = Join-Path 'C:\NexusFlow_Setup' "manual-$stamp"
$backup = Join-Path 'C:\NexusFlow\Backups' "manual-$stamp"
function Assert-SeparateFolders([string]$First, [string]$Second) {
    $a = [IO.Path]::GetFullPath($First).TrimEnd('\')
    $b = [IO.Path]::GetFullPath($Second).TrimEnd('\')
    if ($a -ieq $b -or $a.StartsWith($b + '\', [StringComparison]::OrdinalIgnoreCase) -or
        $b.StartsWith($a + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Folders overlap: $a and $b"
    }
}
foreach ($root in @($sitePath, $instanceRoot)) {
    Assert-SeparateFolders $root $stage
    Assert-SeparateFolders $root $backup
}
if ((Test-Path -LiteralPath $stage) -or (Test-Path -LiteralPath $backup)) {
    throw 'Staging/backup directories must be new.'
}
Expand-Archive -LiteralPath $zipPath -DestinationPath $stage
foreach ($required in @('NexusFlow.Web.dll', 'NexusFlow.Web.exe', 'NexusFlow.Web.runtimeconfig.json')) {
    if (!(Test-Path -LiteralPath (Join-Path $stage $required))) { throw "Wrong ZIP layout: missing $required at root." }
}
```

## 4. IIS server: stop and back up

Begin the agreed maintenance window. This stops only the selected site and dedicated pool. No copy should start until its worker has exited.

```powershell
Stop-Website -Name $siteName
Stop-WebAppPool -Name $poolName
$deadline = [DateTime]::UtcNow.AddSeconds(90)
do {
    $workers = @(Get-CimInstance -Namespace root/WebAdministration -ClassName WorkerProcess |
        Where-Object { $_.AppPoolName -eq $poolName })
    $stopped = (Get-WebAppPoolState -Name $poolName).Value -eq 'Stopped'
    if ($stopped -and $workers.Count -eq 0) { break }
    Start-Sleep -Seconds 2
} while ([DateTime]::UtcNow -lt $deadline)
if (!$stopped -or $workers.Count -ne 0) { throw 'Worker did not exit. Investigate before copying.' }

New-Item -ItemType Directory -Path $backup | Out-Null
icacls.exe $backup /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F'
if ($LASTEXITCODE -ne 0) { throw 'Could not protect backup folder.' }
function Copy-CheckedDirectory([string]$Source, [string]$Destination) {
    if (!(Test-Path -LiteralPath $Source -PathType Container)) { throw "Missing source: $Source" }
    Assert-SeparateFolders $Source $Destination
    robocopy.exe $Source $Destination /E /COPY:DAT /DCOPY:DAT /R:2 /W:2 /XJ
    if ($LASTEXITCODE -ge 8) { throw "Backup failed: $Source. Do not deploy." }
}
Copy-CheckedDirectory $sitePath (Join-Path $backup 'site')
Copy-CheckedDirectory $instanceRoot (Join-Path $backup 'instance')

# Site/instance backups already include directories physically inside those roots.
$externalStorage = (Read-Host 'EVERY external local storage/upload root, separated by semicolons; blank if none') -split ';' |
    ForEach-Object { $_.Trim() } | Where-Object { $_ }
$storageIndex = 0
foreach ($root in $externalStorage) {
    Assert-SeparateFolders $root $backup
    Assert-SeparateFolders $root $stage
    $storageIndex++
    Copy-CheckedDirectory $root (Join-Path $backup "storage-$storageIndex")
    Add-Content -LiteralPath (Join-Path $backup 'storage-locations.txt') -Value "storage-$storageIndex = $root"
}
$configHashes = @{}
Get-ChildItem -LiteralPath $sitePath -File -Force |
    Where-Object { $_.Name -eq 'web.config' -or $_.Name -like 'appsettings*' } |
    ForEach-Object { $configHashes[$_.Name] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
Write-Host "File/state backup: $backup"
```

Backups contain sensitive data: retain their protected ACLs. `/XJ` skips junctions; back up the real target of any linked storage explicitly. Back up remote storage separately where applicable.

**Before continuing, take and verify a current backup of the actual existing server database.** Use the existing SQL backup procedure or SSMS. Example SQL (replace placeholders and use a new filename) follows. The backup path is on the **SQL Server host**, writable by the SQL Server service account.

```sql
BACKUP DATABASE [YOUR_EXISTING_SERVER_DATABASE]
TO DISK = N'D:\SqlBackups\YOUR_DB_before_update_20261015_153000.bak'
WITH COPY_ONLY, CHECKSUM, STATS = 10;

RESTORE VERIFYONLY
FROM DISK = N'D:\SqlBackups\YOUR_DB_before_update_20261015_153000.bak'
WITH CHECKSUM;
```

`RESTORE VERIFYONLY` checks the backup; a test restore gives stronger recoverability evidence. Record the database name and backup reference. Keep the site stopped if backup fails. Do not restore the local UAT database over the server database.

## 5. IIS server: copy application files and restart

Continue only after backups succeeded. The copy preserves destination-only files and excludes server configuration and persistent folders. It verifies the copied payload and original configuration hashes before restarting.

```powershell
if ((Get-WebAppPoolState -Name $poolName).Value -ne 'Stopped' -or
    (Get-Website -Name $siteName).State -ne 'Stopped') { throw 'Site/pool must remain stopped during copy.' }
$excludedFiles = @('web.config', 'web.config.*', 'appsettings*', 'app_offline.htm*',
    'installation-state.json*', 'secrets.*', 'secrets-*', 'secret.key*', 'bootstrap.key*',
    '*.pfx', '*.p12', '*.pem', '*.key', '*.bak', '*.mdf', '*.ldf')
$excludedDirs = @('uploads', 'upload', 'storage', 'logs', 'log', 'data-protection', 'App_Data')
robocopy.exe $stage $sitePath /E /COPY:DAT /DCOPY:DAT /IS /IT /R:2 /W:2 /XJ `
    /XF $excludedFiles /XD $excludedDirs
if ($LASTEXITCODE -ge 8) { throw 'Copy failed. Leave site stopped; do not start a partial update.' }

# The ZIP from step 2 contains application payload only.
foreach ($file in Get-ChildItem -LiteralPath $stage -File -Recurse -Force) {
    $relative = $file.FullName.Substring($stage.Length + 1)
    $destination = Join-Path $sitePath $relative
    if (!(Test-Path -LiteralPath $destination -PathType Leaf) -or
        (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ine
        (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) {
        throw "Payload verification failed: $relative. Leave site stopped."
    }
}
foreach ($name in $configHashes.Keys) {
    if ((Get-FileHash -LiteralPath (Join-Path $sitePath $name) -Algorithm SHA256).Hash -ine $configHashes[$name]) {
        throw "Server configuration changed: $name. Leave site stopped."
    }
}
Start-WebAppPool -Name $poolName
Start-Website -Name $siteName
```

Do not use `iisreset`; leave other sites running. Preserve the server `web.config`. Both DLL and Windows app host are included, supporting the launch configurations checked in step 1.

## 6. Upgrade if required, then check the site

Open the existing HTTPS website. If the application reports **UpgradeRequired** or opens `/maintenance/upgrade`:

1. Sign in with the existing **SuperAdmin** account or legacy **Admin** upgrade authority.
2. Review **Pending migrations**. All pending migrations are applied, including older ones missing on this server. The latest dashboard/performance fixes themselves add no schema migration.
3. Confirm the current backup, then tick **I confirm that a current, restorable database backup exists.**
4. Click **Run controlled upgrade** once. Do not run a parallel `dotnet ef database update`.
5. The application requests a restart after success. Reload; if necessary use `Restart-WebAppPool -Name $poolName` for this pool only.

If the site unexpectedly opens `/install`, investigate the preserved instance environment, state, credentials and pool identity. Do not reinstall or edit installation state/migration history to bypass checks.

After any upgrade completes:

```powershell
$baseUrl = (Read-Host 'Existing HTTPS URL, for example https://nexus.garnetnation.lk').TrimEnd('/')
$live = Invoke-RestMethod -Uri "$baseUrl/health/live" -TimeoutSec 30
if ($live.status -ne 'live' -or $live.instance -cne $instanceId) { throw 'Wrong or unhealthy instance.' }
$ready = Invoke-RestMethod -Uri "$baseUrl/health/ready" -TimeoutSec 30
if ($ready.status -ne 'ready') { throw 'Application is not ready.' }
$live
$ready
```

Expect HTTP 200 with `status=live` and the existing instance ID, then HTTP 200 with `status=ready`. Readiness returns HTTP 503 while an upgrade is required or readiness checks fail. A running pool/login page alone is insufficient.

Before reopening normal work, check login, dashboard A/R and A/P against the **server's** outstanding reports, stock, an existing customer/supplier and **Customer Receipts** at `/Treasury/Receipts`. Use Ctrl+F5 for updated scripts/styles. Check application logs and Windows Event Viewer if startup fails. Use approved UAT transactions for a posting smoke test; do not create test receipts in a production ledger without business approval.

The finalized local UAT project and balances are not transferred. Server records remain; review any changes arising from pending schema/data migrations.

## If an operation fails

- Stop at the failed step. Do not continue to the restart commands after backup/copy/hash errors. Keep users out while troubleshooting.
- Before a database upgrade, recover the previous application payload from the protected `site` backup with the site/pool stopped. Preserve original configuration/state/storage. Account for files introduced by the new release: copying old files over new ones does not remove additions.
- After a database upgrade, older binaries may not be compatible. Keep the site offline and assess application, database, installation state and storage as one recovery set. Restoring a pre-update database can discard later transactions; require an explicit recovery decision.
- A normal update does not reset, replace or restore the existing database.

Source checked: web project file, `Program.cs`, `ApplicationModeMiddleware`, `MaintenanceController`, upgrade view, publish profiles, `deploy/Garnet.Release.psm1` and `deploy/Garnet.Iis.psm1`.