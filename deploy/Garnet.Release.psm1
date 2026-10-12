Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-GarnetFullPath {
    param([Parameter(Mandatory)][string]$Path)
    if (![IO.Path]::IsPathRooted($Path)) { throw "Expected an absolute path: $Path" }
    [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
}

function Assert-GarnetChildPath {
    param([string]$Root, [string]$Path)
    $rootPath = Get-GarnetFullPath $Root
    $fullPath = Get-GarnetFullPath $Path
    if (!$fullPath.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the expected directory: $fullPath"
    }
    $fullPath
}

function Assert-GarnetNoReparsePoint {
    param([string]$Path)
    $cursor = Get-GarnetFullPath $Path
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Reparse points are not supported: $cursor"
            }
        }
        $parent = [IO.Directory]::GetParent($cursor)
        if (!$parent) { break }
        $cursor = $parent.FullName
    }
}

function Test-GarnetPayloadPath {
    param([string]$Path)
    if (!$Path -or $Path -match '(^/|\\|:|[\x00-\x1f])' -or $Path -match '(^|/)\.\.?(/|$)') { return $false }
    foreach ($segment in $Path.Split('/')) {
        if (!$segment -or $segment -match '[<>"|?*]' -or $segment -match '[ .]$' -or
            $segment -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)') { return $false }
    }
    $name = ($Path.Split('/'))[-1]
    if ($name -match '^(web\.config($|\.)|appsettings.*|installation-state\.json($|\.)|secrets[.-].*|secret\.key($|\.)|bootstrap\.key($|\.)|app_offline\.htm($|\.)|\.env.*)' -or
        $name -match '\.(pdb|pfx|p12|pem|key|bak|mdf|ldf|ps1|psm1|cmd|bat|cs|sln|csproj|xls|xlsx|csv|log)$' -or
        $Path -match '(^|/)(uploads?|storage|logs?|data-protection|App_Data|\.git|\.vs|tools)(/|$)' -or
        $Path -match '(^|/)\.') { return $false }
    return $true
}

function Write-GarnetJson {
    param([string]$Path, [object]$Value)
    $json = $Value | ConvertTo-Json -Depth 20
    [IO.File]::WriteAllText($Path + '.tmp', $json, [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath ($Path + '.tmp') -Destination $Path -Force
}

function Assert-GarnetCiMetadata {
    param([object]$Run, [object[]]$Artifacts, [string]$CiRunId)
    if ($Run.status -cne 'completed' -or $Run.conclusion -cne 'success' -or $Run.event -cne 'push' -or
        $Run.head_branch -cne 'main' -or $Run.head_repository.full_name -cne 'Versal-Labs/NexusFlow_ERP' -or
        $Run.path -cnotmatch '^\.github/workflows/nexusflow-ci\.yml(@(refs/heads/)?main)?$' -or $Run.head_sha -cnotmatch '^[a-f0-9]{40}$' -or
        "$($Run.id)" -cne $CiRunId) { throw 'Select a completed, successful push-to-main run of NexusFlow CI.' }
    $name = "garnet-release-$CiRunId-$($Run.run_attempt)"
    $matches = @($Artifacts | Where-Object { $_.name -ceq $name -and !$_.expired })
    if ($matches.Count -ne 1 -or $matches[0].workflow_run.head_sha -cne $Run.head_sha) {
        throw 'Expected exactly one unexpired release artifact from the successful CI attempt.'
    }
    $name
}

function Expand-GarnetSafeZip {
    param([string]$ZipPath, [string]$Destination)
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $Destination) { throw 'Extraction destination must be new.' }
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $seen = @{}
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.TrimEnd('/')
            if (!$name) { continue }
            if ($name -ne 'release-manifest.json' -and $name -ne 'payload' -and
                !($name.StartsWith('payload/') -and (Test-GarnetPayloadPath $name.Substring(8)))) {
                throw "Unexpected or unsafe archive entry: $name"
            }
            if ($seen.ContainsKey($name.ToLowerInvariant())) { throw "Duplicate archive entry: $name" }
            $seen[$name.ToLowerInvariant()] = $true
            if ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) { throw 'Archive contains a symbolic link.' }
            [void](Assert-GarnetChildPath $Destination (Join-Path $Destination $name))
        }
        [IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $Destination)
    } finally { $archive.Dispose() }
}

