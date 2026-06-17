[CmdletBinding()]
param(
    [string]$ImageName = "nexusflow-erp",
    [string]$Tag = "local",
    [string]$Registry,
    [switch]$Push
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$imageTag = if ([string]::IsNullOrWhiteSpace($Registry)) {
    "$ImageName`:$Tag"
} else {
    "$($Registry.TrimEnd('/'))/$ImageName`:$Tag"
}

Write-Host "Building container image $imageTag"
docker build --file (Join-Path $repoRoot "Dockerfile") --tag $imageTag $repoRoot

if ($Push) {
    Write-Host "Pushing container image $imageTag"
    docker push $imageTag
}

Write-Host "Container image ready: $imageTag"
