# No IIS changes, database connections, administrator rights or external test modules required.
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '..\Garnet.Release.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\Garnet.Iis.psm1') -Force
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('NexusFlow-ReleaseTests-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $testRoot)
$sha = 'a' * 40
$script:passed = 0
$script:failed = 0

function Assert-Equal($Actual, $Expected) {
    if ($Actual -cne $Expected) { throw "Expected '$Expected', got '$Actual'." }
}
function Assert-Throws([scriptblock]$Body, [string]$Message = '') {
    $caught = $false
    try { & $Body } catch {
        $caught = $true
        if ($Message -and $_.Exception.Message -notlike "*$Message*") { throw "Unexpected error: $($_.Exception.Message)" }
    }
    if (!$caught) { throw 'Expected an exception.' }
}
function Test-Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $script:passed++; Write-Host "PASS $Name" }
    catch { $script:failed++; Write-Host "FAIL $Name -- $($_.Exception.Message)" }
}
function New-Fixture {
    $root = Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))
    foreach ($dir in @('publish', 'site', 'backup')) { [void](New-Item -ItemType Directory -Path (Join-Path $root $dir) -Force) }
    [IO.File]::WriteAllText((Join-Path $root 'publish\NexusFlow.Web.dll'), 'new application')
    [IO.File]::WriteAllText((Join-Path $root 'publish\NexusFlow.Web.runtimeconfig.json'), '{}')
    [IO.File]::WriteAllText((Join-Path $root 'publish\web.config'), 'developer config')
    [IO.File]::WriteAllText((Join-Path $root 'publish\appsettings.json'), 'developer secrets')
    [IO.File]::WriteAllText((Join-Path $root 'publish\appsettings.Production.json'), 'developer production secrets')
    [IO.File]::WriteAllText((Join-Path $root 'site\NexusFlow.Web.dll'), 'old application')
    [IO.File]::WriteAllText((Join-Path $root 'site\web.config'), 'server config')
    [IO.File]::WriteAllText((Join-Path $root 'site\appsettings.json'), 'server settings')
    & (Join-Path $PSScriptRoot '..\New-GarnetRelease.ps1') -PublishPath "$root\publish" -OutputPath "$root\release" -CommitSha $sha -CiRunId '123' -CiRunAttempt '1'
    $manifest = Test-GarnetPackage "$root\release\package" $sha '123' '1'
    [pscustomobject]@{ Root = $root; Manifest = $manifest; Pending = "$root\pending.json" }
}
function New-Operations($Fixture, [string]$Health = 'Ready') {
    $state = @{ stopped = 0; started = 0; restored = 0; backupFailed = $false; copyFailed = $false; startFailed = $false; health = $Health }
    $root = $Fixture.Root
    $manifest = $Fixture.Manifest
    $operations = @{
        Stop = { $state.stopped++ }.GetNewClosure()
        Backup = {
            if ($state.backupFailed) { throw 'backup failed' }
            Get-ChildItem -LiteralPath "$root\site" -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination "$root\backup" }
            "$root\backup"
        }.GetNewClosure()
        Copy = {
            Copy-GarnetPayload "$root\release\package\payload" "$root\site" $manifest
            if ($state.copyFailed) { throw 'copy failed' }
        }.GetNewClosure()
        ValidateFiles = { Test-GarnetInstalledFiles "$root\site" $manifest }.GetNewClosure()
        Restore = { $state.restored++; Restore-GarnetPayload "$root\backup" "$root\site" $manifest }.GetNewClosure()
        Start = { $state.started++; if ($state.startFailed) { throw 'start failed' } }.GetNewClosure()
        Health = { $state.health }.GetNewClosure()
    }
    [pscustomobject]@{ State = $state; Operations = $operations }
}

