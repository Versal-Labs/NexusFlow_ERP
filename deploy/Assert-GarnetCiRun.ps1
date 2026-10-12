[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+$')][string]$CiRunId,
    [string]$CiRunAttempt = ''
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Garnet.Release.psm1') -Force
if (!$env:GITHUB_TOKEN -or $env:GITHUB_REPOSITORY -cne 'Versal-Labs/NexusFlow_ERP' -or $env:GITHUB_REF -cne 'refs/heads/main') {
    throw 'Release validation requires an authenticated main-branch workflow in the expected repository.'
}
$headers = @{ Authorization = "Bearer $env:GITHUB_TOKEN"; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
$base = 'https://api.github.com/repos/Versal-Labs/NexusFlow_ERP/actions'
$repository = Invoke-RestMethod -Uri 'https://api.github.com/repos/Versal-Labs/NexusFlow_ERP' -Headers $headers
if (!$repository.private) { throw 'This deployment runner is configured for a private repository only.' }
$runUri = "$base/runs/$CiRunId"
if ($CiRunAttempt) {
    if ($CiRunAttempt -notmatch '^[1-9]\d*$') { throw 'CI run attempt must be a positive integer.' }
    $runUri += "/attempts/$CiRunAttempt"
}
$run = Invoke-RestMethod -Uri $runUri -Headers $headers
$artifacts = Invoke-RestMethod -Uri "$base/runs/$CiRunId/artifacts?per_page=100" -Headers $headers
$artifactName = Assert-GarnetCiMetadata $run @($artifacts.artifacts) $CiRunId
if ($env:GITHUB_OUTPUT) {
    Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "commit_sha=$($run.head_sha)"
    Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "ci_run_attempt=$($run.run_attempt)"
    Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "artifact_name=$artifactName"
}
Write-Host "Approved main CI run $CiRunId, attempt $($run.run_attempt), commit $($run.head_sha)."