function Test-GarnetPackage {
    param([string]$PackageRoot, [string]$CommitSha, [string]$CiRunId, [string]$CiRunAttempt)
    $manifest = Get-Content -LiteralPath (Join-Path $PackageRoot 'release-manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1 -or $manifest.repository -cne 'Versal-Labs/NexusFlow_ERP' -or
        $manifest.branch -cne 'main' -or $manifest.commitSha -cne $CommitSha -or
        "$($manifest.ciRunId)" -cne $CiRunId -or "$($manifest.ciRunAttempt)" -cne $CiRunAttempt) {
        throw 'Release manifest provenance does not match the approved CI run.'
    }
    if ($CommitSha -cnotmatch '^[a-f0-9]{40}$' -or $CiRunId -notmatch '^\d+$' -or $CiRunAttempt -notmatch '^\d+$') {
        throw 'Invalid CI provenance identifiers.'
    }
    $payload = Join-Path $PackageRoot 'payload'
    $seen = @{}
    foreach ($file in $manifest.files) {
        if (!(Test-GarnetPayloadPath $file.path) -or $file.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid manifest file entry.' }
        $key = $file.path.ToLowerInvariant()
        if ($seen.ContainsKey($key)) { throw 'Duplicate manifest file.' }
        $seen[$key] = $true
        $path = Assert-GarnetChildPath $payload (Join-Path $payload $file.path)
        Assert-GarnetNoReparsePoint $path
        if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $file.sha256) {
            throw "Missing or corrupted payload file: $($file.path)"
        }
    }
    if (!$seen.ContainsKey('nexusflow.web.dll') -or !$seen.ContainsKey('nexusflow.web.runtimeconfig.json')) {
        throw 'Package does not contain the published web application.'
    }
    $actual = @(Get-ChildItem -LiteralPath $payload -File -Recurse -Force)
    if ($actual.Count -ne $seen.Count) { throw 'Payload contains files not listed in the manifest.' }
    foreach ($file in $actual) {
        $relative = $file.FullName.Substring((Get-GarnetFullPath $payload).Length + 1).Replace('\', '/')
        if (!$seen.ContainsKey($relative.ToLowerInvariant())) { throw 'Unlisted payload file.' }
    }
    $manifest
}

function Assert-GarnetDestination {
    param([string]$SitePath, [object]$Manifest, [string[]]$ProtectedPaths = @())
    foreach ($file in $Manifest.files) {
        $path = Assert-GarnetChildPath $SitePath (Join-Path $SitePath $file.path)
        Assert-GarnetNoReparsePoint $path
        if (Test-Path -LiteralPath $path -PathType Container) { throw "Payload destination is an existing directory: $($file.path)" }
        $parent = [IO.Path]::GetDirectoryName($path)
        while ($parent -ine (Get-GarnetFullPath $SitePath)) {
            if (Test-Path -LiteralPath $parent -PathType Leaf) { throw "Payload parent is an existing file: $($file.path)" }
            $parent = [IO.Path]::GetDirectoryName($parent)
        }
        foreach ($protected in $ProtectedPaths) {
            $root = Get-GarnetFullPath $protected
            if ($path -ieq $root -or $path.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) {
                throw "Payload would overwrite user storage: $($file.path)"
            }
        }
    }
}

function Copy-GarnetPayload {
    param([string]$Source, [string]$Destination, [object]$Manifest)
    foreach ($file in $Manifest.files) {
        $path = Assert-GarnetChildPath $Destination (Join-Path $Destination $file.path)
        Assert-GarnetNoReparsePoint $path
        [void](New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($path)) -Force)
        Copy-Item -LiteralPath (Join-Path $Source $file.path) -Destination $path -Force
    }
}

function Restore-GarnetPayload {
    param([string]$BackupSite, [string]$SitePath, [object]$Manifest)
    # Restore only release payload paths; never delete a directory or restore instance state.
    foreach ($file in $Manifest.files) {
        $path = Assert-GarnetChildPath $SitePath (Join-Path $SitePath $file.path)
        $backup = Assert-GarnetChildPath $BackupSite (Join-Path $BackupSite $file.path)
        Assert-GarnetNoReparsePoint $path
        if (Test-Path -LiteralPath $backup -PathType Leaf) {
            Copy-Item -LiteralPath $backup -Destination $path -Force
        } elseif (Test-Path -LiteralPath $path -PathType Leaf) {
            Remove-Item -LiteralPath $path -Force
        }
    }
}