try {
    Test-Case 'Package excludes server config and verifies roundtrip hashes' {
        $f = New-Fixture
        Assert-Equal @($f.Manifest.files).Count 2
        Expand-GarnetSafeZip "$($f.Root)\release\release.zip" "$($f.Root)\expanded"
        [void](Test-GarnetPackage "$($f.Root)\expanded" $sha '123' '1')
    }
    Test-Case 'Path traversal, Windows aliases and server-owned paths are rejected' {
        foreach ($path in @('../bad', '/bad', 'C:/bad', 'wwwroot/../bad', 'wwwroot\bad', 'web.config',
            'appsettings.Production.json', 'storage/file.dll', 'wwwroot/uploads/photo.jpg', 'secret.key',
            'bootstrap.key', 'CON.dll', 'assets/file.dll ', '.env', 'run.ps1', 'report.xlsx', 'appsettings.json.br', 'web.config.gz')) {
            Assert-Equal (Test-GarnetPayloadPath $path) $false
        }
        Assert-Equal (Test-GarnetPayloadPath 'wwwroot/js/app.js') $true
        Assert-Throws { Assert-GarnetChildPath $testRoot ([IO.Path]::GetFullPath((Join-Path $testRoot '..\outside'))) } 'outside'
    }
    Test-Case 'Wrong commit, run or attempt cannot be deployed' {
        $f = New-Fixture
        Assert-Throws { Test-GarnetPackage "$($f.Root)\release\package" ('b' * 40) '123' '1' } 'provenance'
        Assert-Throws { Test-GarnetPackage "$($f.Root)\release\package" $sha '999' '1' } 'provenance'
        Assert-Throws { Test-GarnetPackage "$($f.Root)\release\package" $sha '123' '2' } 'provenance'
    }
    Test-Case 'CI admission rejects PRs, failed runs, stale attempts and expired artifacts' {
        $run = [pscustomobject]@{ id = 123; status = 'completed'; conclusion = 'success'; event = 'push'; head_branch = 'main';
            head_repository = [pscustomobject]@{ full_name = 'Versal-Labs/NexusFlow_ERP' }; head_sha = $sha;
            path = '.github/workflows/nexusflow-ci.yml'; run_attempt = 1 }
        $artifact = [pscustomobject]@{ name = 'garnet-release-123-1'; expired = $false; workflow_run = [pscustomobject]@{ head_sha = $sha } }
        Assert-Equal (Assert-GarnetCiMetadata $run @($artifact) '123') 'garnet-release-123-1'
        $run.path = '.github/workflows/nexusflow-ci.yml@main'
        Assert-Equal (Assert-GarnetCiMetadata $run @($artifact) '123') 'garnet-release-123-1'
        $run.path = '.github/workflows/nexusflow-ci.yml@other-branch'
        Assert-Throws { Assert-GarnetCiMetadata $run @($artifact) '123' } 'successful'
        $run.path = '.github/workflows/nexusflow-ci.yml'
        $run.event = 'pull_request'
        Assert-Throws { Assert-GarnetCiMetadata $run @($artifact) '123' } 'successful'
        $run.event = 'push'; $run.conclusion = 'failure'
        Assert-Throws { Assert-GarnetCiMetadata $run @($artifact) '123' } 'successful'
        $run.conclusion = 'success'; $run.run_attempt = 2
        Assert-Throws { Assert-GarnetCiMetadata $run @($artifact) '123' } 'artifact'
        $run.run_attempt = 1; $artifact.expired = $true
        Assert-Throws { Assert-GarnetCiMetadata $run @($artifact) '123' } 'artifact'
    }
    Test-Case 'Tampered and extra payload files fail validation' {
        $f = New-Fixture
        [IO.File]::WriteAllText("$($f.Root)\release\package\payload\NexusFlow.Web.dll", 'tampered')
        Assert-Throws { Test-GarnetPackage "$($f.Root)\release\package" $sha '123' '1' } 'corrupted'
        $f = New-Fixture
        [IO.File]::WriteAllText("$($f.Root)\release\package\payload\unexpected.txt", 'extra')
        Assert-Throws { Test-GarnetPackage "$($f.Root)\release\package" $sha '123' '1' } 'not listed'
    }
    Test-Case 'Unsafe archive is rejected before extraction' {
        $zip = Join-Path $testRoot 'unsafe.zip'
        $archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
        [void]$archive.CreateEntry('payload/../../escape.dll')
        $archive.Dispose()
        Assert-Throws { Expand-GarnetSafeZip $zip (Join-Path $testRoot 'unsafe-expanded') } 'unsafe'
        Assert-Equal (Test-Path -LiteralPath (Join-Path $testRoot 'unsafe-expanded')) $false
    }
    Test-Case 'Existing site discovered by path; missing/ambiguous targets fail' {
        $site = [pscustomobject]@{ Name = 'Garnet'; PhysicalPath = 'C:\inetpub\wwwroot\Nexus-Garnet-Prod'; ApplicationPool = 'ExistingPool' }
        Assert-Equal (Resolve-GarnetIisSite @($site)).Name 'Garnet'
        Assert-Throws { Resolve-GarnetIisSite @() } 'exactly one'
        Assert-Throws { Resolve-GarnetIisSite @($site, $site) } 'exactly one'
    }
    Test-Case 'Configuration and pool-identity drift are rejected' {
        $actual = [pscustomobject]@{ sitePath = 'C:\inetpub\wwwroot\Nexus-Garnet-Prod'; siteName = 'Existing'; poolName = 'Existing';
            instanceId = 'nexus-garnet-2026'; instanceRoot = 'C:\ProgramData\NexusFlow\ERP\nexus-garnet-2026';
            poolIdentity = 'ApplicationPoolIdentity:'; binding = '*:443:nexus.garnetnation.lk'; environmentHash = 'envhash';
            configurationHashes = @{ 'web.config' = 'hash' }; certificateHash = 'new-certificate' }
        $expected = [pscustomobject]@{ schemaVersion = 1; sitePath = $actual.sitePath; siteName = $actual.siteName; poolName = $actual.poolName;
            instanceId = $actual.instanceId; instanceRoot = $actual.instanceRoot; poolIdentity = $actual.poolIdentity;
            binding = $actual.binding; environmentHash = $actual.environmentHash; configurationHashes = [pscustomobject]@{ 'web.config' = 'hash' };
            certificateHash = 'old-certificate' }
        Assert-GarnetPinnedTarget $expected $actual
        $actual.configurationHashes['web.config'] = 'changed'
        Assert-Throws { Assert-GarnetPinnedTarget $expected $actual } 'configuration'
        $actual.configurationHashes['web.config'] = 'hash'; $actual.poolIdentity = 'LocalSystem:'
        Assert-Throws { Assert-GarnetPinnedTarget $expected $actual } 'poolIdentity'
    }
    Test-Case 'Successful update preserves configuration and server-only files' {
        $f = New-Fixture; $op = New-Operations $f
        [IO.File]::WriteAllText("$($f.Root)\site\user-upload.jpg", 'user upload')
        $result = Invoke-GarnetFileUpdate $op.Operations $f.Pending $f.Manifest
        Assert-Equal $result.status 'Ready'
        Assert-Equal (Get-Content -LiteralPath "$($f.Root)\site\web.config" -Raw) 'server config'
        Assert-Equal (Get-Content -LiteralPath "$($f.Root)\site\appsettings.json" -Raw) 'server settings'
        Assert-Equal (Get-Content -LiteralPath "$($f.Root)\site\user-upload.jpg" -Raw) 'user upload'
        Assert-Equal $op.State.started 1
    }
    Test-Case 'Backup failure copies nothing and restarts previous application' {
        $f = New-Fixture; $op = New-Operations $f; $op.State.backupFailed = $true
        Assert-Throws { Invoke-GarnetFileUpdate $op.Operations $f.Pending $f.Manifest } 'backup failed'
        Assert-Equal (Get-Content -LiteralPath "$($f.Root)\site\NexusFlow.Web.dll" -Raw) 'old application'
        Assert-Equal $op.State.started 1
        Assert-Equal $op.State.restored 0
        Assert-Equal (Test-Path -LiteralPath $f.Pending) $false
    }
    Test-Case 'Partial copy is restored before startup; only new payload files removed' {
        $f = New-Fixture; $op = New-Operations $f; $op.State.copyFailed = $true
        [IO.File]::WriteAllText("$($f.Root)\site\user-upload.jpg", 'keep')
        Assert-Throws { Invoke-GarnetFileUpdate $op.Operations $f.Pending $f.Manifest } 'copy failed'
        Assert-Equal (Get-Content -LiteralPath "$($f.Root)\site\NexusFlow.Web.dll" -Raw) 'old application'
        Assert-Equal (Test-Path -LiteralPath "$($f.Root)\site\NexusFlow.Web.runtimeconfig.json") $false
        Assert-Equal (Test-Path -LiteralPath "$($f.Root)\site\user-upload.jpg") $true
        Assert-Equal $op.State.restored 1
    }
    Test-Case 'Start or readiness failure never automatically rolls back new files' {
        foreach ($startFails in @($true, $false)) {
            $f = New-Fixture; $op = New-Operations $f 'Failed'; $op.State.startFailed = $startFails
            Assert-Throws { Invoke-GarnetFileUpdate $op.Operations $f.Pending $f.Manifest }
            Assert-Equal $op.State.restored 0
            Assert-Equal (Test-Path -LiteralPath $f.Pending) $true
            Assert-Equal (Get-Content -LiteralPath "$($f.Root)\site\NexusFlow.Web.dll" -Raw) 'new application'
        }
    }
    Test-Case 'Upgrade-required release is pending and blocks another copy' {
        $f = New-Fixture; $op = New-Operations $f 'AwaitingUpgrade'
        Assert-Equal (Invoke-GarnetFileUpdate $op.Operations $f.Pending $f.Manifest).status 'AwaitingUpgrade'
        Assert-Throws { Invoke-GarnetFileUpdate $op.Operations $f.Pending $f.Manifest } 'unresolved'
        Assert-Equal $op.State.stopped 1
    }
    Test-Case 'Verify completes the same upgraded release without copying again' {
        $f = New-Fixture; $op = New-Operations $f 'AwaitingUpgrade'
        [void](Invoke-GarnetFileUpdate $op.Operations $f.Pending $f.Manifest)
        $record = Get-Content -LiteralPath $f.Pending -Raw | ConvertFrom-Json
        Assert-GarnetVerificationRecord $record $sha '123' '1'
        Assert-Throws { Assert-GarnetVerificationRecord $record ('b' * 40) '123' '1' } 'deployed release'
        Test-GarnetInstalledFiles "$($f.Root)\site" $f.Manifest
        $latest = Join-Path $f.Root 'latest.json'
        Complete-GarnetVerification $record $f.Pending $latest
        Assert-Equal (Test-Path -LiteralPath $f.Pending) $false
        Assert-Equal (Get-Content -LiteralPath $latest -Raw | ConvertFrom-Json).status 'Ready'
        Assert-Equal $op.State.stopped 1
        Assert-Equal $op.State.started 1
    }
    Test-Case 'Health distinguishes ready, controlled upgrade, wrong instance and failure' {
        $live = [pscustomobject]@{ StatusCode = 200; Body = [pscustomobject]@{ status = 'live'; instance = 'nexus-garnet-2026' } }
        $ready = [pscustomobject]@{ StatusCode = 200; Body = [pscustomobject]@{ status = 'ready' } }
        Assert-Equal (Get-GarnetHealthStatus $live $ready) 'Ready'
        $ready = [pscustomobject]@{ StatusCode = 503; Body = [pscustomobject]@{ status = 'not-ready'; mode = 'UpgradeRequired' } }
        Assert-Equal (Get-GarnetHealthStatus $live $ready) 'AwaitingUpgrade'
        $live.Body.instance = 'production'
        Assert-Equal (Get-GarnetHealthStatus $live $ready) 'Failed'
        $live.Body.instance = 'nexus-garnet-2026'; $ready.Body.mode = 'Uninitialized'
        Assert-Equal (Get-GarnetHealthStatus $live $ready) 'Failed'
        $ready = [pscustomobject]@{ StatusCode = 503; Body = [pscustomobject]@{ status = 'not-ready' } }
        Assert-Equal (Get-GarnetHealthStatus $live $ready) 'Failed'
        $ready.Body = $null
        Assert-Equal (Get-GarnetHealthStatus $live $ready) 'Failed'
    }
    Test-Case 'Overlapping user storage is refused before deployment' {
        $f = New-Fixture
        Assert-Throws { Assert-GarnetDestination "$($f.Root)\site" $f.Manifest @("$($f.Root)\site") } 'storage'
    }
    Test-Case 'File/directory collisions are refused before deployment' {
        $f = New-Fixture
        [void](New-Item -ItemType Directory -Path "$($f.Root)\site\NexusFlow.Web.runtimeconfig.json")
        Assert-Throws { Assert-GarnetDestination "$($f.Root)\site" $f.Manifest } 'existing directory'
        [IO.File]::WriteAllText("$($f.Root)\site\assets", 'server-owned file')
        $manifest = [pscustomobject]@{ files = @([pscustomobject]@{ path = 'assets/application.js'; sha256 = 'a' * 64 }) }
        Assert-Throws { Assert-GarnetDestination "$($f.Root)\site" $manifest } 'existing file'
    }
    Test-Case 'Mocked IIS registration reads existing target and rejects shared/cloud/wrong-instance setups' {
        $module = Get-Module Garnet.Iis
        & $module {
            $script:mockSite = [pscustomobject]@{ Name = 'ActualGarnet'; PhysicalPath = 'C:\inetpub\wwwroot\Nexus-Garnet-Prod'; ApplicationPool = 'ActualPool' }
            $script:mockSites = @($script:mockSite)
            $script:mockXml = '<configuration><system.webServer><aspNetCore processPath="dotnet" arguments=".\NexusFlow.Web.dll"><environmentVariables><environmentVariable name="NEXUSFLOW_INSTANCE_ID" value="nexus-garnet-2026" /></environmentVariables></aspNetCore></system.webServer></configuration>'
            function script:Import-Module { param($Name, $ErrorAction) if ($Name -ne 'WebAdministration') { throw 'Unexpected module' } }
            function script:Assert-GarnetNoReparsePoint { param($Path) }
            function script:Get-Website { param($Name) $script:mockSites }
            function script:Get-WebApplication { param($Site) @() }
            function script:Get-WebBinding { param($Name, $Protocol) [pscustomobject]@{ bindingInformation = '*:443:nexus.garnetnation.lk'; certificateHash = 'certificate' } }
            function script:Get-Item {
                param($LiteralPath)
                if ($LiteralPath -like 'IIS:*') {
                    return [pscustomobject]@{ environmentVariables = [pscustomobject]@{ Collection = @() };
                        processModel = [pscustomobject]@{ identityType = 'ApplicationPoolIdentity'; userName = '' } }
                }
                [pscustomobject]@{ Name = 'web.config'; FullName = $LiteralPath }
            }
            function script:Get-Content {
                param($LiteralPath, [switch]$Raw)
                if ($LiteralPath -like '*web.config') { return $script:mockXml }
                if ($LiteralPath -like '*installation-state.json') { return '{"InstanceId":"nexus-garnet-2026","Mode":"Installed"}' }
                throw 'Unexpected read'
            }
            function script:Test-Path { param($LiteralPath) $false }
            function script:Get-ChildItem { param($LiteralPath, $Filter, [switch]$File) @() }
            function script:Get-FileHash { param($LiteralPath, $Algorithm) [pscustomobject]@{ Hash = 'hash' } }
            $target = Get-GarnetIisTarget
            if ($target.siteName -ne 'ActualGarnet' -or $target.poolName -ne 'ActualPool' -or $target.instanceId -ne 'nexus-garnet-2026') { throw 'Existing target was not resolved.' }
            $script:mockSites += [pscustomobject]@{ Name = 'OtherSite'; PhysicalPath = 'C:\inetpub\other'; ApplicationPool = 'ActualPool' }
            $caught = $false
            try { Get-GarnetIisTarget } catch { if ($_.Exception.Message -notlike '*dedicated*') { throw }; $caught = $true }
            if (!$caught) { throw 'Shared pool was accepted.' }
            $script:mockSites = @($script:mockSite)
            $script:mockXml = $script:mockXml.Replace('nexus-garnet-2026', 'production')
            $caught = $false
            try { Get-GarnetIisTarget } catch { if ($_.Exception.Message -notlike '*instance ID*') { throw }; $caught = $true }
            if (!$caught) { throw 'Wrong instance was accepted.' }
            $script:mockXml = $script:mockXml.Replace('production', 'nexus-garnet-2026').Replace('</environmentVariables>', '<environmentVariable name="NEXUSFLOW_STATE_STORE" value="AzureBlob" /></environmentVariables>')
            $caught = $false
            try { Get-GarnetIisTarget } catch { if ($_.Exception.Message -notlike '*Cloud*') { throw }; $caught = $true }
            if (!$caught) { throw 'Cloud state was accepted without a backup adapter.' }
        }
        # Discard read-adapter mocks before testing other module functions.
        Remove-Module Garnet.Iis
        Import-Module (Join-Path $PSScriptRoot '..\Garnet.Iis.psm1') -Force
    }
    Test-Case 'Already-stopped pool is harmless (mocked IIS)' {
        $module = Get-Module Garnet.Iis
        & $module {
            function script:Get-Website { param($Name) [pscustomobject]@{ State = 'Stopped' } }
            function script:Get-WebAppPoolState { param($Name) [pscustomobject]@{ Value = 'Stopped' } }
            function script:Get-CimInstance { param($Namespace, $ClassName) @() }
            function script:Stop-Website { param($Name) throw 'Should not stop twice' }
            function script:Stop-WebAppPool { param($Name) throw 'Should not stop twice' }
            Stop-GarnetIis ([pscustomobject]@{ siteName = 'Garnet'; poolName = 'GarnetPool' })
        }
    }
    Test-Case 'PowerShell HTTP 503 error details retain the upgrade-required JSON' {
        $module = Get-Module Garnet.Iis
        & $module {
            function script:Invoke-WebRequest {
                param($Uri, [switch]$UseBasicParsing, $TimeoutSec, $MaximumRedirection, $Headers)
                $exception = [Exception]::new('HTTP 503')
                $exception | Add-Member -NotePropertyName Response -NotePropertyValue ([pscustomobject]@{ StatusCode = 503 })
                $httpError = [Management.Automation.ErrorRecord]::new($exception, 'HTTP503', [Management.Automation.ErrorCategory]::InvalidOperation, $Uri)
                $httpError.ErrorDetails = [Management.Automation.ErrorDetails]::new('{"status":"not-ready","mode":"UpgradeRequired"}')
                throw $httpError
            }
            $response = Get-GarnetHttpResponse '/health/ready'
            if ($response.StatusCode -ne 503 -or $response.Body.mode -ne 'UpgradeRequired') { throw 'Upgrade-required response was lost.' }
        }
    }
    Test-Case 'Worker timeout retains pending state and performs no copy (mocked IIS)' {
        $f = New-Fixture; $op = New-Operations $f
        $op.Operations.Stop = { throw 'Application pool did not release workers' }
        Assert-Throws { Invoke-GarnetFileUpdate $op.Operations $f.Pending $f.Manifest } 'workers'
        Assert-Equal (Test-Path -LiteralPath $f.Pending) $true
        Assert-Equal $op.State.started 0
        Assert-Equal (Get-Content -LiteralPath "$($f.Root)\site\NexusFlow.Web.dll" -Raw) 'old application'
    }
    Test-Case 'Interrupted-copy marker refuses verification phase and a new deployment' {
        $f = New-Fixture; $op = New-Operations $f
        $record = [pscustomobject]@{ phase = 'BackedUp'; status = 'InProgress'; commitSha = $sha; ciRunId = '123'; ciRunAttempt = '1' }
        Write-GarnetJson $f.Pending $record
        Assert-Throws { Assert-GarnetVerificationRecord $record $sha '123' '1' } 'deployed release'
        Assert-Throws { Invoke-GarnetFileUpdate $op.Operations $f.Pending $f.Manifest } 'unresolved'
        Assert-Equal $op.State.stopped 0
    }
    Test-Case 'Exclusive lock rejects simultaneous server operations' {
        $path = Join-Path $testRoot 'release.lock'
        $handle = [IO.File]::Open($path, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try { Assert-Throws { [IO.File]::Open($path, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) } }
        finally { $handle.Dispose() }
    }
} finally {
    # Exact owned GUID fixture directory only; validate before recursive cleanup.
    $leaf = [IO.Path]::GetFileName($testRoot)
    if ($leaf -match '^NexusFlow-ReleaseTests-[a-f0-9]{32}$' -and
        (Get-GarnetFullPath ([IO.Path]::GetDirectoryName($testRoot))) -ieq (Get-GarnetFullPath ([IO.Path]::GetTempPath()))) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
Write-Host "Deployment tests: $script:passed passed; $script:failed failed."
if ($script:failed) { throw 'Deployment regression tests failed.' }
