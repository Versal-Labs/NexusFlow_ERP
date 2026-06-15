[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Configuration = "Release",
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
$OutputDirectory = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $PSScriptRoot "artifacts"
}
else {
    $OutputDirectory
}
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$publishPath = Join-Path $OutputDirectory "NexusFlow-$Version"
$zipPath = Join-Path $OutputDirectory "NexusFlow-$Version-iis.zip"

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if (Test-Path -LiteralPath $publishPath) {
    Remove-Item -LiteralPath $publishPath -Recurse -Force
}

dotnet publish (Join-Path $repoRoot "NexusFlow.Web\NexusFlow.Web.csproj") `
    --configuration $Configuration `
    --output $publishPath `
    --self-contained false `
    -p:Version=$Version

Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Deploy-NexusFlow.ps1") -Destination $publishPath
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "New-NexusFlowSetupKey.ps1") -Destination $publishPath

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $publishPath "*") -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "Release package created: $zipPath"
