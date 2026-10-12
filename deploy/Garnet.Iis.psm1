Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Garnet.Release.psm1') -Force

function Get-GarnetProperty {
    param([object]$Object, [string]$Name, [object]$Default = $null)
    if ($null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]) { return $Object.$Name }
    return $Default
}

function Get-GarnetIisTarget {
    Import-Module WebAdministration -ErrorAction Stop
    $sitePath = 'C:\inetpub\wwwroot\Nexus-Garnet-Prod'
    Assert-GarnetNoReparsePoint $sitePath
    $allSites = @(Get-Website)
    $site = Resolve-GarnetIisSite $allSites
    $poolName = [string]$site.ApplicationPool
    if (@(Get-Website | Where-Object { $_.ApplicationPool -eq $poolName }).Count -ne 1 -or
        @(Get-WebApplication | Where-Object { $_.ApplicationPool -eq $poolName }).Count -ne 0 -or
        @(Get-WebApplication -Site $site.Name).Count -ne 0) {
        throw 'Garnet must use a dedicated application pool with no child IIS applications.'
    }
    $bindings = @(Get-WebBinding -Name $site.Name -Protocol https | Where-Object {
        $_.bindingInformation -match ':443:nexus\.garnetnation\.lk$'
    })
    if ($bindings.Count -ne 1) { throw 'Expected the existing nexus.garnetnation.lk HTTPS binding.' }
    $pool = Get-Item -LiteralPath "IIS:\AppPools\$poolName"
    $environment = @{}
    foreach ($entry in [Environment]::GetEnvironmentVariables('Machine').GetEnumerator()) {
        $environment[[string]$entry.Key] = [string]$entry.Value
    }
    foreach ($entry in $pool.environmentVariables.Collection) { $environment[[string]$entry.name] = [string]$entry.value }
    $configPath = Join-Path $sitePath 'web.config'
    [xml]$config = Get-Content -LiteralPath $configPath -Raw
    $asp = $config.SelectSingleNode('//system.webServer/aspNetCore')
    if (!$asp -or $asp.processPath -notin @('dotnet', '.\NexusFlow.Web.exe', 'NexusFlow.Web.exe') -or
        ($asp.processPath -eq 'dotnet' -and $asp.arguments -notmatch '^\.?[\\/]?NexusFlow\.Web\.dll$')) {
        throw 'Existing web.config does not launch the expected NexusFlow application.'
    }
    foreach ($entry in $asp.SelectNodes('environmentVariables/environmentVariable')) {
        $environment[[string]$entry.name] = [string]$entry.value
    }
    if (!$environment.ContainsKey('NEXUSFLOW_INSTANCE_ID') -or $environment['NEXUSFLOW_INSTANCE_ID'] -cne 'nexus-garnet-2026') {
        throw 'Effective IIS instance ID does not match nexus-garnet-2026.'
    }
    $instanceRoot = Join-Path $env:ProgramData 'NexusFlow\ERP\nexus-garnet-2026'
    if ($environment.ContainsKey('NEXUSFLOW_INSTANCE_ROOT') -and $environment['NEXUSFLOW_INSTANCE_ROOT']) {
        $instanceRoot = [Environment]::ExpandEnvironmentVariables($environment['NEXUSFLOW_INSTANCE_ROOT'])
    }
    $instanceRoot = Get-GarnetFullPath $instanceRoot
    Assert-GarnetNoReparsePoint $instanceRoot
    # This first pipeline supports the existing Windows/file installation. Cloud state needs its own backup adapter.
    $settings = $null
    foreach ($settingsPath in @('appsettings.json', 'appsettings.Production.json')) {
        $path = Join-Path $sitePath $settingsPath
        if (Test-Path -LiteralPath $path) {
            $current = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            $deployment = Get-GarnetProperty $current 'Deployment'
            if ($deployment) {
                foreach ($key in @('StateStore', 'DataProtectionStore', 'Profile')) {
                    $value = Get-GarnetProperty $deployment $key
                    if ($value) {
                        if (!$settings) { $settings = @{} }
                        $settings[$key] = [string]$value
                    }
                }
            }
        }
    }
    foreach ($pair in @(@('StateStore', 'NEXUSFLOW_STATE_STORE'), @('DataProtectionStore', 'NEXUSFLOW_DATA_PROTECTION_STORE'))) {
        $mode = 'File'
        if ($settings -and $settings.ContainsKey($pair[0])) { $mode = $settings[$pair[0]] }
        if ($environment.ContainsKey($pair[1]) -and $environment[$pair[1]]) { $mode = $environment[$pair[1]] }
        if ($mode -ine 'File') { throw 'Cloud installation state/data protection requires a separate backup adapter.' }
    }
    $profile = 'WindowsIis'
    if ($settings -and $settings.ContainsKey('Profile')) { $profile = $settings['Profile'] }
    if ($environment.ContainsKey('NEXUSFLOW_DEPLOYMENT_PROFILE') -and $environment['NEXUSFLOW_DEPLOYMENT_PROFILE']) {
        $profile = $environment['NEXUSFLOW_DEPLOYMENT_PROFILE']
    }
    if ($profile -ine 'WindowsIis') { throw 'This runner targets the existing WindowsIis deployment only.' }
    if ($environment.ContainsKey('ASPNETCORE_ENVIRONMENT') -and $environment['ASPNETCORE_ENVIRONMENT'] -ine 'Production') {
        throw 'Expected the Production IIS runtime environment.'
    }
    $state = Get-Content -LiteralPath (Join-Path $instanceRoot 'installation-state.json') -Raw | ConvertFrom-Json
    if ($state.InstanceId -cne 'nexus-garnet-2026' -or "$($state.Mode)" -notin @('Installed', '2', 'UpgradeRequired', '3')) {
        throw 'Existing installation state is missing, uninstalled or invalid.'
    }
    $configHashes = @{}
    foreach ($file in @(Get-Item -LiteralPath $configPath) + @(Get-ChildItem -LiteralPath $sitePath -Filter 'appsettings*.json' -File)) {
        $configHashes[$file.Name] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
    # Hash environment values without logging or storing connection strings.
    $envText = ($environment.GetEnumerator() | Where-Object { $_.Key -match '^(NEXUSFLOW_|ASPNETCORE_|ConnectionStrings|SQLCONNSTR_|CUSTOMCONNSTR_)' } |
        Sort-Object Key | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "`n"
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { $environmentHash = ([BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($envText)))).Replace('-', '') }
    finally { $hasher.Dispose() }
    [pscustomobject]@{
        sitePath = $sitePath; siteName = [string]$site.Name; poolName = $poolName
        instanceId = 'nexus-garnet-2026'; instanceRoot = $instanceRoot
        poolIdentity = "$($pool.processModel.identityType):$($pool.processModel.userName)"
        binding = [string]$bindings[0].bindingInformation
        certificateHash = [string]$bindings[0].certificateHash
        configurationHashes = $configHashes; environmentHash = $environmentHash
        installationMode = [string]$state.Mode
    }
}

function Assert-GarnetPinnedTarget {
    param([object]$Expected, [object]$Actual)
    if ($Expected.schemaVersion -ne 1) { throw 'Unsupported server target configuration.' }
    foreach ($key in @('sitePath', 'siteName', 'poolName', 'instanceId', 'instanceRoot', 'poolIdentity', 'binding', 'environmentHash')) {
        if ($Expected.$key -cne $Actual.$key) { throw "Server target changed: $key. Review and refresh the server target before release." }
    }
    # Compare keys individually; JSON key ordering is not meaningful.
    $names = @($Expected.configurationHashes.PSObject.Properties.Name)
    if ($Actual.configurationHashes.Count -ne $names.Count) { throw 'Server configuration file set changed.' }
    foreach ($name in $names) {
        if (!$Actual.configurationHashes.ContainsKey($name) -or $Actual.configurationHashes[$name] -cne $Expected.configurationHashes.$name) {
            throw "Server configuration changed: $name"
        }
    }
}

function Assert-GarnetBackupPath {
    param([string]$Path)
    $full = Get-GarnetFullPath $Path
    if ($full -eq [IO.Path]::GetPathRoot($full).TrimEnd('\') -or $full -match '^[A-Za-z]:\\(Windows|Users|ProgramData|Program Files( \(x86\))?|inetpub(\\wwwroot)?)$' -or
        $full.StartsWith('C:\NexusFlow_Setup', [StringComparison]::OrdinalIgnoreCase) -or
        $full.StartsWith('C:\NexusFlow\Backups', [StringComparison]::OrdinalIgnoreCase)) { throw "Backup source is too broad or overlaps release folders: $full" }
    if (!(Test-Path -LiteralPath $full -PathType Container)) { throw "Backup source directory is missing: $full" }
    Assert-GarnetNoReparsePoint $full
    foreach ($entry in Get-ChildItem -LiteralPath $full -Recurse -Force) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Backup source contains a reparse point: $($entry.FullName)" }
    }
    $full
}

function Stop-GarnetIis {
    param([object]$Target)
    if ((Get-Website -Name $Target.siteName).State -ne 'Stopped') { Stop-Website -Name $Target.siteName }
    if ((Get-WebAppPoolState -Name $Target.poolName).Value -ne 'Stopped') { Stop-WebAppPool -Name $Target.poolName }
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    do {
        $workers = @(Get-CimInstance -Namespace root/WebAdministration -ClassName WorkerProcess |
            Where-Object { $_.AppPoolName -eq $Target.poolName })
        if (!$workers.Count -and (Get-WebAppPoolState -Name $Target.poolName).Value -eq 'Stopped') { return }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Application pool did not release its worker processes within 90 seconds.'
}

function Start-GarnetIis {
    param([object]$Target)
    if ((Get-WebAppPoolState -Name $Target.poolName).Value -ne 'Started') { Start-WebAppPool -Name $Target.poolName }
    if ((Get-Website -Name $Target.siteName).State -ne 'Started') { Start-Website -Name $Target.siteName }
}

function Backup-GarnetDirectory {
    param([string]$Source, [string]$Destination)
    [void](New-Item -ItemType Directory -Path $Destination -Force)
    & robocopy.exe $Source $Destination /E /COPY:DAT /DCOPY:DAT /R:2 /W:2 /XJ /NFL /NDL /NJH /NJS | Out-Host
    if ($LASTEXITCODE -ge 8) { throw 'Backup copy failed; no release files will be copied.' }
    # Verify backup completeness, not just the robocopy exit code.
    $root = Get-GarnetFullPath $Source
    foreach ($file in Get-ChildItem -LiteralPath $root -File -Recurse -Force) {
        $relative = $file.FullName.Substring($root.Length + 1)
        $copy = Join-Path $Destination $relative
        if (!(Test-Path -LiteralPath $copy -PathType Leaf) -or
            (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -cne (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash) {
            throw 'Backup verification failed; no release files will be copied.'
        }
    }
}

function Get-GarnetHttpResponse {
    param([string]$Path)
    $uri = 'https://nexus.garnetnation.lk' + $Path + '?release_check=' + [Guid]::NewGuid().ToString('N')
    try {
        $response = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 10 -MaximumRedirection 0 -Headers @{ 'Cache-Control' = 'no-cache' }
        [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Body = ($response.Content | ConvertFrom-Json) }
    } catch {
        $code = 0
        $body = [pscustomobject]@{ status = 'invalid'; mode = ''; instance = '' }
        # Windows PowerShell may have already consumed the HTTP error stream into ErrorDetails.
        if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
            try { $body = $_.ErrorDetails.Message | ConvertFrom-Json } catch { }
        }
        if ($_.Exception.PSObject.Properties['Response'] -and $_.Exception.Response) {
            $code = [int]$_.Exception.Response.StatusCode
            if ($null -eq $body -or !$body.PSObject.Properties['status'] -or $body.status -eq 'invalid') { try {
                $reader = [IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
                try { $body = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
            } catch { } }
        }
        [pscustomobject]@{ StatusCode = $code; Body = $body }
    }
}

function Resolve-GarnetIisSite {
    param([object[]]$Sites)
    $matches = @($Sites | Where-Object {
        [Environment]::ExpandEnvironmentVariables($_.PhysicalPath).TrimEnd('\') -ieq 'C:\inetpub\wwwroot\Nexus-Garnet-Prod'
    })
    if ($matches.Count -ne 1) { throw 'Expected exactly one existing Garnet site at the pinned physical path.' }
    $matches[0]
}

function Wait-GarnetHealth {
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    do {
        $status = Get-GarnetHealthStatus (Get-GarnetHttpResponse '/health/live') (Get-GarnetHttpResponse '/health/ready')
        if ($status -in @('Ready', 'AwaitingUpgrade')) { return $status }
        Start-Sleep -Seconds 5
    } while ([DateTime]::UtcNow -lt $deadline)
    return 'Failed'
}

Export-ModuleMember -Function *-Garnet*