function Test-GarnetInstalledFiles {
    param([string]$SitePath, [object]$Manifest)
    foreach ($file in $Manifest.files) {
        $path = Assert-GarnetChildPath $SitePath (Join-Path $SitePath $file.path)
        Assert-GarnetNoReparsePoint $path
        if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $file.sha256) {
            throw "Deployed release hash mismatch: $($file.path)"
        }
    }
}

function Assert-GarnetVerificationRecord {
    param([object]$Record, [string]$CommitSha, [string]$CiRunId, [string]$CiRunAttempt)
    if ($Record.phase -cne 'Started' -or $Record.commitSha -cne $CommitSha -or
        "$($Record.ciRunId)" -cne $CiRunId -or "$($Record.ciRunAttempt)" -cne $CiRunAttempt) {
        throw 'Verification must select the deployed release recorded on this server.'
    }
}

function Complete-GarnetVerification {
    param([object]$Record, [string]$PendingPath, [string]$LatestPath)
    $Record.status = 'Ready'
    $Record | Add-Member -NotePropertyName verifiedAtUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o')) -Force
    Write-GarnetJson $LatestPath $Record
    if (Test-Path -LiteralPath $PendingPath) { Remove-Item -LiteralPath $PendingPath -Force }
}

function Get-GarnetHealthStatus {
    param([object]$Live, [object]$Ready, [string]$ExpectedInstance = 'nexus-garnet-2026')
    if ($null -eq $Live -or $null -eq $Ready -or $null -eq $Live.Body -or $null -eq $Ready.Body) { return 'Failed' }
    if ($Live.StatusCode -ne 200 -or !$Live.Body.PSObject.Properties['status'] -or !$Live.Body.PSObject.Properties['instance'] -or
        $Live.Body.status -ne 'live' -or $Live.Body.instance -cne $ExpectedInstance) {
        return 'Failed'
    }
    if ($Ready.StatusCode -eq 200 -and $Ready.Body.PSObject.Properties['status'] -and $Ready.Body.status -eq 'ready') { return 'Ready' }
    if ($Ready.StatusCode -eq 503 -and $Ready.Body.PSObject.Properties['mode'] -and $Ready.Body.mode -eq 'UpgradeRequired') { return 'AwaitingUpgrade' }
    return 'Failed'
}

function Invoke-GarnetFileUpdate {
    param([hashtable]$Operations, [string]$PendingPath, [object]$Release)
    if (Test-Path -LiteralPath $PendingPath) { throw 'A previous release is unresolved. Verify or recover it before another deployment.' }
    $record = [ordered]@{
        schemaVersion = 1; commitSha = $Release.commitSha; ciRunId = $Release.ciRunId
        ciRunAttempt = $Release.ciRunAttempt; phase = 'Preparing'; status = 'InProgress'
        startedAtUtc = [DateTime]::UtcNow.ToString('o'); backupPath = $null
    }
    Write-GarnetJson $PendingPath $record
    $copyAttempted = $false
    $appMayHaveStarted = $false
    $stopped = $false
    try {
        & $Operations.Stop
        $stopped = $true
        $record.backupPath = & $Operations.Backup
        $record.phase = 'BackedUp'
        Write-GarnetJson $PendingPath $record
        $copyAttempted = $true
        & $Operations.Copy
        & $Operations.ValidateFiles
        $record.phase = 'Started'
        # Persist BEFORE starting: even a failed start might have launched the new application.
        Write-GarnetJson $PendingPath $record
        $appMayHaveStarted = $true
        & $Operations.Start
        $status = & $Operations.Health
        if ($status -notin @('Ready', 'AwaitingUpgrade')) { throw 'Release health checks failed.' }
        $record.status = $status
        Write-GarnetJson $PendingPath $record
        [pscustomobject]$record
    } catch {
        if (!$appMayHaveStarted -and $stopped) {
            try {
                if ($copyAttempted) { & $Operations.Restore }
                & $Operations.Start
                $record.status = 'RecoveredBeforeStartup'
                Write-GarnetJson $PendingPath $record
                Remove-Item -LiteralPath $PendingPath -Force
            } catch {
                $record.status = 'RecoveryFailed'
                Write-GarnetJson $PendingPath $record
                throw 'Pre-start recovery failed. Leave users blocked and recover from the recorded backup.'
            }
        } else {
            $record.status = 'Failed'
            Write-GarnetJson $PendingPath $record
        }
        throw
    }
}

Export-ModuleMember -Function *-Garnet*
