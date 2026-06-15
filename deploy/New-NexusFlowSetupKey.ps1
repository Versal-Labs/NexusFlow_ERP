[CmdletBinding()]
param(
    [ValidatePattern("^[A-Za-z0-9_.-]+$")]
    [string]$InstanceId = "default",

    [string]$InstanceRoot
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($InstanceRoot)) {
    $InstanceRoot = Join-Path $env:ProgramData "NexusFlow\ERP\$InstanceId"
}

$resolvedRoot = [IO.Path]::GetFullPath($InstanceRoot)
$statePath = Join-Path $resolvedRoot "installation-state.json"
if (-not (Test-Path -LiteralPath $statePath)) {
    throw "No NexusFlow installation state exists at '$statePath'. Start the application once or use Deploy-NexusFlow.ps1 first."
}

$setupKeyBytes = [byte[]]::new(32)
$salt = [byte[]]::new(32)
$random = [Security.Cryptography.RNGCryptoServiceProvider]::new()
try {
    $random.GetBytes($setupKeyBytes)
    $random.GetBytes($salt)
}
finally {
    $random.Dispose()
}
$setupKey = [Convert]::ToBase64String($setupKeyBytes).TrimEnd("=").Replace("+", "-").Replace("/", "_")
$deriver = [Security.Cryptography.Rfc2898DeriveBytes]::new(
    $setupKey, $salt, 210000, [Security.Cryptography.HashAlgorithmName]::SHA256)
try {
    $hash = $deriver.GetBytes(32)
}
finally {
    $deriver.Dispose()
}

$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json

$state.InstanceId = $InstanceId
$state.SetupKeySalt = [Convert]::ToBase64String($salt)
$state.SetupKeyHash = [Convert]::ToBase64String($hash)
$state.SetupKeyConsumed = $false
$state.UpdatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")

$temporaryPath = "$statePath.tmp"
$json = $state | ConvertTo-Json -Depth 10
[IO.File]::WriteAllText($temporaryPath, $json, [Text.UTF8Encoding]::new($false))
Move-Item -LiteralPath $temporaryPath -Destination $statePath -Force

Write-Host ""
Write-Host "A new one-time NexusFlow setup/maintenance key was created."
Write-Host "Instance state: $statePath"
Write-Host "One-time setup key: $setupKey"
Write-Host "The key is shown only in this output and will be consumed after successful setup or upgrade."
