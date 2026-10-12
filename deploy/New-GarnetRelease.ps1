[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishPath,
    [Parameter(Mandatory)][string]$OutputPath,
    [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$CommitSha,
    [Parameter(Mandatory)][ValidatePattern('^\d+$')][string]$CiRunId,
    [Parameter(Mandatory)][ValidatePattern('^\d+$')][string]$CiRunAttempt,
    [string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent)
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Garnet.Release.psm1') -Force
$publish = Get-GarnetFullPath $PublishPath
$output = Get-GarnetFullPath $OutputPath
if (Test-Path -LiteralPath $output) { throw 'Release output must be a new directory.' }
if ($output -ieq $publish -or $output.StartsWith($publish + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Release output must be outside the publish directory.'
}
Assert-GarnetNoReparsePoint $publish
[void](New-Item -ItemType Directory -Path (Join-Path $output 'package\payload') -Force)
$payload = Join-Path $output 'package\payload'
$files = @()
foreach ($file in Get-ChildItem -LiteralPath $publish -File -Recurse -Force | Sort-Object FullName) {
    Assert-GarnetNoReparsePoint $file.FullName
    $relative = $file.FullName.Substring($publish.Length + 1).Replace('\', '/')
    if (!(Test-GarnetPayloadPath $relative)) { continue }
    $destination = Assert-GarnetChildPath $payload (Join-Path $payload $relative)
    [void](New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force)
    Copy-Item -LiteralPath $file.FullName -Destination $destination
    $files += [ordered]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$migrations = @(Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'NexusFlow.Infrastructure\Migrations') -Filter '*.cs' |
    Where-Object { $_.BaseName -match '^\d{14}_' -and $_.Name -notlike '*.Designer.cs' } |
    Sort-Object Name | ForEach-Object { $_.BaseName })
$manifest = [ordered]@{
    schemaVersion = 1; repository = 'Versal-Labs/NexusFlow_ERP'; branch = 'main'
    commitSha = $CommitSha; ciRunId = $CiRunId; ciRunAttempt = $CiRunAttempt
    createdAtUtc = [DateTime]::UtcNow.ToString('o'); migrations = $migrations; files = $files
}
Write-GarnetJson (Join-Path $output 'package\release-manifest.json') $manifest
[void](Test-GarnetPackage (Join-Path $output 'package') $CommitSha $CiRunId $CiRunAttempt)
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $output 'release.zip'
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    $packageRoot = Get-GarnetFullPath (Join-Path $output 'package')
    foreach ($file in Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Sort-Object FullName) {
        # .NET Framework CreateFromDirectory emits backslashes; explicitly use ZIP-standard slash names.
        $entryName = $file.FullName.Substring($packageRoot.Length + 1).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entryName)
    }
} finally { $archive.Dispose() }
[IO.File]::WriteAllText(($zip + '.sha256'), (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant(), [Text.UTF8Encoding]::new($false))
Write-Host "Verified release package: $zip"
